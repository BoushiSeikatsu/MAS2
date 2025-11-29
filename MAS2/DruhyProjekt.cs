//https://github.com/CoMuNeLab/EU-Air-Transportation-Multiplex
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using MAS2;

public class DruhyProjekt
{
    /*public static void Main(string[] args)
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
        RunAnalysis(ml, args, outputDir);
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
        float scaleX = (width - 2 * padding) / rangeX;
        float scaleY = (height - 2 * padding) / rangeY;
        
        // Keep aspect ratio? Maybe not strictly necessary but looks better. 
        // Let's stretch to fill for now to maximize visibility.
        
        foreach (var pos in positions.Values)
        {
            pos.X = padding + (pos.X - minX) * scaleX;
            // Flip Y because screen coordinates (0,0) is top-left, but latitude increases upwards
            pos.Y = height - (padding + (pos.Y - minY) * scaleY); 
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

    private static void RunAnalysis(MultilayerNetwork ml, string[] args, string outputDir)
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
            w.WriteLine("Layer,Edges,SharedEdges,SharedFrac,UniqueEdges,UniqueFrac,Params(edgeSharedMinLayers,uniqueMaxLayers,minSharedFrac,maxUniqueFrac)");
            foreach (var s in stats)
            {
                w.WriteLine(string.Join(',', new[]
                {
                    s.Layer.ToString(), s.Edges.ToString(), s.SharedEdges.ToString(), s.SharedFrac.ToString("F6"), s.UniqueEdges.ToString(), s.UniqueFrac.ToString("F6"),
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
            w.WriteLine("Node,Community");
            for (int i = 0; i < labels.Length; i++) w.WriteLine($"{i},{labels[i]}");
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
                w2.WriteLine("Node,Community");
                for (int i = 0; i < resTop.labels.Length; i++) w2.WriteLine($"{i},{resTop.labels[i]}");
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
    }*/
}
