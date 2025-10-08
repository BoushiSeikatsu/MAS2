using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MAS2;

/*Dodělat inkrementálně rok po roku*/

 /*
  * Je třeba vykreslit grafy pro všechny 3 sítě.
  */
public class Program
{
    
    /*public static void Main(string[] args)
    {
        // List of networks to process: tuple of (file path, network label)
        var networks = new List<(string path, string label, char sep)>
        {
            ("com-youtube.ungraph.txt", "com-youtube", '\t'),
            ("9606.protein.links.v10.5.txt", "9606-protein", (char)32),
            ("socfb-Penn94.mtx", "socfb-Penn94", (char)32)
        };

        foreach (var (path, label, sep) in networks)
        {
            Console.WriteLine($"\n=== Processing network: {label} (file: {path}) ===");

            if (!File.Exists(path))
            {
                Console.WriteLine($"Warning: file not found: {path} — skipping {label}.");
                continue;
            }

            // Attempt to load matrix. Use tab as default separator; adjust per file if needed.
            DokSparseMatrix<int> matrix;
            try
            {
                matrix = DokSparseMatrix<int>.FromFile(path, s => int.Parse(s), sep);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load matrix from {path}: {ex.Message}");
                continue;
            }

            var analyzer = new Analyzer<int>(matrix);

            // Degree analysis
            Console.WriteLine("--- Degree Analysis ---");
            var (degrees, degreeTime) = analyzer.GetDegrees();
            Console.WriteLine($"Computation Time: {degreeTime.TotalMilliseconds:F2} ms");

            var (avgDegree, maxDegree) = analyzer.GetAverageAndMaximumDegree(degrees);
            Console.WriteLine($"Average Degree: {avgDegree:F2}");
            Console.WriteLine($"Maximum Degree: {maxDegree}");

            var degreeDistribution = analyzer.GetDegreeDistribution(degrees);

            // Clustering analysis
            Console.WriteLine("--- Clustering Effect Analysis ---");
            var (clusteringCoefficients, clusteringTime) = analyzer.GetClusteringCoefficients(degrees);
            Console.WriteLine($"Computation Time: {clusteringTime.TotalMilliseconds:F2} ms");

            // Average clustering coefficient (over all nodes)
            double avgClustering = 0.0;
            if (clusteringCoefficients != null && clusteringCoefficients.Length > 0)
            {
                avgClustering = clusteringCoefficients.Average();
            }
            Console.WriteLine($"Average Clustering Coefficient: {avgClustering:F4}");

            var clusteringDistribution = analyzer.GetClusteringDistribution(degrees, clusteringCoefficients ?? Array.Empty<double>());

            // Save plots with network label in filename
            try
            {
                var degXs = degreeDistribution.Keys.OrderBy(k => k).Select(k => (double)k).ToArray();
                var degYs = degreeDistribution.Keys.OrderBy(k => k).Select(k => (double)degreeDistribution[k]).ToArray();
                var degFile = $"{SanitizeFileName(label)}_degree.png";
                ChartGenerator.SaveDistributionPng(degFile, degXs, degYs, $"{label} - Degree Distribution (log-log)", logX: true, logY: true);

                var cluXs = clusteringDistribution.Keys.OrderBy(k => k).Select(k => (double)k).ToArray();
                var cluYs = clusteringDistribution.Keys.OrderBy(k => k).Select(k => (double)clusteringDistribution[k]).ToArray();
                var cluFile = $"{SanitizeFileName(label)}_clusteringCoefficient.png";
                ChartGenerator.SaveDistributionPng(cluFile, cluXs, cluYs, $"{label} - Clustering vs Degree (log-log)", logX: true, logY: true);

                Console.WriteLine($"Saved charts: {degFile}, {cluFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save charts for {label}: {ex.Message}");
            }

            // Common neighbors (kept for informational output)
            Console.WriteLine("--- Common Neighbors Analysis ---");
            var (commonNeighborsMatrix, commonNeighborsTime) = analyzer.GetCommonNeighbors();
            Console.WriteLine($"Computation Time: {commonNeighborsTime.TotalSeconds:F2} seconds");
            var (avgCommonNeighbors, maxCommonNeighbors) = analyzer.GetAverageAndMaximumCommonNeighbors(commonNeighborsMatrix);
            Console.WriteLine($"Average Number of Common Neighbors: {avgCommonNeighbors:F2}");
            Console.WriteLine($"Maximum Number of Common Neighbors: {maxCommonNeighbors}");
        }
    }*/

    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrEmpty(input)) return "network";
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(input.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return cleaned.Replace(' ', '_');
    }

   /* public static void Main(string[] args)
    {
        // File paths
        const string nvertsPath = "C:\\Users\\dub0074\\MAS2\\MAS2\\Cviko2\\coauth-DBLP-nverts.txt";
        const string simplicesPath = "C:\\Users\\dub0074\\MAS2\\MAS2\\Cviko2\\coauth-DBLP-simplices.txt";
        const string timesPath = "C:\\Users\\dub0074\\MAS2\\MAS2\\Cviko2\\coauth-DBLP-times.txt";

        // Check files exist
        if (!File.Exists(nvertsPath) || !File.Exists(simplicesPath) || !File.Exists(timesPath))
        {
            Console.WriteLine("Error: One or more data files were not found.");
            Console.WriteLine($"Checked: {nvertsPath}, {simplicesPath}, {timesPath}");
            return;
        }

        // Load temporal cliques
        var loader = new TemporalNetworkLoader(nvertsPath, simplicesPath, timesPath);
        var yearToCliques = loader.YearToCliques;

        // Print summary
        Console.WriteLine("Loaded clique network by year:");
        foreach (var kvp in yearToCliques.OrderBy(kvp => kvp.Key))
        {
            Console.WriteLine($"Year {kvp.Key}: {kvp.Value.Count} cliques");
        }

        // --- Analysis per year ---
        Console.WriteLine("\nYearly Network Analysis:");
        /*foreach (var kvp in yearToCliques.OrderBy(kvp => kvp.Key))
        {
            int year = kvp.Key;
            var cliques = kvp.Value;
            // Build node index mapping
            var allNodeIds = cliques.SelectMany(c => c.NodeIds).Distinct().OrderBy(id => id).ToList();
            var nodeIdToIndex = allNodeIds.Select((id, idx) => new { id, idx }).ToDictionary(x => x.id, x => x.idx);
            int nodeCount = allNodeIds.Count;
            var matrix = new DokSparseMatrix<int>(nodeCount, nodeCount);

            // Build weighted adjacency matrix
            foreach (var clique in cliques)
            {
                var ids = clique.NodeIds;
                for (int i = 0; i < ids.Count; i++)
                {
                    for (int j = i + 1; j < ids.Count; j++)
                    {
                        int idx1 = nodeIdToIndex[ids[i]];
                        int idx2 = nodeIdToIndex[ids[j]];
                        matrix[idx1, idx2] = matrix[idx1, idx2] + 1;
                        matrix[idx2, idx1] = matrix[idx2, idx1] + 1;
                    }
                }
            }

            var analyzer = new Analyzer<int>(matrix);
            var (degrees, _) = analyzer.GetDegrees();
            var (avgDegree, _) = analyzer.GetAverageAndMaximumDegree(degrees);

            // Weighted degree: sum of edge weights for each node
            var weightedDegrees = new int[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                int sum = 0;
                for (int j = 0; j < nodeCount; j++)
                {
                    if (i != j)
                        sum += matrix[i, j];
                }
                weightedDegrees[i] = sum;
            }
            double avgWeightedDegree = weightedDegrees.Average();

            var (clusteringCoefficients, _) = analyzer.GetClusteringCoefficients(degrees);
            double avgClustering = clusteringCoefficients.Average();

            // --- Find clique with highest average edge weight ---
            var cliquesAsIndices = cliques.Select(c => c.NodeIds.Select(id => nodeIdToIndex[id]).ToList()).ToList();
            var (bestCliqueIndices, bestAvgWeight) = analyzer.FindCliqueWithHighestAverageEdgeWeight(cliquesAsIndices);
            var bestCliqueNodeIds = bestCliqueIndices?.Select(idx => allNodeIds[idx]).ToList() ?? new List<int>();
            Console.WriteLine($"Year {year}: Avg Degree = {avgDegree:F2}, Avg Weighted Degree = {avgWeightedDegree:F2}, Avg Clustering Coefficient = {avgClustering:F4}");
            Console.WriteLine($"  Clique with highest average edge weight: [{string.Join(", ", bestCliqueNodeIds)}], Avg Edge Weight = {bestAvgWeight:F4}");
        }

        // --- Global analysis across all years ---
        var allCliques = yearToCliques.SelectMany(kvp => kvp.Value).ToList();
        var globalNodeIds = allCliques.SelectMany(c => c.NodeIds).Distinct().OrderBy(id => id).ToList();
        var globalNodeIdToIndex = globalNodeIds.Select((id, idx) => new { id, idx }).ToDictionary(x => x.id, x => x.idx);
        int globalNodeCount = globalNodeIds.Count;
        var globalMatrix = new DokSparseMatrix<int>(globalNodeCount, globalNodeCount);
        foreach (var clique in allCliques)
        {
            var ids = clique.NodeIds;
            for (int i = 0; i < ids.Count; i++)
            {
                for (int j = i + 1; j < ids.Count; j++)
                {
                    int idx1 = globalNodeIdToIndex[ids[i]];
                    int idx2 = globalNodeIdToIndex[ids[j]];
                    globalMatrix[idx1, idx2] = globalMatrix[idx1, idx2] + 1;
                    globalMatrix[idx2, idx2] = globalMatrix[idx2, idx2] + 1;
                }
            }
        }
        var globalAnalyzer = new Analyzer<int>(globalMatrix);
        var allCliquesAsIndices = allCliques.Select(c => c.NodeIds.Select(id => globalNodeIdToIndex[id]).ToList()).ToList();
        var (globalBestCliqueIndices, globalBestAvgWeight) = globalAnalyzer.FindCliqueWithHighestAverageEdgeWeight(allCliquesAsIndices);
        var globalBestCliqueNodeIds = globalBestCliqueIndices?.Select(idx => globalNodeIds[idx]).ToList() ?? new List<int>();
        Console.WriteLine("\nGlobal analysis across all years:");
        Console.WriteLine($"  Clique with highest average edge weight: [{string.Join(", ", globalBestCliqueNodeIds)}], Avg Edge Weight = {globalBestAvgWeight:F4}");
    }*/
}