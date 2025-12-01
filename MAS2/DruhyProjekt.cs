//https://github.com/CoMuNeLab/EU-Air-Transportation-Multiplex
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using MAS2;

public class DruhyProjekt
{
    public static void Main(string[] args)
    {
        string datasetPath = Path.Combine("data", "EU-Air-Transportation-Multiplex-main", "Dataset");
        string nodesFile = Path.Combine(datasetPath, "EUAirTransportation_nodes.txt");
        string edgesFile = Path.Combine(datasetPath, "EUAirTransportation_multiplex.edges");
        string layersFile = Path.Combine(datasetPath, "EUAirTransportation_layers.txt");

        if (!File.Exists(nodesFile) || !File.Exists(edgesFile))
        {
            Console.WriteLine($"Dataset files not found in {datasetPath}");
            return;
        }

        // Load Labels
        var nodeLabels = LoadLabels(nodesFile);
        var layerLabels = LoadLabels(layersFile);

        // 1. Load Nodes and Coordinates
        Console.WriteLine("Loading nodes and coordinates...");
        var nodePositions = LoadAndNormalizePositions(nodesFile, 1200, 1200, 50);
        
        string outputDir = Path.Combine("data", "DruhyProjekt");
        Directory.CreateDirectory(outputDir);

        // Save positions for NetworkPlotting
        string positionsFile = Path.Combine(outputDir, "network_positions.csv");
        NetworkPlotting.SavePositions(nodePositions, positionsFile);
        Console.WriteLine($"Saved normalized node positions to {positionsFile}");

        // 2. Load Multiplex Network
        Console.WriteLine("Loading multiplex network...");
        var ml = LoadEUAirTransportationNetwork(edgesFile, 450, 37);
        Console.WriteLine($"Loaded multilayer network with {ml.Layers.Count} layers and {ml.NodeCount} nodes.");

        // 3. Run Analysis (adapted from Cviko7)
        RunAnalysis(ml, args, outputDir, nodeLabels, layerLabels);

        // 4. Geographic Analysis
        var rawPositions = LoadRawNodePositions(nodesFile);
        RunGeographicAnalysis(ml, rawPositions, outputDir, positionsFile, layerLabels);

        // 5. Aggregate Analysis
        RunAggregateAnalysis(ml, outputDir, positionsFile, nodeLabels);

        // 6. Layer Comparison Analysis (Structural & Jaccard)
        RunLayerComparisonAnalysis(ml, outputDir, layerLabels);
    }

    private static Dictionary<int, string> LoadLabels(string file)
    {
        var dict = new Dictionary<int, string>();
        if (!File.Exists(file)) return dict;
        foreach (var line in File.ReadAllLines(file).Skip(1))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && int.TryParse(parts[0], out int id))
            {
                dict[id - 1] = parts[1]; // 0-indexed
            }
        }
        return dict;
    }

    private static Dictionary<int, NetworkPlotting.NodePosition> LoadAndNormalizePositions(string nodesFile, float width, float height, float padding)
    {
        var positions = new Dictionary<int, NetworkPlotting.NodePosition>();
        var lines = File.ReadAllLines(nodesFile).Skip(1); // Skip header

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var line in lines)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue;

            int id = int.Parse(parts[0]) - 1; // 0-indexed
            // Longitude is X, Latitude is Y
            float x = float.Parse(parts[2], CultureInfo.InvariantCulture);
            float y = float.Parse(parts[3], CultureInfo.InvariantCulture);

            positions[id] = new NetworkPlotting.NodePosition(id, x, y);

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        // Normalize to canvas size
        float rangeX = maxX - minX;
        float rangeY = maxY - minY;
        
        // Use uniform scale to preserve aspect ratio (map shape)
        float scaleX = (width - 2 * padding) / rangeX;
        float scaleY = (height - 2 * padding) / rangeY;
        float scale = Math.Min(scaleX, scaleY);
        
        foreach (var pos in positions.Values)
        {
            pos.X = padding + (pos.X - minX) * scale;
            // Flip Y because screen coordinates (0,0) is top-left, but latitude increases upwards
            pos.Y = height - (padding + (pos.Y - minY) * scale); 
        }

        return positions;
    }

    private static MultilayerNetwork LoadEUAirTransportationNetwork(string edgesFile, int nodeCount, int layerCount)
    {
        var layers = new DokSparseMatrix<int>[layerCount];
        for (int i = 0; i < layerCount; i++)
        {
            layers[i] = new DokSparseMatrix<int>(nodeCount, nodeCount);
        }

        var lines = File.ReadAllLines(edgesFile);
        foreach (var line in lines)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue;

            int layerId = int.Parse(parts[0]);
            int u = int.Parse(parts[1]);
            int v = int.Parse(parts[2]);
            // weight is usually 1.0, we can ignore or parse it.
            
            // Adjust to 0-indexed
            int layerIdx = layerId - 1;
            int uIdx = u - 1;
            int vIdx = v - 1;

            if (layerIdx >= 0 && layerIdx < layerCount && uIdx >= 0 && uIdx < nodeCount && vIdx >= 0 && vIdx < nodeCount)
            {
                // Undirected
                layers[layerIdx][uIdx, vIdx] = 1;
                layers[layerIdx][vIdx, uIdx] = 1;
            }
        }

        return new MultilayerNetwork(layers);
    }

    private static void RunAnalysis(MultilayerNetwork ml, string[] args, string outputDir, Dictionary<int, string> nodeLabels, Dictionary<int, string> layerLabels)
    {
        // Simple CLI flags
        double minSharedFrac = 0.5;     // keep layers with at least this fraction of edges that are shared widely
        int? edgeSharedMinLayersOpt = null; // if null, will default to ceil(0.5*L)
        int uniqueMaxLayers = 1;        // edges counted as "too unique" if they appear in <= this many layers
        double maxUniqueFrac = 0.4;     // drop layers whose unique-fraction exceeds this

        for (int i = 0; i < (args?.Length ?? 0); i++)
        {
            var a = args[i];
            if (a == "--sharedMinFrac" && i + 1 < args.Length && double.TryParse(args[i + 1], out var f)) { minSharedFrac = f; i++; continue; }
            if (a == "--edgeSharedMinLayers" && i + 1 < args.Length && int.TryParse(args[i + 1], out var k)) { edgeSharedMinLayersOpt = k; i++; continue; }
            if (a == "--uniqueMaxLayers" && i + 1 < args.Length && int.TryParse(args[i + 1], out var u)) { uniqueMaxLayers = u; i++; continue; }
            if (a == "--maxUniqueFrac" && i + 1 < args.Length && double.TryParse(args[i + 1], out var mf)) { maxUniqueFrac = mf; i++; continue; }
        }

        // ---- Save full-network flattenings for comparison ----
        var flatAllUnweighted = ml.FlattenUnweighted();
        var flatAllWeighted = ml.FlattenWeighted(bySum: false);
        SaveEdgeListCsv(flatAllUnweighted, Path.Combine(outputDir, "DruhyProjekt_flatten_unweighted_all.csv"));
        SaveEdgeListCsv(flatAllWeighted, Path.Combine(outputDir, "DruhyProjekt_flatten_weighted_all.csv"));
        Console.WriteLine("Saved full-network flattened edge lists: DruhyProjekt_flatten_unweighted_all.csv and DruhyProjekt_flatten_weighted_all.csv");

        // ==========================================================================================
        // INTEGRATED METRICS FROM CVIKO5
        // ==========================================================================================
        
        Console.WriteLine($"Calculating metrics... Output directory: {outputDir}/");

        // 1. Basic Multilayer Measures
        var degreesPerLayer = ml.GetDegreesPerLayer();
        var clusteringPerLayer = ml.GetClusteringPerLayer();
        var aggregateDegree = ml.GetAggregateDegree();
        var multiplexDegree = ml.GetMultiplexDegree();
        
        // Save multilayer_node_measures.csv
        string outPath = Path.Combine(outputDir, "multilayer_node_measures.csv");
        using (var w = new StreamWriter(outPath))
        {
            var headerParts = new List<string> { "Node", "AggregateDegree", "MultiplexDegree", "AvgClustering" };
            for (int layerIdx = 0; layerIdx < ml.Layers.Count; layerIdx++)
            {
                headerParts.Add($"Deg_L{layerIdx}");
                headerParts.Add($"Clust_L{layerIdx}");
            }
            w.WriteLine(string.Join(',', headerParts));

            for (int i = 0; i < ml.NodeCount; i++)
            {
                double avgClust = 0.0;
                if (ml.Layers.Count > 0)
                    avgClust = clusteringPerLayer.Select(a => a[i]).Average();

                var parts = new List<string>
                {
                    i.ToString(),
                    aggregateDegree[i].ToString(),
                    multiplexDegree[i].ToString(),
                    avgClust.ToString("F6", CultureInfo.InvariantCulture)
                };

                for (int layerIdx = 0; layerIdx < ml.Layers.Count; layerIdx++)
                {
                    parts.Add(degreesPerLayer[layerIdx][i].ToString());
                    parts.Add(clusteringPerLayer[layerIdx][i].ToString("F6", CultureInfo.InvariantCulture));
                }

                w.WriteLine(string.Join(',', parts));
            }
        }
        Console.WriteLine($"Per-node measures saved to: {outPath}");

        // 2. Measures on Unweighted Flattening (flatAllUnweighted)
        // Note: flatAllUnweighted is already computed above
        
        var degUnw = MultilayerNetwork.DegreeCentrality(flatAllUnweighted, weighted: false); 
        var neighUnw = MultilayerNetwork.NeighborCounts(flatAllUnweighted);                  
        var avgDistUnw = MultilayerNetwork.AverageShortestPathUnweighted(flatAllUnweighted); 

        // Multilayer set-based measures across all layers
        var allLayersIdx = Enumerable.Range(0, ml.Layers.Count);
        var degreeDeviation = new double[ml.NodeCount];
        var connectiveRedundancy = new double[ml.NodeCount];
        var exclusiveNeighborhood = new int[ml.NodeCount];
        for (int i = 0; i < ml.NodeCount; i++)
        {
            degreeDeviation[i] = ml.DegreeDeviation(i, allLayersIdx);
            connectiveRedundancy[i] = ml.ConnectiveRedundancy(i, allLayersIdx);
            exclusiveNeighborhood[i] = ml.ExclusiveNeighborhood(i, allLayersIdx).Count;
        }

        // Save flattened (unweighted) measures
        string outFlatUnw = Path.Combine(outputDir, "flattened_measures_unweighted.csv");
        using (var w = new StreamWriter(outFlatUnw))
        {
            w.WriteLine("Node,Degree,DegreeDeviation,Neighborhood,ConnectiveRedundancy,XNeighborhood,AvgShortestPath");
            for (int i = 0; i < ml.NodeCount; i++)
            {
                w.WriteLine(string.Join(',', new[]
                {
                    i.ToString(),
                    degUnw[i].ToString(),
                    (double.IsNaN(degreeDeviation[i]) ? "" : degreeDeviation[i].ToString("F6", CultureInfo.InvariantCulture)),
                    neighUnw[i].ToString(),
                    connectiveRedundancy[i].ToString("F6", CultureInfo.InvariantCulture),
                    exclusiveNeighborhood[i].ToString(),
                    (double.IsNaN(avgDistUnw[i]) ? "" : avgDistUnw[i].ToString("F6", CultureInfo.InvariantCulture))
                }));
            }
        }
        Console.WriteLine($"Saved: {outFlatUnw}");

        // 3. Measures on Weighted Flattening (flatAllWeighted)
        // Note: flatAllWeighted is computed above with bySum: false (layer count).
        
        var degWeighted = MultilayerNetwork.DegreeCentrality(flatAllWeighted, weighted: true);
        var neighWeighted = MultilayerNetwork.NeighborCounts(flatAllWeighted); // Should be same as neighUnw
        var connRedWeighted = MultilayerNetwork.ConnectiveRedundancy(flatAllWeighted, weighted: true);
        var avgDistWeighted = MultilayerNetwork.AverageShortestPathWeightedByInverseMultiplicity(flatAllWeighted);

        // Save weighted measures
        string outFlatWeighted = Path.Combine(outputDir, "flattened_measures_weighted.csv");
        using (var w = new StreamWriter(outFlatWeighted))
        {
            w.WriteLine("Node,DegreeWeighted,Neighborhood,ConnectiveRedundancyWeighted,AvgShortestPathWeighted");
            for (int i = 0; i < ml.NodeCount; i++)
            {
                w.WriteLine(string.Join(',', new[]
                {
                    i.ToString(),
                    degWeighted[i].ToString(),
                    neighWeighted[i].ToString(),
                    connRedWeighted[i].ToString("F6", CultureInfo.InvariantCulture),
                    (double.IsNaN(avgDistWeighted[i]) ? "" : avgDistWeighted[i].ToString("F6", CultureInfo.InvariantCulture))
                }));
            }
        }
        Console.WriteLine($"Saved: {outFlatWeighted}");

        // 4. Charts
        
        (double[] xs, double[] ys) DiscreteFrequency(int[] values)
        {
            var groups = values.GroupBy(v => v).OrderBy(g => g.Key).ToArray();
            return (groups.Select(g => (double)g.Key).ToArray(), groups.Select(g => (double)g.Count()).ToArray());
        }

        (double[] xs, double[] ys) ContinuousFrequency(double[] values)
        {
            var groups = values.Where(v => !double.IsNaN(v) && v > 0)
                               .Select(v => Math.Round(v, 6))
                               .GroupBy(v => v).OrderBy(g => g.Key).ToArray();
            return (groups.Select(g => (double)g.Key).ToArray(), groups.Select(g => (double)g.Count()).ToArray());
        }

        // Degree deviation
        var (xDD, yDD) = ContinuousFrequency(degreeDeviation);
        if (xDD.Length > 0) ChartGenerator.SaveDistributionPng(Path.Combine(outputDir, "loglog_degree_deviation.png"), xDD, yDD, "Degree deviation (across layers)");

        // Neighborhood
        var (xNeigh, yNeigh) = DiscreteFrequency(neighUnw);
        ChartGenerator.SaveDistributionPng(Path.Combine(outputDir, "loglog_neighborhood.png"), xNeigh, yNeigh, "Neighborhood");

        // Exclusive Neighborhood
        var (xExcl, yExcl) = DiscreteFrequency(exclusiveNeighborhood);
        ChartGenerator.SaveDistributionPng(Path.Combine(outputDir, "loglog_xneighborhood.png"), xExcl, yExcl, "XNeighborhood");

        // Weighted Degree
        var (xDegW, yDegW) = DiscreteFrequency(degWeighted);
        ChartGenerator.SaveDistributionPng(Path.Combine(outputDir, "loglog_degree.png"), xDegW, yDegW, "Degree (weighted)");

        // Weighted Connective Redundancy
        var (xCrW, yCrW) = ContinuousFrequency(connRedWeighted);
        if (xCrW.Length > 0) ChartGenerator.SaveDistributionPng(Path.Combine(outputDir, "loglog_connective_redundancy.png"), xCrW, yCrW, "Connective redundancy (weighted)");

        // Weighted Avg Distance
        var (xDw, yDw) = ContinuousFrequency(avgDistWeighted);
        if (xDw.Length > 0) ChartGenerator.SaveDistributionPng(Path.Combine(outputDir, "loglog_avg_distance.png"), xDw, yDw, "Avg shortest path (weighted)");

        // 5. Individual CSVs
        void SaveSingleColumn(string path, string header, IEnumerable<string> lines)
        {
            using var sw = new StreamWriter(Path.Combine(outputDir, path));
            sw.WriteLine($"Node,{header}");
            int idx = 0;
            foreach (var val in lines) { sw.WriteLine($"{idx},{val}"); idx++; }
        }

        SaveSingleColumn("degree.csv", "Degree", degUnw.Select(v => v.ToString()));
        SaveSingleColumn("degree_deviation.csv", "DegreeDeviation", degreeDeviation.Select(v => double.IsNaN(v) ? "" : v.ToString("F6", CultureInfo.InvariantCulture)));
        SaveSingleColumn("neighborhood.csv", "Neighborhood", neighUnw.Select(v => v.ToString()));
        SaveSingleColumn("connective_redundancy.csv", "ConnectiveRedundancy", connectiveRedundancy.Select(v => v.ToString("F6", CultureInfo.InvariantCulture)));
        SaveSingleColumn("xneighborhood.csv", "XNeighborhood", exclusiveNeighborhood.Select(v => v.ToString()));
        SaveSingleColumn("average_shortest_path_unweighted.csv", "AvgShortestPath", avgDistUnw.Select(v => double.IsNaN(v) ? "" : v.ToString("F6", CultureInfo.InvariantCulture)));
        
        SaveSingleColumn("degree_weighted.csv", "DegreeWeighted", degWeighted.Select(v => v.ToString()));
        SaveSingleColumn("connective_redundancy_weighted.csv", "ConnectiveRedundancyWeighted", connRedWeighted.Select(v => v.ToString("F6", CultureInfo.InvariantCulture)));
        SaveSingleColumn("average_shortest_path_weighted.csv", "AvgShortestPathWeighted", avgDistWeighted.Select(v => double.IsNaN(v) ? "" : v.ToString("F6", CultureInfo.InvariantCulture)));

        Console.WriteLine("Metrics calculation completed.");

        // ==========================================================================================
        // INTEGRATED METRICS FROM CVIKO6 (Random Walk & Relevance)
        // ==========================================================================================
        Console.WriteLine("Calculating advanced metrics (Cviko6)...");

        // 1. Occupation Centrality via Random Walk
        Console.WriteLine("Running random walk for occupation centrality (200k steps)...");
        var occ = ml.OccupationCentrality(steps: 200_000, startNode: null, layerWeights: null, seed: 12345);

        // 2. Save Advanced Measures CSV
        string outAdvanced = Path.Combine(outputDir, "multilayer_advanced_measures.csv");
        using (var w = new StreamWriter(outAdvanced))
        {
            var header = new List<string> { "Node", "OccCentrality" };
            for (int layerIdx = 0; layerIdx < ml.Layers.Count; layerIdx++)
            {
                header.Add($"Deg_L{layerIdx}");
                header.Add($"Rel_L{layerIdx}");
                header.Add($"ExRel_L{layerIdx}");
            }
            w.WriteLine(string.Join(',', header));

            // We reuse degreesPerLayer from earlier in the method
            for (int i = 0; i < ml.NodeCount; i++)
            {
                var parts = new List<string> { i.ToString(), occ[i].ToString("F6", CultureInfo.InvariantCulture) };
                for (int layerIdx = 0; layerIdx < ml.Layers.Count; layerIdx++)
                {
                    double rel = ml.LayerRelevance(i, layerIdx);
                    double exRel = ml.ExclusiveLayerRelevance(i, layerIdx);
                    parts.Add(degreesPerLayer[layerIdx][i].ToString());
                    parts.Add(rel.ToString("F6", CultureInfo.InvariantCulture));
                    parts.Add(exRel.ToString("F6", CultureInfo.InvariantCulture));
                }
                w.WriteLine(string.Join(',', parts));
            }
        }
        Console.WriteLine($"Advanced measures saved to: {outAdvanced}");

        // 3. Comparison Tables
        Console.WriteLine("Generating relevance comparison tables...");
        RelevanceTableGenerator.GenerateComparisonTable(ml, occ, Path.Combine(outputDir, "relevance_comparison"));

        // 4. Charts for Advanced Metrics
        // Occupation centrality distribution
        var (xOcc, yOcc) = ContinuousFrequency(occ);
        if (xOcc.Length > 0)
        {
            ChartGenerator.SaveDistributionPng(Path.Combine(outputDir, "loglog_occupation_centrality.png"), xOcc, yOcc, "Occupation Centrality Distribution");
        }

        // Average relevance across all layers (per node)
        var avgRelevance = Enumerable.Range(0, ml.NodeCount)
            .Select(i => Enumerable.Range(0, ml.Layers.Count).Average(layerIdx => ml.LayerRelevance(i, layerIdx)))
            .ToArray();
        var (xAvgRel, yAvgRel) = ContinuousFrequency(avgRelevance);
        if (xAvgRel.Length > 0)
        {
            ChartGenerator.SaveDistributionPng(Path.Combine(outputDir, "loglog_avg_relevance.png"), xAvgRel, yAvgRel, "Average Relevance Across All Layers");
        }

        // Average exclusive relevance across all layers (per node)
        var avgExclusiveRelevance = Enumerable.Range(0, ml.NodeCount)
            .Select(i => Enumerable.Range(0, ml.Layers.Count).Average(layerIdx => ml.ExclusiveLayerRelevance(i, layerIdx)))
            .ToArray();
        var (xAvgExRel, yAvgExRel) = ContinuousFrequency(avgExclusiveRelevance);
        if (xAvgExRel.Length > 0)
        {
            ChartGenerator.SaveDistributionPng(Path.Combine(outputDir, "loglog_avg_exclusive_relevance.png"), xAvgExRel, yAvgExRel, "Average Exclusive Relevance Across All Layers");
        }

        Console.WriteLine("Advanced metrics calculation completed.");
        // ==========================================================================================

        int L = ml.Layers.Count;
        int N = ml.NodeCount;
        int edgeSharedMinLayers = edgeSharedMinLayersOpt ?? Math.Max(2, (int)Math.Ceiling(0.5 * L));

        // Use built-in method to analyze and select layers
        var selected = ml.SelectLayersByEdgeOverlap(minSharedFrac, edgeSharedMinLayers, uniqueMaxLayers, maxUniqueFrac, out var stats);

        string summaryCsv = Path.Combine(outputDir, "layer_overlap_summary.csv");
        using (var w = new StreamWriter(summaryCsv))
        {
            w.WriteLine("Layer,LayerName,Edges,SharedEdges,SharedFrac,UniqueEdges,UniqueFrac,Params(edgeSharedMinLayers,uniqueMaxLayers,minSharedFrac,maxUniqueFrac)");
            foreach (var s in stats)
            {
                string name = layerLabels.ContainsKey(s.Layer) ? layerLabels[s.Layer] : "Unknown";
                w.WriteLine(string.Join(',', new[]
                {
                    s.Layer.ToString(), name, s.Edges.ToString(), s.SharedEdges.ToString(), s.SharedFrac.ToString("F6"), s.UniqueEdges.ToString(), s.UniqueFrac.ToString("F6"),
                    $"({edgeSharedMinLayers},{uniqueMaxLayers},{minSharedFrac:F2},{maxUniqueFrac:F2})"
                }));
            }
        }
        Console.WriteLine($"Saved layer overlap summary to {summaryCsv}.");
        Console.WriteLine($"Selected layers: {string.Join(",", selected)}");
        File.WriteAllText(Path.Combine(outputDir, "selected_layers.txt"), string.Join(',', selected));

        // ---- Flatten selected layers (unweighted union) ----
        var flat = ml.FlattenLayers(selected, weighted: false);
        string flatCsv = Path.Combine(outputDir, "cviko7_flatten_unweighted_selected.csv");
        SaveEdgeListCsv(flat, flatCsv);
        Console.WriteLine($"Saved flattened edge list to {flatCsv}.");

        // ---- Community detection (Label Propagation) and modularity ----
        // Using minCommunitySize=3 to merge small communities and avoid fragmentation
        var result = ml.CommunitiesAndModularity(flat, maxIter: 100, seed: 42, minCommunitySize: 3);
        var labels = result.labels;
        double modularity = result.modularity;
        Console.WriteLine($"Communities: {result.communities.Count}, Modularity Q = {modularity:F6}");

        // Save outputs
        using (var w = new StreamWriter(Path.Combine(outputDir, "communities.csv")))
        {
            w.WriteLine("Node,NodeLabel,Community");
            for (int i = 0; i < labels.Length; i++) 
            {
                string label = nodeLabels.ContainsKey(i) ? nodeLabels[i] : i.ToString();
                w.WriteLine($"{i},{label},{labels[i]}");
            }
        }
        File.WriteAllText(Path.Combine(outputDir, "modularity.txt"), $"Q={modularity:F6}, communities={labels.Distinct().Count()}\n");

        // ---- Visualize the flattened network with communities ----
        Console.WriteLine("Generating network visualizations...");
        
        string positionsFile = Path.Combine(outputDir, "network_positions.csv");
        
        // Plot 1: Selected layers flattened network
        NetworkPlotting.PlotNetworkAuto(
            flat, 
            labels, 
            Path.Combine(outputDir, "network_selected_layers.png"),
            positionsFile,
            $"Network (Selected Layers) - Q={modularity:F4}",
            seed: 42
        );
        Console.WriteLine("Saved network_selected_layers.png");

        // Plot 2: Full network (all layers) - using same positions
        var labelsAll = ml.CommunitiesAndModularity(flatAllUnweighted, maxIter: 100, seed: 42, minCommunitySize: 3).labels;
        NetworkPlotting.PlotNetworkAuto(
            flatAllUnweighted,
            labelsAll,
            Path.Combine(outputDir, "network_all_layers.png"),
            positionsFile,
            "Network (All Layers)",
            seed: 42
        );
        Console.WriteLine("Saved network_all_layers.png");

        // Plot 3: Circular layout for comparison
        NetworkPlotting.PlotNetworkCircular(
            flat,
            labels,
            Path.Combine(outputDir, "network_circular.png"),
            $"Network Circular Layout - Q={modularity:F4}"
        );
        Console.WriteLine("Saved network_circular.png");

        // ---- Extra: top-2 layers by number of shared edges, flatten and community detection ----
        // "Shared" is defined using the same edgeSharedMinLayers threshold used above.
        var top2 = stats
            .OrderByDescending(s => s.SharedEdges)
            .ThenByDescending(s => s.SharedFrac)
            .Take(2)
            .Select(s => s.Layer)
            .ToList();

        if (top2.Count >= 1)
        {
            Console.WriteLine($"Top layers by shared edges: {string.Join(",", top2)}");
            var flatTop = ml.FlattenLayers(top2, weighted: false);
            string flatTopCsv = Path.Combine(outputDir, "cviko7_flatten_unweighted_top2_shared.csv");
            SaveEdgeListCsv(flatTop, flatTopCsv);
            Console.WriteLine($"Saved flattened edge list (top2 shared) to {flatTopCsv}.")
            ;

            var resTop = ml.CommunitiesAndModularity(flatTop, maxIter: 100, seed: 42, minCommunitySize: 3);
            Console.WriteLine($"[Top2] Communities: {resTop.communities.Count}, Modularity Q = {resTop.modularity:F6}");

            using (var w2 = new StreamWriter(Path.Combine(outputDir, "communities_top2.csv")))
            {
                w2.WriteLine("Node,NodeLabel,Community");
                for (int i = 0; i < resTop.labels.Length; i++) 
                {
                    string label = nodeLabels.ContainsKey(i) ? nodeLabels[i] : i.ToString();
                    w2.WriteLine($"{i},{label},{resTop.labels[i]}");
                }
            }
            File.WriteAllText(Path.Combine(outputDir, "modularity_top2.txt"), $"Q={resTop.modularity:F6}, communities={resTop.communities.Count}\n");

            // Visualize top-2 network (reusing same positions for consistency)
            NetworkPlotting.PlotNetworkAuto(
                flatTop,
                resTop.labels,
                Path.Combine(outputDir, "network_top2_shared.png"),
                Path.Combine(outputDir, "network_positions.csv"),  // Use same positions as other plots
                $"Network (Top 2 Shared Layers) - Q={resTop.modularity:F4}",
                seed: 42
            );
            Console.WriteLine("Saved network_top2_shared.png");

            // Also create circular layout version for top-2
            NetworkPlotting.PlotNetworkCircular(
                flatTop,
                resTop.labels,
                Path.Combine(outputDir, "network_top2_shared_circular.png"),
                $"Network (Top 2 Shared Layers) Circular - Q={resTop.modularity:F4}"
            );
            Console.WriteLine("Saved network_top2_shared_circular.png");
        }
    }

    private static void SaveEdgeListCsv(DokSparseMatrix<int> A, string path)
    {
        using var w = new StreamWriter(path);
        int n = A.Rows;
        w.WriteLine("Source,target,weight");
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                int wgt = A[i, j];
                if (wgt != 0)
                {
                    w.WriteLine($"{i},{j},{wgt}");
                }
            }
        }
    }

    private static Dictionary<int, (double lon, double lat)> LoadRawNodePositions(string nodesFile)
    {
        var positions = new Dictionary<int, (double lon, double lat)>();
        if (!File.Exists(nodesFile)) return positions;

        var lines = File.ReadAllLines(nodesFile).Skip(1);
        foreach (var line in lines)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue;
            int id = int.Parse(parts[0]) - 1;
            float x = float.Parse(parts[2], CultureInfo.InvariantCulture); // Longitude
            float y = float.Parse(parts[3], CultureInfo.InvariantCulture); // Latitude
            positions[id] = (x, y);
        }
        return positions;
    }

    private static void RunGeographicAnalysis(MultilayerNetwork ml, Dictionary<int, (double lon, double lat)> rawPositions, string outputDir, string positionsFile, Dictionary<int, string> layerLabels)
    {
        Console.WriteLine("Running geographic analysis...");

        // 1. Top 3 Airlines (Layers) Visualization
        var layerEdgeCounts = new List<(int LayerId, int EdgeCount)>();
        for (int i = 0; i < ml.Layers.Count; i++)
        {
            int edges = 0;
            int n = ml.NodeCount;
            for (int u = 0; u < n; u++)
            {
                for (int v = u + 1; v < n; v++)
                {
                    if (ml.Layers[i][u, v] != 0) edges++;
                }
            }
            layerEdgeCounts.Add((i, edges));
        }

        var top3 = layerEdgeCounts.OrderByDescending(x => x.EdgeCount).Take(3).ToList();
        Console.WriteLine($"Top 3 airlines by edge count: {string.Join(", ", top3.Select(x => $"L{x.LayerId} {(layerLabels.ContainsKey(x.LayerId) ? layerLabels[x.LayerId] : "")} ({x.EdgeCount} edges)"))}");

        foreach (var item in top3)
        {
            int layerId = item.LayerId;
            var layerMatrix = ml.Layers[layerId];

            // Detect communities for this layer for visualization coloring
            var res = ml.CommunitiesAndModularity(layerMatrix, maxIter: 50, seed: 42, minCommunitySize: 2);

            string plotPath = Path.Combine(outputDir, $"network_layer_{layerId}_top{top3.IndexOf(item) + 1}.png");
            string layerName = layerLabels.ContainsKey(layerId) ? layerLabels[layerId] : $"Layer {layerId}";
            NetworkPlotting.PlotNetworkAuto(
                layerMatrix,
                res.labels,
                plotPath,
                positionsFile,
                $"{layerName} ({item.EdgeCount} edges)",
                seed: 42
            );
            Console.WriteLine($"Saved plot for layer {layerId} to {plotPath}");
        }

        // 2. Geographic Edge Length Analysis
        Console.WriteLine("Calculating geographic edge lengths (Haversine)...");
        using (var w = new StreamWriter(Path.Combine(outputDir, "layer_distances.csv")))
        {
            w.WriteLine("LayerId,LayerName,EdgeCount,AvgDistanceKm,TotalDistanceKm,MaxDistanceKm");

            var layerStats = new List<(int Id, double AvgDist)>();

            for (int i = 0; i < ml.Layers.Count; i++)
            {
                var dists = new List<double>();
                int n = ml.NodeCount;
                for (int u = 0; u < n; u++)
                {
                    for (int v = u + 1; v < n; v++)
                    {
                        if (ml.Layers[i][u, v] != 0)
                        {
                            if (rawPositions.ContainsKey(u) && rawPositions.ContainsKey(v))
                            {
                                var p1 = rawPositions[u];
                                var p2 = rawPositions[v];
                                double km = Haversine(p1.lat, p1.lon, p2.lat, p2.lon);
                                dists.Add(km);
                            }
                        }
                    }
                }

                string name = layerLabels.ContainsKey(i) ? layerLabels[i] : "Unknown";
                if (dists.Count > 0)
                {
                    double avg = dists.Average();
                    double total = dists.Sum();
                    double max = dists.Max();
                    w.WriteLine($"{i},{name},{dists.Count},{avg:F2},{total:F2},{max:F2}");
                    layerStats.Add((i, avg));
                }
                else
                {
                    w.WriteLine($"{i},{name},0,0,0,0");
                }
            }

            var sortedByAvg = layerStats.OrderByDescending(x => x.AvgDist).ToList();
            if (sortedByAvg.Any())
            {
                Console.WriteLine("Longest avg flights:");
                foreach (var x in sortedByAvg.Take(3)) 
                {
                    string name = layerLabels.ContainsKey(x.Id) ? layerLabels[x.Id] : "";
                    Console.WriteLine($"  Layer {x.Id} {name}: {x.AvgDist:F2} km");
                }

                Console.WriteLine("Shortest avg flights:");
                foreach (var x in sortedByAvg.AsEnumerable().Reverse().Take(3)) 
                {
                    string name = layerLabels.ContainsKey(x.Id) ? layerLabels[x.Id] : "";
                    Console.WriteLine($"  Layer {x.Id} {name}: {x.AvgDist:F2} km");
                }
            }
        }
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRadians(double val) => (Math.PI / 180) * val;

    private static void RunAggregateAnalysis(MultilayerNetwork ml, string outputDir, string positionsFile, Dictionary<int, string> nodeLabels)
    {
        Console.WriteLine("Running aggregate network analysis (Hubs, Bridges, Communities)...");

        // 1. Weighted Flattening (Overlap Count)
        // w_ij = sum of edges across layers (since layers are unweighted, this is overlap count)
        var flatWeighted = ml.FlattenWeighted(bySum: false);

        // 2. Hubs (Weighted Degree Centrality)
        var degWeighted = MultilayerNetwork.DegreeCentrality(flatWeighted, weighted: true);
        var topHubs = degWeighted.Select((val, idx) => (idx, val))
                                 .OrderByDescending(x => x.val)
                                 .Take(10)
                                 .ToList();

        Console.WriteLine("Top 10 Hubs (Weighted Degree):");
        foreach (var h in topHubs) 
        {
            string label = nodeLabels.ContainsKey(h.idx) ? nodeLabels[h.idx] : "";
            Console.WriteLine($"  Node {h.idx} ({label}): {h.val}");
        }

        File.WriteAllLines(Path.Combine(outputDir, "top_hubs.txt"), topHubs.Select(h => 
        {
            string label = nodeLabels.ContainsKey(h.idx) ? nodeLabels[h.idx] : "";
            return $"Node {h.idx},{label},{h.val}";
        }));

        // 3. Bridges (Betweenness Centrality)
        // We use unweighted topology of the flattened graph for Betweenness to find structural bridges.
        Console.WriteLine("Calculating Betweenness Centrality...");
        var betweenness = CalculateBetweenness(flatWeighted, ml.NodeCount);
        var topBridges = betweenness.Select((val, idx) => (idx, val))
                                    .OrderByDescending(x => x.val)
                                    .Take(10)
                                    .ToList();

        Console.WriteLine("Top 10 Bridges (Betweenness):");
        foreach (var b in topBridges) 
        {
            string label = nodeLabels.ContainsKey(b.idx) ? nodeLabels[b.idx] : "";
            Console.WriteLine($"  Node {b.idx} ({label}): {b.val:F2}");
        }

        File.WriteAllLines(Path.Combine(outputDir, "top_bridges.txt"), topBridges.Select(b => 
        {
            string label = nodeLabels.ContainsKey(b.idx) ? nodeLabels[b.idx] : "";
            return $"Node {b.idx},{label},{b.val:F2}";
        }));

        // 4. Communities (Louvain/LabelPropagation on Weighted)
        Console.WriteLine("Detecting communities on weighted aggregate network...");
        var commRes = ml.CommunitiesAndModularity(flatWeighted, maxIter: 100, seed: 42, minCommunitySize: 3);
        Console.WriteLine($"Aggregate Communities: {commRes.communities.Count}, Modularity: {commRes.modularity:F4}");

        using (var w = new StreamWriter(Path.Combine(outputDir, "communities_aggregate.csv")))
        {
            w.WriteLine("Node,NodeLabel,Community");
            for (int i = 0; i < commRes.labels.Length; i++) 
            {
                string label = nodeLabels.ContainsKey(i) ? nodeLabels[i] : i.ToString();
                w.WriteLine($"{i},{label},{commRes.labels[i]}");
            }
        }

        // 5. Visualization
        string plotPath = Path.Combine(outputDir, "network_aggregate_weighted.png");
        NetworkPlotting.PlotNetworkAuto(
            flatWeighted,
            commRes.labels,
            plotPath,
            positionsFile,
            $"Aggregate Network (Weighted) - Q={commRes.modularity:F4}",
            seed: 42
        );
        Console.WriteLine($"Saved aggregate network plot to {plotPath}");
    }

    private static double[] CalculateBetweenness(DokSparseMatrix<int> adj, int n)
    {
        // Build adjacency list for speed
        var adjList = new List<int>[n];
        for (int i = 0; i < n; i++) adjList[i] = new List<int>();

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                if (adj[i, j] != 0)
                {
                    adjList[i].Add(j);
                    adjList[j].Add(i);
                }
            }
        }

        double[] cb = new double[n];

        // Brandes algorithm for unweighted graphs
        for (int s = 0; s < n; s++)
        {
            Stack<int> S = new Stack<int>();
            List<int>[] P = new List<int>[n];
            for (int i = 0; i < n; i++) P[i] = new List<int>();

            double[] sigma = new double[n];
            sigma[s] = 1;

            int[] d = new int[n];
            for (int i = 0; i < n; i++) d[i] = -1;
            d[s] = 0;

            Queue<int> Q = new Queue<int>();
            Q.Enqueue(s);

            while (Q.Count > 0)
            {
                int v = Q.Dequeue();
                S.Push(v);

                foreach (int w in adjList[v])
                {
                    // w found for the first time?
                    if (d[w] < 0)
                    {
                        Q.Enqueue(w);
                        d[w] = d[v] + 1;
                    }
                    // shortest path to w via v?
                    if (d[w] == d[v] + 1)
                    {
                        sigma[w] += sigma[v];
                        P[w].Add(v);
                    }
                }
            }

            double[] delta = new double[n];
            while (S.Count > 0)
            {
                int w = S.Pop();
                foreach (int v in P[w])
                {
                    delta[v] += (sigma[v] / sigma[w]) * (1 + delta[w]);
                }
                if (w != s)
                {
                    cb[w] += delta[w];
                }
            }
        }

        // For undirected graph, divide by 2
        for (int i = 0; i < n; i++) cb[i] /= 2.0;

        return cb;
    }

    private static void RunLayerComparisonAnalysis(MultilayerNetwork ml, string outputDir, Dictionary<int, string> layerLabels)
    {
        Console.WriteLine("Running layer comparison analysis (Structural & Jaccard)...");

        // 1. Structural Differences (Avg Degree, Clustering)
        var degrees = ml.GetDegreesPerLayer();
        var clusterings = ml.GetClusteringPerLayer();

        var layerMetrics = new List<(int Id, string Name, double AvgDeg, double AvgClust, int EdgeCount)>();

        using (var w = new StreamWriter(Path.Combine(outputDir, "layer_structural_metrics.csv")))
        {
            w.WriteLine("LayerId,LayerName,EdgeCount,ActiveNodes,AvgDegree,AvgClustering");

            for (int i = 0; i < ml.Layers.Count; i++)
            {
                long sumDeg = 0;
                int activeNodes = 0;
                foreach (var d in degrees[i])
                {
                    sumDeg += d;
                    if (d > 0) activeNodes++;
                }
                int edgeCount = (int)(sumDeg / 2);
                double avgDeg = (double)sumDeg / ml.NodeCount; // Avg degree over all nodes
                double avgClust = clusterings[i].Average();    // Avg clustering over all nodes

                string name = layerLabels.ContainsKey(i) ? layerLabels[i] : "";
                w.WriteLine($"{i},{name},{edgeCount},{activeNodes},{avgDeg:F4},{avgClust:F4}");

                layerMetrics.Add((i, name, avgDeg, avgClust, edgeCount));
            }
        }

        // Report Top Airlines by Clustering (Testing Hypothesis)
        Console.WriteLine("Top 5 Airlines by Clustering Coefficient (National carriers vs Low-cost?):");
        foreach (var item in layerMetrics.OrderByDescending(x => x.AvgClust).Take(5))
        {
            Console.WriteLine($"  {item.Name} (L{item.Id}): {item.AvgClust:F4} (Edges: {item.EdgeCount})");
        }

        // 2. Jaccard Similarity (Edge Overlap)
        // Precompute edge sets
        var layerEdges = new HashSet<(int, int)>[ml.Layers.Count];
        for (int i = 0; i < ml.Layers.Count; i++)
        {
            layerEdges[i] = new HashSet<(int, int)>();
            int n = ml.NodeCount;
            for (int u = 0; u < n; u++)
            {
                for (int v = u + 1; v < n; v++)
                {
                    if (ml.Layers[i][u, v] != 0)
                    {
                        layerEdges[i].Add((u, v));
                    }
                }
            }
        }

        var similarities = new List<(int L1, string N1, int L2, string N2, double J)>();

        using (var w = new StreamWriter(Path.Combine(outputDir, "layer_jaccard_similarity.csv")))
        {
            w.WriteLine("Layer1,Name1,Layer2,Name2,Intersection,Union,Jaccard");

            for (int i = 0; i < ml.Layers.Count; i++)
            {
                for (int j = i + 1; j < ml.Layers.Count; j++)
                {
                    var set1 = layerEdges[i];
                    var set2 = layerEdges[j];

                    int intersection = 0;
                    foreach (var e in set1)
                    {
                        if (set2.Contains(e)) intersection++;
                    }

                    int union = set1.Count + set2.Count - intersection;
                    double jaccard = union > 0 ? (double)intersection / union : 0;

                    string n1 = layerLabels.ContainsKey(i) ? layerLabels[i] : "";
                    string n2 = layerLabels.ContainsKey(j) ? layerLabels[j] : "";

                    w.WriteLine($"{i},{n1},{j},{n2},{intersection},{union},{jaccard:F6}");
                    similarities.Add((i, n1, j, n2, jaccard));
                }
            }
        }

        Console.WriteLine("Top 10 Airline Pairs by Jaccard Similarity (Direct Competition):");
        foreach (var s in similarities.OrderByDescending(x => x.J).Take(10))
        {
            Console.WriteLine($"  {s.N1} vs {s.N2}: {s.J:F4}");
        }
    }
}
