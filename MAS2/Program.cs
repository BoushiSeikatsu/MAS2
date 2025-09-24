using System;
using System.IO;
using System.Linq;
using MAS2;

public class Program
{
    public static void Main(string[] args)
    {
        const string filePath = "9606.protein.links.v10.5.txt";

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: The data file was not found at '{filePath}'.");
            Console.WriteLine("Please ensure the file is in the same directory as the executable.");
            return;
        }

        // Construct the sparse matrix from the protein links file.
        // The value parser s => int.Parse(s) converts the score string to an integer.
        var matrix = DokSparseMatrix<int>.FromFile(filePath, s => int.Parse(s));

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
}