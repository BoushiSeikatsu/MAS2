using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MAS2;

namespace MAS2
{
    /*public class Cviko6
    {
        // Usage: Cviko6 <layerFile1> <layerFile2> <layerFile3> ...
        // If no args provided, looks for default files in bin/Debug/net8.0.
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
                Console.WriteLine("No layer files found. Provide file paths as arguments or ensure defaults exist.");
                return;
            }

            Console.WriteLine($"Loading {existing.Count} layer files (multilayer edge format)...");
            var ml = MultilayerNetwork.LoadFromMultilayerFiles(existing, ' ', true);
            Console.WriteLine($"Loaded multilayer network with {ml.Layers.Count} layers and {ml.NodeCount} nodes.");

            // Compute relevance and exclusive relevance per node per layer
            int n = ml.NodeCount;
            int Lc = ml.Layers.Count;

            // Occupation centrality via random walk
            Console.WriteLine("Running random walk for occupation centrality (200k steps)...");
            var occ = ml.OccupationCentrality(steps: 200_000, startNode: null, layerWeights: null, seed: 12345);

            // Prepare CSV output
            string outPath = "multilayer_advanced_measures.csv";
            using (var w = new StreamWriter(outPath))
            {
                var header = new List<string> { "Node", "OccCentrality" };
                for (int L = 0; L < Lc; L++)
                {
                    header.Add($"Deg_L{L}");
                    header.Add($"Rel_L{L}");
                    header.Add($"ExRel_L{L}");
                }
                w.WriteLine(string.Join(',', header));

                // Precompute degrees per layer for convenience
                var degPerLayer = ml.GetDegreesPerLayer();

                for (int i = 0; i < n; i++)
                {
                    var parts = new List<string> { i.ToString(), occ[i].ToString("F6") };
                    for (int L = 0; L < Lc; L++)
                    {
                        double rel = ml.LayerRelevance(i, L);
                        double exRel = ml.ExclusiveLayerRelevance(i, L);
                        parts.Add(degPerLayer[L][i].ToString());
                        parts.Add(rel.ToString("F6"));
                        parts.Add(exRel.ToString("F6"));
                    }
                    w.WriteLine(string.Join(',', parts));
                }
            }
            Console.WriteLine($"Advanced measures saved to: {outPath}");

            // Apply flattening (unweighted and weighted) and save as edge lists
            Console.WriteLine("Flattening network (unweighted union)...");
            var flatU = ml.FlattenUnweighted();
            var flatUPath = "flatten_unweighted.csv";
            NetworkGenerator.SaveMatrixAsEdgeList(flatU, flatUPath);
            Console.WriteLine($"Unweighted flattened graph saved to: {flatUPath}");

            Console.WriteLine("Flattening network (weighted sum of layers)...");
            var flatW = ml.FlattenWeighted(bySum: true);
            var flatWPath = "flatten_weighted.csv";
            NetworkGenerator.SaveMatrixAsEdgeList(flatW, flatWPath);
            Console.WriteLine($"Weighted flattened graph saved to: {flatWPath}");

            // Print short sample summary
            int printN = Math.Min(10, n);
            Console.WriteLine("Sample nodes (OccCentrality and relevance on first layer):");
            for (int i = 0; i < printN; i++)
            {
                double rel0 = Lc > 0 ? ml.LayerRelevance(i, 0) : 0.0;
                double ex0 = Lc > 0 ? ml.ExclusiveLayerRelevance(i, 0) : 0.0;
                Console.WriteLine($"Node {i}: occ={occ[i]:F4}, relL0={rel0:F4}, exRelL0={ex0:F4}");
            }
        }
    }*/
}