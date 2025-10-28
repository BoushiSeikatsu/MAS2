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
    /*    public static void Main(string[] args)
    {
            // mode: "observed"/"normalized"
            string mode = "normalized";
            if (args.Length > 0)
                mode = args[0].ToLowerInvariant();
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

        // Store results: dataset -> algorithm -> list of (precision, recall, f1, threshold)
        var allResults = new Dictionary<string, Dictionary<string, List<(double precision, double recall, double f1, double threshold)>> >();

        foreach (var (name, path, separator) in datasets)
        {
            Console.WriteLine($"\n=== Dataset: {name} ===");
            var matrix = DokSparseMatrix<int>.FromFile(path, s => int.Parse(s), separator);
            var analyzer = new Analyzer<int>(matrix);
            var folds = analyzer.CrossValidation(foldsCount);
            var results = new Dictionary<string, List<(double precision, double recall, double f1, double threshold)>>();
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

            // For each algorithm evaluate a range of thresholds
            // Two modes supported:
            //  - "observed": candidate thresholds are taken from observed score values (original behavior)
            //  - "normalized": all scores for the algorithm are min-max normalized to [0,1] and thresholds 0.0..1.0 (step 0.1) are evaluated
            // Keep the original logic under "observed" to preserve previous solution.
            Console.WriteLine($"Evaluation mode: {mode}");
            bool useNormalizedThresholds = mode == "normalized";

            // Helper to produce normalized thresholds list
            List<double> fixedNormalizedThresholds = Enumerable.Range(0, 11).Select(i => i / 10.0).ToList();

            // We'll need to store normalized score lists per algorithm across folds when using normalization
            var allScoresPerAlgForNormalization = new Dictionary<string, List<double>>();
            if (useNormalizedThresholds)
            {
                foreach (var alg in algorithms.Keys)
                    allScoresPerAlgForNormalization[alg] = new List<double>();

                // collect all scores across folds for each algorithm
                foreach (var alg in algorithms.Keys)
                {
                    for (int foldIdx = 0; foldIdx < foldsCount; foldIdx++)
                    {
                        var scores = scoresPerAlg[alg][foldIdx];
                        foreach (var p in scores)
                            allScoresPerAlgForNormalization[alg].Add(p.score);
                    }
                }
            }

            // For each algorithm evaluate a range of thresholds (taken from observed scores across all folds)
            foreach (var alg in algorithms.Keys)
            {
                // We'll also collect avg-F1 values for each candidate threshold so we can plot F1 vs threshold
                var thresholdsEvaluated = new List<double>();
                var avgF1PerThreshold = new List<double>();

                var perFoldScores = scoresPerAlg[alg];
                List<double> candidateThresholds;

                if (!useNormalizedThresholds)
                {
                    // gather candidate thresholds from all folds' score values (original behavior)
                    candidateThresholds = perFoldScores.SelectMany(s => s.Select(x => x.score)).Distinct().OrderBy(x => x).ToList();
                    if (candidateThresholds.Count == 0)
                        candidateThresholds.Add(0.0);
                }
                else
                {
                    // Normalization: min-max per-algorithm across all folds
                    var allScores = allScoresPerAlgForNormalization[alg];
                    double min = allScores.Count == 0 ? 0.0 : allScores.Min();
                    double max = allScores.Count == 0 ? 0.0 : allScores.Max();
                    // To avoid division by zero when all scores are equal, treat them as zero after normalization
                    // We'll create normalized per-fold score lists on the fly during evaluation.
                    candidateThresholds = fixedNormalizedThresholds;
                }

                double bestThreshold = candidateThresholds[0];
                double bestAvgF1 = Double.NegativeInfinity;
                double bestAvgP = 0, bestAvgR = 0;

                //Console.WriteLine($"\nEvaluating thresholds for algorithm {alg} on dataset {name} (testing {candidateThresholds.Count} thresholds)");

                foreach (var thr in candidateThresholds)
                {
                    var ps = new List<double>();
                    var rs = new List<double>();
                    var f1s = new List<double>();

                    for (int foldIdx = 0; foldIdx < foldsCount; foldIdx++)
                    {
                        var scores = perFoldScores[foldIdx];

                        // If using normalized thresholds, normalize scores for this algorithm and fold
                        IEnumerable<((int, int) edge, double score)> normScoresEnumerable = scores;
                        if (useNormalizedThresholds)
                        {
                            var allScores = allScoresPerAlgForNormalization[alg];
                            double min = allScores.Count == 0 ? 0.0 : allScores.Min();
                            double max = allScores.Count == 0 ? 0.0 : allScores.Max();
                            double denom = max - min;
                            if (denom == 0)
                            {
                                // all scores same -> set normalized to 0
                                normScoresEnumerable = scores.Select(p => (p.edge, 0.0));
                            }
                            else
                            {
                                normScoresEnumerable = scores.Select(p => (p.edge, (p.score - min) / denom));
                            }
                        }

                        var predicted = normScoresEnumerable.Where(x => x.score > thr).Select(x => x.edge).ToHashSet();
                        var removed = removedEdgesPerFold[foldIdx];
                        int tp = predicted.Count(e => removed.Contains(e));
                        int fp = predicted.Count - tp;
                        int fn = removed.Count - tp;
                        double precision = (tp + fp) == 0 ? 0 : (double)tp / (tp + fp);
                        double recall = removed.Count == 0 ? 0 : (double)tp / removed.Count;
                        double f1 = (precision + recall) == 0 ? 0 : (2 * precision * recall) / (precision + recall);
                        ps.Add(precision);
                        rs.Add(recall);
                        f1s.Add(f1);
                    }

                    double avgP = ps.Average();
                    double avgR = rs.Average();
                    double avgF1 = f1s.Average();

                    // store for plotting
                    thresholdsEvaluated.Add(thr);
                    avgF1PerThreshold.Add(avgF1);

                    //Console.WriteLine($"Alg={alg}, threshold={thr:F6}, Precision avg={avgP:F4}, Recall avg={avgR:F4}, F2 avg={avgF2:F4}");

                    if (avgF1 > bestAvgF1)
                    {
                        bestAvgF1 = avgF1;
                        bestThreshold = thr;
                        bestAvgP = avgP;
                        bestAvgR = avgR;
                    }
                }

                // store the best averaged results for this algorithm on this dataset
                results[alg].Add((bestAvgP, bestAvgR, bestAvgF1, bestThreshold));
                Console.WriteLine($"Best threshold for {alg} on {name}: {bestThreshold:F6} with F1 avg={bestAvgF1:F4}, Precision avg={bestAvgP:F4}, Recall avg={bestAvgR:F4}\n");

                // Save per-algorithm threshold->F1 series into a per-dataset structure for later plotting
                // We'll attach these to a temporary dictionary stored in a local variable that we'll use below.
                // Use filesafe alg name for keys
                // Store on disk after finishing all algorithms for this dataset
                string perAlgPrefix = alg; // not used further here
            }

            // After evaluations we want to produce a line chart with one series per algorithm showing F1 vs threshold.
            try
            {
                // Build series arrays: thresholds are the same for normalized mode, but for observed mode they vary per algorithm.
                // To keep the plot simple, use the fixedNormalizedThresholds (0..1 step 0.1) when in normalized mode.
                if (useNormalizedThresholds)
                {
                    var seriesLabels = algorithms.Keys.ToArray();
                    var xsPerSeries = new double[seriesLabels.Length][];
                    var ysPerSeries = new double[seriesLabels.Length][];
                    for (int i = 0; i < seriesLabels.Length; i++)
                    {
                        var algName = seriesLabels[i];
                        // Recompute avg F1 per threshold for the algorithm using same logic as above to ensure data is available here.
                        var perFoldScores = scoresPerAlg[algName];
                        var allScores = allScoresPerAlgForNormalization[algName];
                        double min = allScores.Count == 0 ? 0.0 : allScores.Min();
                        double max = allScores.Count == 0 ? 0.0 : allScores.Max();
                        double denom = max - min;

                        var xs = fixedNormalizedThresholds.ToArray();
                        var ys = new double[xs.Length];
                        for (int ti = 0; ti < xs.Length; ti++)
                        {
                            double thr = xs[ti];
                            var f1sList = new List<double>();
                            for (int foldIdx = 0; foldIdx < foldsCount; foldIdx++)
                            {
                                var scores = perFoldScores[foldIdx];
                                IEnumerable<((int, int) edge, double score)> normScoresEnumerable = scores;
                                if (denom == 0)
                                    normScoresEnumerable = scores.Select(p => (p.edge, 0.0));
                                else
                                    normScoresEnumerable = scores.Select(p => (p.edge, (p.score - min) / denom));

                                var predicted = normScoresEnumerable.Where(x => x.score > thr).Select(x => x.edge).ToHashSet();
                                var removed = removedEdgesPerFold[foldIdx];
                                int tp = predicted.Count(e => removed.Contains(e));
                                int fp = predicted.Count - tp;
                                double precision = (tp + fp) == 0 ? 0 : (double)tp / (tp + fp);
                                double recall = removed.Count == 0 ? 0 : (double)tp / removed.Count;
                                double f1 = (precision + recall) == 0 ? 0 : (2 * precision * recall) / (precision + recall);
                                f1sList.Add(f1);
                            }
                            ys[ti] = f1sList.Count == 0 ? 0.0 : f1sList.Average();
                        }
                        xsPerSeries[i] = xs;
                        ysPerSeries[i] = ys;
                    }

                    string chartsDir = Path.Combine("charts");
                    Directory.CreateDirectory(chartsDir);
                    string lineChartPath = Path.Combine(chartsDir, $"{name}_{mode}_f1_vs_threshold.png");
                    ChartGenerator.SaveLineChart(lineChartPath, algorithms.Keys.ToArray(), xsPerSeries, ysPerSeries, $"{name} F1 vs threshold ({mode})");
                    Console.WriteLine($"Saved line chart: {lineChartPath}");
                }
                else
                {
                    // Observed mode: thresholds differ per algorithm. We'll sample thresholds by taking union of candidate thresholds across algorithms,
                    // sort them, and evaluate each algorithm on that common set for plotting.
                    var unionThresholds = new HashSet<double>();
                    foreach (var alg in algorithms.Keys)
                    {
                        var perFoldScores = scoresPerAlg[alg];
                        var candidateThresholds = perFoldScores.SelectMany(s => s.Select(x => x.score)).Distinct().OrderBy(x => x).ToList();
                        foreach (var t in candidateThresholds) unionThresholds.Add(t);
                    }
                    var xs = unionThresholds.OrderBy(x => x).ToArray();
                    // For plotting, normalize x-axis to [0,1] by mapping observed score values to their rank-percentile between min and max observed across all algorithms
                    double globalMin = xs.Length == 0 ? 0.0 : xs.Min();
                    double globalMax = xs.Length == 0 ? 1.0 : xs.Max();
                    double denom = globalMax - globalMin;
                    var xsScaled = xs.Select(v => denom == 0 ? 0.0 : (v - globalMin) / denom).ToArray();

                    var seriesLabels = algorithms.Keys.ToArray();
                    var xsPerSeries = new double[seriesLabels.Length][];
                    var ysPerSeries = new double[seriesLabels.Length][];
                    for (int i = 0; i < seriesLabels.Length; i++)
                    {
                        var algName = seriesLabels[i];
                        var perFoldScores = scoresPerAlg[algName];
                        var ys = new double[xs.Length];
                        for (int xi = 0; xi < xs.Length; xi++)
                        {
                            double thr = xs[xi];
                            var f1sList = new List<double>();
                            for (int foldIdx = 0; foldIdx < foldsCount; foldIdx++)
                            {
                                var scores = perFoldScores[foldIdx];
                                var predicted = scores.Where(x => x.score > thr).Select(x => x.edge).ToHashSet();
                                var removed = removedEdgesPerFold[foldIdx];
                                int tp = predicted.Count(e => removed.Contains(e));
                                int fp = predicted.Count - tp;
                                double precision = (tp + fp) == 0 ? 0 : (double)tp / (tp + fp);
                                double recall = removed.Count == 0 ? 0 : (double)tp / removed.Count;
                                double f1 = (precision + recall) == 0 ? 0 : (2 * precision * recall) / (precision + recall);
                                f1sList.Add(f1);
                            }
                            ys[xi] = f1sList.Count == 0 ? 0.0 : f1sList.Average();
                        }

                        xsPerSeries[i] = xsScaled;
                        ysPerSeries[i] = ys;
                    }

                    string chartsDir = Path.Combine("charts");
                    Directory.CreateDirectory(chartsDir);
                    string lineChartPath = Path.Combine(chartsDir, $"{name}_{mode}_f1_vs_threshold.png");
                    ChartGenerator.SaveLineChart(lineChartPath, algorithms.Keys.ToArray(), xsPerSeries, ysPerSeries, $"{name} F1 vs threshold ({mode})");
                    Console.WriteLine($"Saved line chart: {lineChartPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save line chart for {name}: {ex.Message}");
            }

            allResults[name] = results;

            // After evaluating all algorithms for this dataset, create a bar chart of the best F1 scores per algorithm
            try
            {
                var labels = algorithms.Keys.ToArray();
                var values = labels.Select(l => results[l].Count == 0 ? 0.0 : results[l].OrderByDescending(x => x.f1).First().f1).ToArray();
                string chartsDir = Path.Combine("charts");
                Directory.CreateDirectory(chartsDir);
                string chartPath = Path.Combine(chartsDir, $"{name}_{mode}_bestf1.png");
                ChartGenerator.SaveBarChart(chartPath, labels, values, $"{name} best F1 ({mode})");
                Console.WriteLine($"Saved chart: {chartPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save chart for {name}: {ex.Message}");
            }
        }

        // --- Multilayer example (optional) ---
        var layerFiles = new List<string>
        {
            "layer1.csv",
            "layer2.csv",
            "layer3.csv"
        };
        bool allExist = layerFiles.All(f => File.Exists(f));
        if (allExist)
        {
            var ml = MultilayerNetwork.LoadFromFiles(layerFiles, ';');
            Console.WriteLine($"Loaded multilayer network with {ml.Layers.Count} layers and {ml.NodeCount} nodes.");
            var degPer = ml.GetDegreesPerLayer();
            var clusPer = ml.GetClusteringPerLayer();
            var aggDeg = ml.GetAggregateDegree();
            var mux = ml.GetMultiplexDegree();
            for (int i = 0; i < ml.NodeCount; i++)
            {
                Console.WriteLine($"Node {i}: aggregateDegree={aggDeg[i]}, multiplexDegree={mux[i]}");
            }
        }
        else
        {
            Console.WriteLine("Multilayer example skipped: example layer1.csv/layer2.csv/layer3.csv not found in working directory.");
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
                var f1s = results[alg].Select(x => x.f1).ToList();
                var threshold = results[alg].Select(x => x.threshold).ToList();
                double avgP = pr.Average();
                double avgR = rc.Average();
                double avgF1 = f1s.Average();
                // Instead of reporting the average threshold, report the best threshold (the one with highest F1)
                double BestThreshold;
                if (results[alg].Count == 0)
                {
                    BestThreshold = 0.0;
                }
                else
                {
                    var bestEntry = results[alg].OrderByDescending(x => x.f1).First();
                    BestThreshold = bestEntry.threshold;
                }
                double stdP = Math.Sqrt(pr.Select(x => Math.Pow(x - avgP, 2)).Average());
                double stdR = Math.Sqrt(rc.Select(x => Math.Pow(x - avgR, 2)).Average());
                double stdF1 = Math.Sqrt(f1s.Select(x => Math.Pow(x - avgF1, 2)).Average());
                Console.WriteLine($"{alg}:\n Precision avg={avgP:F3} std={stdP:F3}, Recall avg={avgR:F3} std={stdR:F3}, F1 avg={avgF1:F3} std={stdF1:F3}, BestThreshold: {BestThreshold}");
            }
        }
    }*/
}