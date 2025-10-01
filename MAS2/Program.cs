using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MAS2;

/*Dodělat inkrementálně rok po roku*/

public class Program
{
    
    public static void Main(string[] args)
    {
        const string filePath = "com-youtube.ungraph.txt";

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: The data file was not found at '{filePath}'.");
            Console.WriteLine("Please ensure the file is in the same directory as the executable.");
            return;
        }

        // Construct the sparse matrix from the protein links file.
        // The value parser s => int.Parse(s) converts the score string to an integer.
        var matrix = DokSparseMatrix<int>.FromFile(filePath, s => int.Parse(s),'\t');
        //Console.WriteLine(matrix.ToString());
        var analyzer = new Analyzer<int>(matrix);

        // --- Degree Analysis ---
        Console.WriteLine("\n--- Degree Analysis ---");
        var (degrees, degreeTime) = analyzer.GetDegrees();
        Console.WriteLine($"Computation Time: {degreeTime.TotalMilliseconds:F2} ms");

        var (avgDegree, maxDegree) = analyzer.GetAverageAndMaximumDegree(degrees);
        Console.WriteLine($"Average Degree: {avgDegree:F2}");
        Console.WriteLine($"Maximum Degree: {maxDegree}");

        var degreeDistribution = analyzer.GetDegreeDistribution(degrees);
        Console.WriteLine("\nDegree Distribution (Log-Log Scale):");
        // Displaying a small sample of the distribution for brevity
        foreach (var entry in degreeDistribution.OrderBy(d => d.Key).Take(10))
        {
            if (entry.Key > 0 && entry.Value > 0)
            {
                Console.WriteLine($"Log(Degree {entry.Key}): {Math.Log(entry.Key):F2}, Log(Count {entry.Value}): {Math.Log(entry.Value):F2}");
            }
        }

        // --- Clustering Effect Analysis ---
        Console.WriteLine("\n--- Clustering Effect Analysis ---");
        var (clusteringCoefficients, clusteringTime) = analyzer.GetClusteringCoefficients(degrees);
        Console.WriteLine($"Computation Time: {clusteringTime.TotalMilliseconds:F2} ms");

        var clusteringDistribution = analyzer.GetClusteringDistribution(degrees, clusteringCoefficients);
        Console.WriteLine("\nClustering Distribution (Degree x CC, Log-Log Scale):");
        // Displaying a small sample
        foreach (var entry in clusteringDistribution.OrderBy(d => d.Key).Take(10))
        {
            if (entry.Key > 0 && entry.Value > 0)
            {
                Console.WriteLine($"Log(Degree {entry.Key}): {Math.Log(entry.Key):F2}, Log(Avg CC {entry.Value:F4}): {Math.Log(entry.Value):F2}");
            }
        }

        // --- Common Neighbors Analysis ---
        Console.WriteLine("\n--- Common Neighbors Analysis ---");
        var (commonNeighborsMatrix, commonNeighborsTime) = analyzer.GetCommonNeighbors();
        Console.WriteLine($"Computation Time: {commonNeighborsTime.TotalSeconds:F2} seconds");

        var (avgCommonNeighbors, maxCommonNeighbors) = analyzer.GetAverageAndMaximumCommonNeighbors(commonNeighborsMatrix);
        Console.WriteLine($"\nAverage Number of Common Neighbors: {avgCommonNeighbors:F2}");
        Console.WriteLine($"Maximum Number of Common Neighbors: {maxCommonNeighbors}");
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