using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MAS2;

/*
 Zkusime jine prahy a jine metriky pro jine site a potom porovname vysledky na konci, budeme pouzivat cross validaci.
    Precision, recall, F1-score -> všechny metody pro predikce linku a všechny sítě
 */
public class Cviko3
{
    
    /*public static void Main(string[] args)
    {
    // File paths
        const string dolphins = "edges_dolphins.csv";
        const string karate = "E:\\C#Projects\\MAS2\\Data\\cviko3\\edges karate.txt";
        const string lesmis = "E:\\C#Projects\\MAS2\\Data\\cviko3\\edges lesmis.txt";

        // Load a network (example: dolphins)
        var matrix = DokSparseMatrix<int>.FromFile(dolphins, s => int.Parse(s),';');
        //Console.WriteLine(matrix.ToString());
        int foldsCount = 10;
        var analyzer = new Analyzer<int>(matrix);
        var folds = analyzer.CrossValidation(foldsCount);
        
        // List of LP algorithms
        var algorithms = new Dictionary<string, Func<DokSparseMatrix<int>, int, int, double>>
        {
            { "CommonNeighbors", (m, u, v) => LPAlgorithms.CommonNeighbors(m, u, v) },
            { "JaccardCoefficient", (m, u, v) => LPAlgorithms.JaccardCoefficient(m, u, v) },
            { "AdamicAdar", (m, u, v) => LPAlgorithms.AdamicAdar(m, u, v) },
            { "PreferentialAttachment", (m, u, v) => LPAlgorithms.PreferentialAttachment(m, u, v) },
            { "ResourceAllocation", (m, u, v) => LPAlgorithms.ResourceAllocation(m, u, v) },
            { "CosineSimilarity", (m, u, v) => LPAlgorithms.CosineSimilarity(m, u, v) },
            { "SorensenIndex", (m, u, v) => LPAlgorithms.SorensenIndex(m, u, v) },
            { "CARIndex", (m, u, v) => LPAlgorithms.CARIndex(m, u, v) }
        };

        // Store results: algorithm -> list of (precision, recall, f2)
        var results = new Dictionary<string, List<(double precision, double recall, double f2, double threshold)>>();
        foreach (var alg in algorithms.Keys)
            results[alg] = new List<(double, double, double, double)>();
        foreach(var fold in folds) {
            //Console.WriteLine(fold.ToString());
        }
        for (int foldIdx = 0; foldIdx < foldsCount; foldIdx++)
        {
            var testMatrix = folds[foldIdx]; // Matrix with edges removed
            var trainMatrix = new DokSparseMatrix<int>(matrix.Rows, matrix.Columns);
            for (int i = 0; i < matrix.Rows; i++)
                for (int j = 0; j < matrix.Columns; j++)
                    trainMatrix[i, j] = testMatrix[i, j];

            // Find removed edges (positive samples)
            var removedEdges = new List<(int, int)>();
            for (int i = 0; i < matrix.Rows; i++)
                for (int j = i + 1; j < matrix.Columns; j++)
                    if (matrix[i, j] != 0 && testMatrix[i, j] == 0)
                        removedEdges.Add((i, j));

            // Negative samples: pairs with no edge in either matrix
            var negativeEdges = new List<(int, int)>();
            for (int i = 0; i < matrix.Rows; i++)
                for (int j = i + 1; j < matrix.Columns; j++)
                    if (matrix[i, j] == 0 && testMatrix[i, j] == 0)
                        negativeEdges.Add((i, j));

            foreach (var alg in algorithms)
            {
                // Score all possible pairs (not connected in trainMatrix)
                var scores = new List<((int, int) edge, double score)>();
                for (int i = 0; i < trainMatrix.Rows; i++)
                {
                    for (int j = i + 1; j < trainMatrix.Columns; j++)
                    {
                        if (trainMatrix[i, j] == 0)
                        {
                            double score = alg.Value(trainMatrix, i, j);
                            scores.Add(((i, j), score));
                        }
                    }
                }

                // Use median score as threshold
                var scoreValues = scores.Select(x => x.score).OrderBy(x => x).ToList();
                //double threshold = scoreValues.Count == 0 ? 0 : scoreValues[scoreValues.Count / 2];
                double threshold = scoreValues.Count == 0 ? 0 : scoreValues.Average();
                // Predict edges: add if score >= threshold
                var predicted = scores.Where(x => x.score > threshold).Select(x => x.edge).ToHashSet();

                // Only evaluate on removedEdges (positives)
                int tp = predicted.Count(e => removedEdges.Contains(e));
                int fp = predicted.Count - tp;
                int fn = removedEdges.Count - tp;
                double precision = (tp + fp) == 0 ? 0 : (double)tp / (tp + fp);
                double recall = removedEdges.Count == 0 ? 0 : (double)tp / removedEdges.Count;
                double f2 = (precision + recall) == 0 ? 0 : (5 * precision * recall) / (4 * precision + recall);
                results[alg.Key].Add((precision, recall, f2, threshold));
            }
        }

        // Print average and deviation for each algorithm
        Console.WriteLine("\nResults:");
        foreach (var alg in algorithms.Keys)
        {
            var pr = results[alg].Select(x => x.precision).ToList();
            var rc = results[alg].Select(x => x.recall).ToList();
            var f2s = results[alg].Select(x => x.f2).ToList();
            var threshold = results[alg].Select(x => x.threshold).ToList();
            double avgP = pr.Average();
            double avgR = rc.Average();
            double avgF2 = f2s.Average();
            double AvgThreshold = threshold.Average();
            double stdP = Math.Sqrt(pr.Select(x => Math.Pow(x - avgP, 2)).Average());
            double stdR = Math.Sqrt(rc.Select(x => Math.Pow(x - avgR, 2)).Average());
            double stdF2 = Math.Sqrt(f2s.Select(x => Math.Pow(x - avgF2, 2)).Average());
            Console.WriteLine($"{alg}:\n Precision avg={avgP:F3} std={stdP:F3}, Recall avg={avgR:F3} std={stdR:F3}, F2 avg={avgF2:F3} std={stdF2:F3}, {AvgThreshold}");
        }

    }*/
}