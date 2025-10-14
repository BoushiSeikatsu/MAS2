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
    public static void Main(string[] args)
    {
        // File paths and separators
        var datasets = new List<(string name, string path, char separator)>
        {
            ("dolphins", "edges_dolphins.csv", ';'),
            ("karate", "edges karate.csv", ';'),
            ("lesmis", "edges lesmis.csv", ';')
        };

        int foldsCount = 10;
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

        // Store results: dataset -> algorithm -> list of (precision, recall, f2, threshold)
        var allResults = new Dictionary<string, Dictionary<string, List<(double precision, double recall, double f2, double threshold)>> >();

        foreach (var (name, path, separator) in datasets)
        {
            Console.WriteLine($"\n=== Dataset: {name} ===");
            var matrix = DokSparseMatrix<int>.FromFile(path, s => int.Parse(s), separator);
            var analyzer = new Analyzer<int>(matrix);
            var folds = analyzer.CrossValidation(foldsCount);
            var results = new Dictionary<string, List<(double precision, double recall, double f2, double threshold)>>();
            foreach (var alg in algorithms.Keys)
                results[alg] = new List<(double, double, double, double)>();

            // We'll collect scores per-algorithm for each fold so we can evaluate many thresholds
            var scoresPerAlg = algorithms.Keys.ToDictionary(k => k, k => new List<List<((int, int) edge, double score)>>());
            var removedEdgesPerFold = new List<HashSet<(int, int)>>();

            for (int foldIdx = 0; foldIdx < foldsCount; foldIdx++)
            {
                var testMatrix = folds[foldIdx]; // Matrix with edges removed
                var trainMatrix = new DokSparseMatrix<int>(matrix.Rows, matrix.Columns);
                for (int i = 0; i < matrix.Rows; i++)
                    for (int j = 0; j < matrix.Columns; j++)
                        trainMatrix[i, j] = testMatrix[i, j];

                // Find removed edges (positive samples)
                var removedEdges = new HashSet<(int, int)>();
                for (int i = 0; i < matrix.Rows; i++)
                    for (int j = i + 1; j < matrix.Columns; j++)
                        if (matrix[i, j] != 0 && testMatrix[i, j] == 0)
                            removedEdges.Add((i, j));

                removedEdgesPerFold.Add(removedEdges);

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

                    scoresPerAlg[alg.Key].Add(scores);
                }
            }

            // For each algorithm evaluate a range of thresholds (taken from observed scores across all folds)
            foreach (var alg in algorithms.Keys)
            {
                var perFoldScores = scoresPerAlg[alg];
                // gather candidate thresholds from all folds' score values
                var candidateThresholds = perFoldScores.SelectMany(s => s.Select(x => x.score)).Distinct().OrderBy(x => x).ToList();
                if (candidateThresholds.Count == 0)
                    candidateThresholds.Add(0.0);

                double bestThreshold = candidateThresholds[0];
                double bestAvgF2 = Double.NegativeInfinity;
                double bestAvgP = 0, bestAvgR = 0;

                //Console.WriteLine($"\nEvaluating thresholds for algorithm {alg} on dataset {name} (testing {candidateThresholds.Count} thresholds)");

                foreach (var thr in candidateThresholds)
                {
                    var ps = new List<double>();
                    var rs = new List<double>();
                    var f2s = new List<double>();

                    for (int foldIdx = 0; foldIdx < foldsCount; foldIdx++)
                    {
                        var scores = perFoldScores[foldIdx];
                        var predicted = scores.Where(x => x.score > thr).Select(x => x.edge).ToHashSet();
                        var removed = removedEdgesPerFold[foldIdx];
                        int tp = predicted.Count(e => removed.Contains(e));
                        int fp = predicted.Count - tp;
                        int fn = removed.Count - tp;
                        double precision = (tp + fp) == 0 ? 0 : (double)tp / (tp + fp);
                        double recall = removed.Count == 0 ? 0 : (double)tp / removed.Count;
                        double f2 = (precision + recall) == 0 ? 0 : (5 * precision * recall) / (4 * precision + recall);
                        ps.Add(precision);
                        rs.Add(recall);
                        f2s.Add(f2);
                    }

                    double avgP = ps.Average();
                    double avgR = rs.Average();
                    double avgF2 = f2s.Average();

                    //Console.WriteLine($"Alg={alg}, threshold={thr:F6}, Precision avg={avgP:F4}, Recall avg={avgR:F4}, F2 avg={avgF2:F4}");

                    if (avgF2 > bestAvgF2)
                    {
                        bestAvgF2 = avgF2;
                        bestThreshold = thr;
                        bestAvgP = avgP;
                        bestAvgR = avgR;
                    }
                }

                // store the best averaged results for this algorithm on this dataset
                results[alg].Add((bestAvgP, bestAvgR, bestAvgF2, bestThreshold));
                Console.WriteLine($"Best threshold for {alg} on {name}: {bestThreshold:F6} with F2 avg={bestAvgF2:F4}, Precision avg={bestAvgP:F4}, Recall avg={bestAvgR:F4}\n");
            }

            allResults[name] = results;
        }

        // Print average and deviation for each algorithm and dataset
        foreach (var dataset in allResults.Keys)
        {
            Console.WriteLine($"\nResults for {dataset}:");
            var results = allResults[dataset];
            foreach (var alg in algorithms.Keys)
            {
                var pr = results[alg].Select(x => x.precision).ToList();
                var rc = results[alg].Select(x => x.recall).ToList();
                var f2s = results[alg].Select(x => x.f2).ToList();
                var threshold = results[alg].Select(x => x.threshold).ToList();
                double avgP = pr.Average();
                double avgR = rc.Average();
                double avgF2 = f2s.Average();
                // Instead of reporting the average threshold, report the best threshold (the one with highest F2)
                double BestThreshold;
                if (results[alg].Count == 0)
                {
                    BestThreshold = 0.0;
                }
                else
                {
                    var bestEntry = results[alg].OrderByDescending(x => x.f2).First();
                    BestThreshold = bestEntry.threshold;
                }
                double stdP = Math.Sqrt(pr.Select(x => Math.Pow(x - avgP, 2)).Average());
                double stdR = Math.Sqrt(rc.Select(x => Math.Pow(x - avgR, 2)).Average());
                double stdF2 = Math.Sqrt(f2s.Select(x => Math.Pow(x - avgF2, 2)).Average());
                Console.WriteLine($"{alg}:\n Precision avg={avgP:F3} std={stdP:F3}, Recall avg={avgR:F3} std={stdR:F3}, F2 avg={avgF2:F3} std={stdF2:F3}, BestThreshold: {BestThreshold}");
            }
        }
    }
}