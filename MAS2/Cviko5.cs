using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MAS2;

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
        }
    }
}