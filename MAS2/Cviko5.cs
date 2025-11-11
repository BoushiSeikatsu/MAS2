using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using MAS2;
//Potřeba je kouknout na ty metriky!
namespace MAS2
{
    /*public class Cviko5
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

            // Multilayer set-based measures across all layers (unweighted): degree deviation, connective redundancy
            var allLayersIdx = Enumerable.Range(0, ml.Layers.Count);
            var degreeDeviation = new double[ml.NodeCount];
            var connectiveRedundancy = new double[ml.NodeCount];
            for (int i = 0; i < ml.NodeCount; i++)
            {
                degreeDeviation[i] = ml.DegreeDeviation(i, allLayersIdx);
                connectiveRedundancy[i] = ml.ConnectiveRedundancy(i, allLayersIdx);
            }

            // Exclusive neighborhood count: neighbors connected via edges present in exactly one layer
            var exclLayerOnly = MultilayerNetwork.ExclusiveNeighborhoodCountFromLayerCountFlatten(flatLayerCount);

            // Save flattened (unweighted) measures per node
            string outFlatUnw = "flattened_measures_unweighted.csv";
            using (var w = new StreamWriter(outFlatUnw))
            {
                w.WriteLine("Node,DegreeCentrality,DegreeDeviation,CountOfNeighbors,NeighborhoodCentrality,ConnectiveRedundancy,ExclusiveNeighborhood,AvgShortestPath");
                for (int i = 0; i < ml.NodeCount; i++)
                {
                    w.WriteLine(string.Join(',', new[]
                    {
                        i.ToString(),
                        degUnw[i].ToString(),
                        (double.IsNaN(degreeDeviation[i]) ? "" : degreeDeviation[i].ToString("F6", CultureInfo.InvariantCulture)),
                        neighUnw[i].ToString(),
                        neighUnw[i].ToString(),
                        connectiveRedundancy[i].ToString(CultureInfo.InvariantCulture),
                        exclLayerOnly[i].ToString(),
                        (double.IsNaN(avgDistUnw[i]) ? "" : avgDistUnw[i].ToString("F6", CultureInfo.InvariantCulture))
                    }));
                }
            }
            Console.WriteLine($"Saved: {outFlatUnw}");

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

            // Unweighted degree distribution
            var (xDegUnw, yDegUnw) = DiscreteFrequency(degUnw);
            ChartGenerator.SaveDistributionPng("loglog_degree_unweighted.png", xDegUnw, yDegUnw, "Degree centrality (unweighted flatten)");

            // Degree deviation distribution (across layers)
            var (xDD, yDD) = ContinuousFrequency(degreeDeviation);
            if (xDD.Length > 0)
                ChartGenerator.SaveDistributionPng("loglog_degree_deviation.png", xDD, yDD, "Degree deviation (across layers)");

            // Connective redundancy distribution (across layers)
            var (xCr, yCr) = ContinuousFrequency(connectiveRedundancy);
            if (xCr.Length > 0)
                ChartGenerator.SaveDistributionPng("loglog_connective_redundancy.png", xCr, yCr, "Connective redundancy (across layers)");

            // Average distance distributions
            var (xDu, yDu) = ContinuousFrequency(avgDistUnw);
            if (xDu.Length > 0)
                ChartGenerator.SaveDistributionPng("loglog_avg_distance_unweighted.png", xDu, yDu, "Avg shortest path (unweighted)");

            // Neighbor count and neighborhood centrality (same on flattened unweighted)
            var (xNeigh, yNeigh) = DiscreteFrequency(neighUnw);
            ChartGenerator.SaveDistributionPng("loglog_neighbor_count_unweighted.png", xNeigh, yNeigh, "Neighbor count (unweighted flatten)");
            ChartGenerator.SaveDistributionPng("loglog_neighborhood_centrality_unweighted.png", xNeigh, yNeigh, "Neighborhood centrality (unweighted flatten)");

            // Exclusive neighbors distribution (layer unique edges)
            var (xExcl, yExcl) = DiscreteFrequency(exclLayerOnly);
            ChartGenerator.SaveDistributionPng("loglog_exclusive_neighbors.png", xExcl, yExcl, "Exclusive neighbors (edges in exactly one layer)");

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
            SaveSingleColumn("degree_centrality_unweighted.csv", "DegreeCentrality", degUnw.Select(v => v.ToString()));

            // Degree Deviation (across layers)
            SaveSingleColumn("degree_deviation.csv", "DegreeDeviation", degreeDeviation.Select(v => double.IsNaN(v) ? "" : v.ToString("F6", CultureInfo.InvariantCulture)));

            // Count of Neighbors
            SaveSingleColumn("count_of_neighbors_unweighted.csv", "CountOfNeighbors", neighUnw.Select(v => v.ToString()));

            // Neighborhood Centrality (same as neighbor count in unweighted flatten)
            SaveSingleColumn("neighborhood_centrality_unweighted.csv", "NeighborhoodCentrality", neighUnw.Select(v => v.ToString()));

            // Connective Redundancy (across layers)
            SaveSingleColumn("connective_redundancy.csv", "ConnectiveRedundancy", connectiveRedundancy.Select(v => v.ToString(CultureInfo.InvariantCulture)));

            // Exclusive Neighborhood size (neighbors via exactly one layer)
            SaveSingleColumn("exclusive_neighborhood.csv", "ExclusiveNeighborhood", exclLayerOnly.Select(v => v.ToString()));

            // Average Shortest Path (unweighted)
            SaveSingleColumn("average_shortest_path_unweighted.csv", "AvgShortestPath", avgDistUnw.Select(v => double.IsNaN(v) ? "" : v.ToString("F6", CultureInfo.InvariantCulture)));
        }
    }*/
}