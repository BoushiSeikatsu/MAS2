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

            // Create output directory if it doesn't exist
            string outputDir = "data";
            Directory.CreateDirectory(outputDir);
            Console.WriteLine($"Output directory: {outputDir}/");

            // Compute relevance and exclusive relevance per node per layer
            int n = ml.NodeCount;
            int Lc = ml.Layers.Count;

            // Occupation centrality via random walk
            Console.WriteLine("Running random walk for occupation centrality (200k steps)...");
            var occ = ml.OccupationCentrality(steps: 200_000, startNode: null, layerWeights: null, seed: 12345);

            // Prepare CSV output
            string outPath = Path.Combine(outputDir, "multilayer_advanced_measures.csv");
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
            var flatUPath = Path.Combine(outputDir, "flatten_unweighted.csv");
            NetworkGenerator.SaveMatrixAsEdgeList(flatU, flatUPath);
            Console.WriteLine($"Unweighted flattened graph saved to: {flatUPath}");

            Console.WriteLine("Flattening network (weighted sum of layers)...");
            var flatW = ml.FlattenWeighted(bySum: true);
            var flatWPath = Path.Combine(outputDir, "flatten_weighted.csv");
            NetworkGenerator.SaveMatrixAsEdgeList(flatW, flatWPath);
            Console.WriteLine($"Weighted flattened graph saved to: {flatWPath}");

            // --------- Generate comprehensive comparison tables ---------
            Console.WriteLine("\nGenerating comparison tables...");
            RelevanceTableGenerator.GenerateComparisonTable(ml, occ, Path.Combine(outputDir, "relevance_comparison"));

            // --------- Generate comparative tables for relevance measures ---------
            Console.WriteLine("\n=== TOP NODES BY OCCUPATION CENTRALITY ===");
            var topByOcc = occ.Select((val, idx) => (Node: idx, OccCentrality: val))
                              .OrderByDescending(x => x.OccCentrality)
                              .Take(10)
                              .ToList();
            Console.WriteLine("Rank\tNode\tOccupation Centrality");
            for (int rank = 0; rank < topByOcc.Count; rank++)
            {
                Console.WriteLine($"{rank + 1}\t{topByOcc[rank].Node}\t{topByOcc[rank].OccCentrality:F6}");
            }

            // Top nodes by relevance per layer
            for (int L = 0; L < Lc; L++)
            {
                Console.WriteLine($"\n=== TOP NODES BY RELEVANCE ON LAYER {L} ===");
                var topByRel = Enumerable.Range(0, n)
                    .Select(i => (Node: i, Relevance: ml.LayerRelevance(i, L)))
                    .OrderByDescending(x => x.Relevance)
                    .Take(10)
                    .ToList();
                Console.WriteLine("Rank\tNode\tRelevance");
                for (int rank = 0; rank < topByRel.Count; rank++)
                {
                    Console.WriteLine($"{rank + 1}\t{topByRel[rank].Node}\t{topByRel[rank].Relevance:F6}");
                }
            }

            // Top nodes by exclusive relevance per layer
            for (int L = 0; L < Lc; L++)
            {
                Console.WriteLine($"\n=== TOP NODES BY EXCLUSIVE RELEVANCE ON LAYER {L} ===");
                var topByExRel = Enumerable.Range(0, n)
                    .Select(i => (Node: i, ExclusiveRelevance: ml.ExclusiveLayerRelevance(i, L)))
                    .OrderByDescending(x => x.ExclusiveRelevance)
                    .Take(10)
                    .ToList();
                Console.WriteLine("Rank\tNode\tExclusive Relevance");
                for (int rank = 0; rank < topByExRel.Count; rank++)
                {
                    Console.WriteLine($"{rank + 1}\t{topByExRel[rank].Node}\t{topByExRel[rank].ExclusiveRelevance:F6}");
                }
            }

            // --------- Generate log-log distribution plots ---------
            Console.WriteLine("\nGenerating distribution plots...");

            // Helper functions for frequency distribution
            static (double[] xs, double[] ys) ContinuousFrequency(double[] values)
            {
                var groups = values.Where(v => !double.IsNaN(v) && v > 0)
                                   .Select(v => Math.Round(v, 6))
                                   .GroupBy(v => v)
                                   .OrderBy(g => g.Key)
                                   .ToArray();
                var xs = groups.Select(g => (double)g.Key).ToArray();
                var ys = groups.Select(g => (double)g.Count()).ToArray();
                return (xs, ys);
            }

            // Occupation centrality distribution
            var (xOcc, yOcc) = ContinuousFrequency(occ);
            if (xOcc.Length > 0)
            {
                ChartGenerator.SaveDistributionPng(Path.Combine(outputDir, "loglog_occupation_centrality.png"), xOcc, yOcc, 
                    "Occupation Centrality Distribution");
                Console.WriteLine("Saved: loglog_occupation_centrality.png");
            }

            // Average relevance across all layers (per node)
            var avgRelevance = Enumerable.Range(0, n)
                .Select(i => Enumerable.Range(0, Lc).Average(L => ml.LayerRelevance(i, L)))
                .ToArray();
            var (xAvgRel, yAvgRel) = ContinuousFrequency(avgRelevance);
            if (xAvgRel.Length > 0)
            {
                ChartGenerator.SaveDistributionPng(Path.Combine(outputDir, "loglog_avg_relevance.png"), xAvgRel, yAvgRel,
                    "Average Relevance Across All Layers");
                Console.WriteLine("Saved: loglog_avg_relevance.png");
            }

            // Average exclusive relevance across all layers (per node)
            var avgExclusiveRelevance = Enumerable.Range(0, n)
                .Select(i => Enumerable.Range(0, Lc).Average(L => ml.ExclusiveLayerRelevance(i, L)))
                .ToArray();
            var (xAvgExRel, yAvgExRel) = ContinuousFrequency(avgExclusiveRelevance);
            if (xAvgExRel.Length > 0)
            {
                ChartGenerator.SaveDistributionPng(Path.Combine(outputDir, "loglog_avg_exclusive_relevance.png"), xAvgExRel, yAvgExRel,
                    "Average Exclusive Relevance Across All Layers");
                Console.WriteLine("Saved: loglog_avg_exclusive_relevance.png");
            }

            Console.WriteLine("\nAll visualizations generated successfully!");
        }
    }*/
}