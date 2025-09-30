using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MAS2;

public class Program
{
    public static void Main(string[] args)
    {
        // File paths
        const string nvertsPath = "C:\\Users\\mduba\\Documents\\Python Scripts\\MAS2\\MAS2\\Cviko2\\coauth-DBLP-nverts.txt";
        const string simplicesPath = "C:\\Users\\mduba\\Documents\\Python Scripts\\MAS2\\MAS2\\Cviko2\\coauth-DBLP-simplices.txt";
        const string timesPath = "C:\\Users\\mduba\\Documents\\Python Scripts\\MAS2\\MAS2\\Cviko2\\coauth-DBLP-times.txt";

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
        }*/

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
                    globalMatrix[idx2, idx1] = globalMatrix[idx2, idx1] + 1;
                }
            }
        }
        var globalAnalyzer = new Analyzer<int>(globalMatrix);
        var allCliquesAsIndices = allCliques.Select(c => c.NodeIds.Select(id => globalNodeIdToIndex[id]).ToList()).ToList();
        var (globalBestCliqueIndices, globalBestAvgWeight) = globalAnalyzer.FindCliqueWithHighestAverageEdgeWeight(allCliquesAsIndices);
        var globalBestCliqueNodeIds = globalBestCliqueIndices?.Select(idx => globalNodeIds[idx]).ToList() ?? new List<int>();
        Console.WriteLine("\nGlobal analysis across all years:");
        Console.WriteLine($"  Clique with highest average edge weight: [{string.Join(", ", globalBestCliqueNodeIds)}], Avg Edge Weight = {globalBestAvgWeight:F4}");
    }
}