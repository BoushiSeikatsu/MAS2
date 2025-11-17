using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using MAS2;
//Potřeba je kouknout na ty metriky!
namespace MAS2
{
    public class Cviko5
    {
        // Usage: Cviko5 <layerFile1> <layerFile2> <layerFile3> ...
        // If no args provided, looks for layer1.csv, layer2.csv, layer3.csv in working directory.
        public static void Main(string[] args)
        {
            var defaultFiles = new List<string>
            {
                Path.Combine("bin", "Debug", "net8.0", "Kaktovi.edges"),
                Path.Combine("bin", "Debug", "net8.0", "Venetie.edges"),
                Path.Combine("bin", "Debug", "net8.0", "Wainwright.edges")
            };

            var layerFiles = args != null && args.Length > 0 ? args.ToList() : defaultFiles;

            // Filter only existing files
            var existing = layerFiles.Where(f => File.Exists(f)).ToList();
            if (existing.Count == 0)
            {
                Console.WriteLine("No layer files found. Provide file paths as arguments or place layer1.csv, layer2.csv, layer3.csv in the working directory.");
                return;
            }

            Console.WriteLine($"Loading {existing.Count} layer files (multilayer edge format)...");
            var ml = MultilayerNetwork.LoadFromMultilayerFiles(existing, ' ', true);
            Console.WriteLine($"Loaded multilayer network with {ml.Layers.Count} layers and {ml.NodeCount} nodes.");

            // Compute measures
            var degreesPerLayer = ml.GetDegreesPerLayer();
            var clusteringPerLayer = ml.GetClusteringPerLayer();
            var aggregateDegree = ml.GetAggregateDegree();
            var multiplexDegree = ml.GetMultiplexDegree();
            var aggregateMatrix = ml.GetAggregateMatrix();

            // Prepare CSV output
            string outPath = "multilayer_node_measures.csv";
            using (var w = new StreamWriter(outPath))
            {
                // Header
                var headerParts = new List<string> { "Node", "AggregateDegree", "MultiplexDegree", "AvgClustering" };
                for (int L = 0; L < ml.Layers.Count; L++)
                {
                    headerParts.Add($"Deg_L{L}");
                    headerParts.Add($"Clust_L{L}");
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
                        avgClust.ToString("F6")
                    };

                    for (int L = 0; L < ml.Layers.Count; L++)
                    {
                        parts.Add(degreesPerLayer[L][i].ToString());
                        parts.Add(clusteringPerLayer[L][i].ToString("F6"));
                    }

                    w.WriteLine(string.Join(',', parts));
                }
            }

            Console.WriteLine($"Per-node measures saved to: {outPath}");

            // Print short summary for first 10 nodes
            int printN = Math.Min(10, ml.NodeCount);
            Console.WriteLine("Sample node measures:");
            for (int i = 0; i < printN; i++)
            {
                Console.WriteLine($"Node {i}: aggDeg={aggregateDegree[i]}, muxDeg={multiplexDegree[i]}, avgClust={clusteringPerLayer.Select(a => a[i]).Average():F4}");
            }

            // --------- NEW: Flatten (unweighted and weighted) and compute measures on flattened networks ---------

            Console.WriteLine("Flattening multilayer network (unweighted union)...");
            var flatUnw = ml.FlattenUnweighted();
            NetworkGenerator.SaveMatrixAsEdgeList(flatUnw, "flatten_unweighted.csv");

            // Additionally build a layer-count multiplicity flatten internally for exclusive-neighborhood calculation
            var flatLayerCount = ml.FlattenWeighted(bySum: false);

            // Measures on unweighted flatten
            var degUnw = MultilayerNetwork.DegreeCentrality(flatUnw, weighted: false); // Degree Centrality
            var neighUnw = MultilayerNetwork.NeighborCounts(flatUnw);                  // Count of Neighbors
            var avgDistUnw = MultilayerNetwork.AverageShortestPathUnweighted(flatUnw); // Distance (avg shortest path)

            // Multilayer set-based measures across all layers (unweighted): degree deviation, connective redundancy, exclusive neighborhood
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

            // Save flattened (unweighted) measures per node
            string outFlatUnw = "flattened_measures_unweighted.csv";
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
                        connectiveRedundancy[i].ToString(CultureInfo.InvariantCulture),
                        exclusiveNeighborhood[i].ToString(),
                        (double.IsNaN(avgDistUnw[i]) ? "" : avgDistUnw[i].ToString("F6", CultureInfo.InvariantCulture))
                    }));
                }
            }
            Console.WriteLine($"Saved: {outFlatUnw}");

            // --------- WEIGHTED FLATTENING: Reveal multi-layer redundancy ---------
            Console.WriteLine("\nFlattening multilayer network (weighted by layer multiplicity)...");
            var flatWeighted = ml.FlattenWeighted(bySum: false); // bySum=false uses layer-count as weight
            NetworkGenerator.SaveMatrixAsEdgeList(flatWeighted, "flatten_weighted.csv");

            // Measures on weighted flatten
            var degWeighted = MultilayerNetwork.DegreeCentrality(flatWeighted, weighted: true);  // Weighted degree (sum of multiplicities)
            var neighWeighted = MultilayerNetwork.NeighborCounts(flatWeighted);                  // Unique neighbors (same as unweighted)
            var connRedWeighted = MultilayerNetwork.ConnectiveRedundancy(flatWeighted, weighted: true); // NOW meaningful!
            var avgDistWeighted = MultilayerNetwork.AverageShortestPathWeightedByInverseMultiplicity(flatWeighted);

            // Save weighted measures
            string outFlatWeighted = "flattened_measures_weighted.csv";
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

            // --------- Plot log-log distributions for key measures ---------

            // Helper local function to compute discrete frequency distribution (value -> count) for integer arrays
            static (double[] xs, double[] ys) DiscreteFrequency(int[] values)
            {
                var groups = values.GroupBy(v => v)
                                   .OrderBy(g => g.Key)
                                   .ToArray();
                var xs = groups.Select(g => (double)g.Key).ToArray();
                var ys = groups.Select(g => (double)g.Count()).ToArray();
                return (xs, ys);
            }

            // Helper for double arrays: bin identical values and drop non-positive x for log scaling
            static (double[] xs, double[] ys) ContinuousFrequency(double[] values)
            {
                // Round to 6 decimals to group almost-equal values
                var groups = values.Where(v => !double.IsNaN(v) && v > 0)
                                   .Select(v => Math.Round(v, 6))
                                   .GroupBy(v => v)
                                   .OrderBy(g => g.Key)
                                   .ToArray();
                var xs = groups.Select(g => (double)g.Key).ToArray();
                var ys = groups.Select(g => (double)g.Count()).ToArray();
                return (xs, ys);
            }

            // --------- Charts: Keep unweighted-only measures, use weighted for measures with both variants ---------
            
            // Degree deviation distribution (multilayer-only, no weighted variant)
            var (xDD, yDD) = ContinuousFrequency(degreeDeviation);
            if (xDD.Length > 0)
                ChartGenerator.SaveDistributionPng("loglog_degree_deviation.png", xDD, yDD, "Degree deviation (across layers)");

            // Neighborhood centrality distribution (same for weighted/unweighted)
            var (xNeigh, yNeigh) = DiscreteFrequency(neighUnw);
            ChartGenerator.SaveDistributionPng("loglog_neighborhood.png", xNeigh, yNeigh, "Neighborhood");

            // Exclusive neighborhood distribution (multilayer-only, no weighted variant)
            var (xExcl, yExcl) = DiscreteFrequency(exclusiveNeighborhood);
            ChartGenerator.SaveDistributionPng("loglog_xneighborhood.png", xExcl, yExcl, "XNeighborhood (neighbors exclusive to selected layers)");

            // --------- Weighted flattening charts (replaces unweighted degree, connective redundancy, distance) ---------
            var (xDegW, yDegW) = DiscreteFrequency(degWeighted);
            ChartGenerator.SaveDistributionPng("loglog_degree.png", xDegW, yDegW, "Degree (weighted by layer multiplicity)");

            var (xCrW, yCrW) = ContinuousFrequency(connRedWeighted);
            if (xCrW.Length > 0)
                ChartGenerator.SaveDistributionPng("loglog_connective_redundancy.png", xCrW, yCrW, "Connective redundancy (weighted)");

            var (xDw, yDw) = ContinuousFrequency(avgDistWeighted);
            if (xDw.Length > 0)
                ChartGenerator.SaveDistributionPng("loglog_avg_distance.png", xDw, yDw, "Avg shortest path (weighted by 1/multiplicity)");


            // --------- Save each measure into its own CSV ---------
            void SaveSingleColumn(string path, string header, IEnumerable<string> lines)
            {
                using var sw = new StreamWriter(path);
                sw.WriteLine($"Node,{header}");
                int idx = 0;
                foreach (var val in lines)
                {
                    sw.WriteLine($"{idx},{val}");
                    idx++;
                }
            }

            // Degree Centrality
            SaveSingleColumn("degree.csv", "Degree", degUnw.Select(v => v.ToString()));

            // Degree Deviation (across layers)
            SaveSingleColumn("degree_deviation.csv", "DegreeDeviation", degreeDeviation.Select(v => double.IsNaN(v) ? "" : v.ToString("F6", CultureInfo.InvariantCulture)));

            // Neighborhood Centrality
            SaveSingleColumn("neighborhood.csv", "Neighborhood", neighUnw.Select(v => v.ToString()));

            // Connective Redundancy (across layers)
            SaveSingleColumn("connective_redundancy.csv", "ConnectiveRedundancy", connectiveRedundancy.Select(v => v.ToString(CultureInfo.InvariantCulture)));

            // Exclusive Neighborhood size (neighbors in L but not in complement of L)
            SaveSingleColumn("xneighborhood.csv", "XNeighborhood", exclusiveNeighborhood.Select(v => v.ToString()));

            // Average Shortest Path (unweighted)
            SaveSingleColumn("average_shortest_path_unweighted.csv", "AvgShortestPath", avgDistUnw.Select(v => double.IsNaN(v) ? "" : v.ToString("F6", CultureInfo.InvariantCulture)));

            // --------- Weighted flattening individual CSVs ---------
            SaveSingleColumn("degree_weighted.csv", "DegreeWeighted", degWeighted.Select(v => v.ToString()));
            SaveSingleColumn("connective_redundancy_weighted.csv", "ConnectiveRedundancyWeighted", connRedWeighted.Select(v => v.ToString("F6", CultureInfo.InvariantCulture)));
            SaveSingleColumn("average_shortest_path_weighted.csv", "AvgShortestPathWeighted", avgDistWeighted.Select(v => double.IsNaN(v) ? "" : v.ToString("F6", CultureInfo.InvariantCulture)));

            // --------- ADDITIONAL EXPERIMENT: Compute measures for 2 random layers ---------
            Console.WriteLine("\n--- Additional Experiment: Analyzing 2 Random Layers ---");
            var random = new Random(42); // Fixed seed for reproducibility
            var selectedLayers = Enumerable.Range(0, ml.Layers.Count).OrderBy(x => random.Next()).Take(2).ToList();
            Console.WriteLine($"Selected layers: {string.Join(", ", selectedLayers)}");

            // Flatten the 2 selected layers only
            var twoLayersFlatUnw = ml.FlattenUnweighted(selectedLayers);
            var twoLayersFlatLayerCount = ml.FlattenWeighted(selectedLayers, bySum: false);

            // Compute measures on the 2-layer flatten
            var deg2L = MultilayerNetwork.DegreeCentrality(twoLayersFlatUnw, weighted: false);
            var neigh2L = MultilayerNetwork.NeighborCounts(twoLayersFlatUnw);
            var avgDist2L = MultilayerNetwork.AverageShortestPathUnweighted(twoLayersFlatUnw);

            // Multilayer measures for the 2 selected layers
            var degDev2L = new double[ml.NodeCount];
            var connRed2L = new double[ml.NodeCount];
            var exclNeigh2L = new int[ml.NodeCount];
            for (int i = 0; i < ml.NodeCount; i++)
            {
                degDev2L[i] = ml.DegreeDeviation(i, selectedLayers);
                connRed2L[i] = ml.ConnectiveRedundancy(i, selectedLayers);
                exclNeigh2L[i] = ml.ExclusiveNeighborhood(i, selectedLayers).Count;
            }

            // Save CSV for 2-layer experiment
            string out2Layers = "flattened_measures_2layers.csv";
            using (var w = new StreamWriter(out2Layers))
            {
                w.WriteLine("Node,Degree,DegreeDeviation,Neighborhood,ConnectiveRedundancy,XNeighborhood,AvgShortestPath");
                for (int i = 0; i < ml.NodeCount; i++)
                {
                    w.WriteLine(string.Join(',', new[]
                    {
                        i.ToString(),
                        deg2L[i].ToString(),
                        (double.IsNaN(degDev2L[i]) ? "" : degDev2L[i].ToString("F6", CultureInfo.InvariantCulture)),
                        neigh2L[i].ToString(),
                        connRed2L[i].ToString(CultureInfo.InvariantCulture),
                        exclNeigh2L[i].ToString(),
                        (double.IsNaN(avgDist2L[i]) ? "" : avgDist2L[i].ToString("F6", CultureInfo.InvariantCulture))
                    }));
                }
            }
            Console.WriteLine($"Saved: {out2Layers}");

            // Generate charts for 2-layer experiment (unweighted-only measures)
            var (xDD2L, yDD2L) = ContinuousFrequency(degDev2L);
            if (xDD2L.Length > 0)
                ChartGenerator.SaveDistributionPng("loglog_degree_deviation_2layers.png", xDD2L, yDD2L, "Degree deviation (2 layers)");

            var (xNeigh2L, yNeigh2L) = DiscreteFrequency(neigh2L);
            ChartGenerator.SaveDistributionPng("loglog_neighborhood_2layers.png", xNeigh2L, yNeigh2L, "Neighborhood (2 random layers)");

            var (xExcl2L, yExcl2L) = DiscreteFrequency(exclNeigh2L);
            ChartGenerator.SaveDistributionPng("loglog_xneighborhood_2layers.png", xExcl2L, yExcl2L, "XNeighborhood (2 random layers)");

            // Save individual CSV files for 2-layer experiment
            SaveSingleColumn("degree_2layers.csv", "Degree", deg2L.Select(v => v.ToString()));
            SaveSingleColumn("degree_deviation_2layers.csv", "DegreeDeviation", degDev2L.Select(v => double.IsNaN(v) ? "" : v.ToString("F6", CultureInfo.InvariantCulture)));
            SaveSingleColumn("neighborhood_2layers.csv", "Neighborhood", neigh2L.Select(v => v.ToString()));
            SaveSingleColumn("connective_redundancy_2layers.csv", "ConnectiveRedundancy", connRed2L.Select(v => v.ToString(CultureInfo.InvariantCulture)));
            SaveSingleColumn("xneighborhood_2layers.csv", "XNeighborhood", exclNeigh2L.Select(v => v.ToString()));
            SaveSingleColumn("average_shortest_path_2layers.csv", "AvgShortestPath", avgDist2L.Select(v => double.IsNaN(v) ? "" : v.ToString("F6", CultureInfo.InvariantCulture)));

            Console.WriteLine("2-layer experiment completed. Charts and CSVs saved with '_2layers' suffix.");

            // --------- WEIGHTED 2-layer experiment ---------
            Console.WriteLine("\n--- Weighted Analysis of 2 Random Layers ---");
            var twoLayersFlatWeighted = ml.FlattenWeighted(selectedLayers, bySum: false);

            var deg2LW = MultilayerNetwork.DegreeCentrality(twoLayersFlatWeighted, weighted: true);
            var neigh2LW = MultilayerNetwork.NeighborCounts(twoLayersFlatWeighted);
            var connRed2LW = MultilayerNetwork.ConnectiveRedundancy(twoLayersFlatWeighted, weighted: true);
            var avgDist2LW = MultilayerNetwork.AverageShortestPathWeightedByInverseMultiplicity(twoLayersFlatWeighted);

            string out2LayersW = "flattened_measures_2layers_weighted.csv";
            using (var w = new StreamWriter(out2LayersW))
            {
                w.WriteLine("Node,DegreeWeighted,Neighborhood,ConnectiveRedundancyWeighted,AvgShortestPathWeighted");
                for (int i = 0; i < ml.NodeCount; i++)
                {
                    w.WriteLine(string.Join(',', new[]
                    {
                        i.ToString(),
                        deg2LW[i].ToString(),
                        neigh2LW[i].ToString(),
                        connRed2LW[i].ToString("F6", CultureInfo.InvariantCulture),
                        (double.IsNaN(avgDist2LW[i]) ? "" : avgDist2LW[i].ToString("F6", CultureInfo.InvariantCulture))
                    }));
                }
            }
            Console.WriteLine($"Saved: {out2LayersW}");

            // Weighted 2-layer charts (replacing unweighted versions for these measures)
            var (xDeg2LW, yDeg2LW) = DiscreteFrequency(deg2LW);
            ChartGenerator.SaveDistributionPng("loglog_degree_2layers.png", xDeg2LW, yDeg2LW, "Degree weighted (2 random layers)");

            var (xCr2LW, yCr2LW) = ContinuousFrequency(connRed2LW);
            if (xCr2LW.Length > 0)
                ChartGenerator.SaveDistributionPng("loglog_connective_redundancy_2layers.png", xCr2LW, yCr2LW, "Connective redundancy weighted (2 layers)");

            var (xDu2LW, yDu2LW) = ContinuousFrequency(avgDist2LW);
            if (xDu2LW.Length > 0)
                ChartGenerator.SaveDistributionPng("loglog_avg_distance_2layers.png", xDu2LW, yDu2LW, "Avg shortest path weighted (2 layers)");

            SaveSingleColumn("degree_2layers_weighted.csv", "DegreeWeighted", deg2LW.Select(v => v.ToString()));
            SaveSingleColumn("connective_redundancy_2layers_weighted.csv", "ConnectiveRedundancyWeighted", connRed2LW.Select(v => v.ToString("F6", CultureInfo.InvariantCulture)));
            SaveSingleColumn("average_shortest_path_2layers_weighted.csv", "AvgShortestPathWeighted", avgDist2LW.Select(v => double.IsNaN(v) ? "" : v.ToString("F6", CultureInfo.InvariantCulture)));

            Console.WriteLine("Weighted 2-layer experiment completed.");
        }
    }
}