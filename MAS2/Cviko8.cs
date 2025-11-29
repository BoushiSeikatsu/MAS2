using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using MAS2;
//Potřeba je kouknout na ty metriky!
//Bude ho hodně zajímat pravidla, vypsat je, jak jsem je našel, jak je používám apod, apriori algoritmus?
namespace MAS2
{
    /*public class Cviko8
    {
        public static void Main(string[] args)
        {
            // -----------------------------
            // CLI PARAMETERS
            // Files: non --* arguments
            // --sourceLayer i (default 0)
            // --targetLayer j (default 1)
            // --raTopK K (optional, choose top-K RA candidates)
            // --raMinScore S (alternative threshold if raTopK absent)
            // --supportMinLayers M (minimum number of layers containing the edge to be considered; default 1)
            // --holdoutFrac f (0<f<1 : fraction of target-layer edges removed for evaluation)
            // --seed s (random seed for holdout)
            // --outputPrefix name (prefix for output CSVs)
            // Example: Cviko8 Kaktovi.edges Venetie.edges Wainwright.edges --sourceLayer 0 --targetLayer 1 --raTopK 100
            // -----------------------------

            var defaultFiles = new List<string>
            {
                Path.Combine("bin", "Debug", "net8.0", "Kaktovi.edges"),
                Path.Combine("bin", "Debug", "net8.0", "Venetie.edges"),
                Path.Combine("bin", "Debug", "net8.0", "Wainwright.edges")
            };

            var fileArgs = new List<string>();
            int sourceLayer = 0;
            int targetLayer = 1;
            int? raTopK = null;
            double raMinScore = 0.0; // if raTopK not used
            int supportMinLayers = 1;
            double? holdoutFrac = null;
            int seed = 42;
            string outputPrefix = "cviko8";

            for (int i = 0; i < (args?.Length ?? 0); i++)
            {
                var a = args[i];
                if (!a.StartsWith("--")) { fileArgs.Add(a); continue; }
                string Next(int offset = 1) => (i + offset < args.Length) ? args[i + offset] : null;
                switch (a)
                {
                    case "--sourceLayer":
                        if (int.TryParse(Next(), out var sl)) { sourceLayer = sl; i++; }
                        break;
                    case "--targetLayer":
                        if (int.TryParse(Next(), out var tl)) { targetLayer = tl; i++; }
                        break;
                    case "--raTopK":
                        if (int.TryParse(Next(), out var rk)) { raTopK = rk; i++; }
                        break;
                    case "--raMinScore":
                        if (double.TryParse(Next(), NumberStyles.Float, CultureInfo.InvariantCulture, out var ms)) { raMinScore = ms; i++; }
                        break;
                    case "--supportMinLayers":
                        if (int.TryParse(Next(), out var sm)) { supportMinLayers = sm; i++; }
                        break;
                    case "--holdoutFrac":
                        if (double.TryParse(Next(), NumberStyles.Float, CultureInfo.InvariantCulture, out var hf)) { holdoutFrac = hf; i++; }
                        break;
                    case "--seed":
                        if (int.TryParse(Next(), out var sd)) { seed = sd; i++; }
                        break;
                    case "--outputPrefix":
                        if (Next() != null) { outputPrefix = Next(); i++; }
                        break;
                }
            }
            if (fileArgs.Count == 0) fileArgs = defaultFiles;

            var existing = fileArgs.Where(f => File.Exists(f)).ToList();
            if (existing.Count == 0)
            {
                Console.WriteLine("No layer files found. Provide file paths as arguments or ensure default .edges files exist.");
                return;
            }
            Console.WriteLine($"Loading {existing.Count} layer files (multilayer edge format)...");
            var ml = MultilayerNetwork.LoadFromMultilayerFiles(existing, ' ', true);
            int Lcount = ml.Layers.Count;
            int N = ml.NodeCount;
            Console.WriteLine($"Loaded multilayer network with {Lcount} layers and {N} nodes.");

            if (Lcount == 0 || N == 0)
            {
                Console.WriteLine("Empty network; aborting.");
                return;
            }
            if (sourceLayer < 0 || sourceLayer >= Lcount || targetLayer < 0 || targetLayer >= Lcount)
            {
                Console.WriteLine($"Invalid source/target layer indices (source={sourceLayer}, target={targetLayer}). Layer range: 0..{Lcount - 1}");
                return;
            }
            if (sourceLayer == targetLayer)
            {
                Console.WriteLine("Source and target layer must differ for prediction experiment.");
                return;
            }
            if (holdoutFrac.HasValue && (holdoutFrac.Value <= 0 || holdoutFrac.Value >= 1))
            {
                Console.WriteLine("holdoutFrac must be in (0,1). Ignoring provided value.");
                holdoutFrac = null;
            }

            var src = ml.Layers[sourceLayer];
            var tgt = ml.Layers[targetLayer];

            // Collect edges (u,v) with u < v existing in source layer
            var sourceEdges = new List<(int u, int v)>();
            for (int i = 0; i < N; i++)
            {
                for (int j = i + 1; j < N; j++)
                {
                    if (src[i, j] != 0)
                        sourceEdges.Add((i, j));
                }
            }
            Console.WriteLine($"Source layer {sourceLayer} edges: {sourceEdges.Count}");

            // Determine actual confidence of rule (edge in source -> edge in target)
            int bothCount = sourceEdges.Count(e => tgt[e.u, e.v] != 0);
            double ruleConfidence = sourceEdges.Count == 0 ? 0.0 : (double)bothCount / sourceEdges.Count;
            Console.WriteLine($"Empirical confidence (edge in L{sourceLayer} implies edge in L{targetLayer}): {ruleConfidence:F6}");

            // OPTIONAL HOLDOUT: remove fraction of target edges for evaluation
            var removedEdges = new HashSet<(int u, int v)>();
            var rng = new Random(seed);
            if (holdoutFrac.HasValue)
            {
                var targetEdges = new List<(int u, int v)>();
                for (int i = 0; i < N; i++)
                {
                    for (int j = i + 1; j < N; j++)
                    {
                        if (tgt[i, j] != 0) targetEdges.Add((i, j));
                    }
                }
                int removeCount = (int)Math.Floor(holdoutFrac.Value * targetEdges.Count);
                var shuffled = targetEdges.OrderBy(_ => rng.Next()).ToList();
                foreach (var e in shuffled.Take(removeCount))
                {
                    tgt[e.u, e.v] = 0; // remove edge for prediction task
                    tgt[e.v, e.u] = 0;
                    removedEdges.Add(e);
                }
                Console.WriteLine($"Holdout applied: removed {removedEdges.Count} edges from target layer {targetLayer} for evaluation.");
            }

            // Precompute support counts for edges: number of layers containing the edge
            int[] layerPresenceCounts = new int[sourceEdges.Count];
            for (int idx = 0; idx < sourceEdges.Count; idx++)
            {
                var (u, v) = sourceEdges[idx];
                int cnt = 0;
                for (int L = 0; L < Lcount; L++)
                {
                    if (ml.Layers[L][u, v] != 0) cnt++;
                }
                layerPresenceCounts[idx] = cnt;
            }

            // Compute RA scores on target layer (after holdout removal if any)
            double[] raScores = new double[sourceEdges.Count];
            for (int idx = 0; idx < sourceEdges.Count; idx++)
            {
                var (u, v) = sourceEdges[idx];
                raScores[idx] = LPAlgorithms.ResourceAllocation(tgt, u, v);
            }

            // Separate edges present vs absent in target (post-holdout) for RA summary
            var presentScores = new List<double>();
            var absentScores = new List<double>();
            for (int idx = 0; idx < sourceEdges.Count; idx++)
            {
                var (u, v) = sourceEdges[idx];
                if (tgt[u, v] != 0) presentScores.Add(raScores[idx]); else absentScores.Add(raScores[idx]);
            }
            double Avg(List<double> list) => list.Count == 0 ? double.NaN : list.Average();
            Console.WriteLine($"Avg RA score for edges present in target: {Avg(presentScores):F6} (n={presentScores.Count})");
            Console.WriteLine($"Avg RA score for edges absent in target: {Avg(absentScores):F6} (n={absentScores.Count})");

            // Candidate edges for prediction: source edges absent in target (post-holdout)
            var candidateIndices = Enumerable.Range(0, sourceEdges.Count)
                .Where(idx => tgt[sourceEdges[idx].u, sourceEdges[idx].v] == 0)
                .ToList();
            Console.WriteLine($"Candidate edges (present in source, absent in target after holdout): {candidateIndices.Count}");

            // Filter by support minimum
            candidateIndices = candidateIndices.Where(i => layerPresenceCounts[i] >= supportMinLayers).ToList();
            Console.WriteLine($"Candidates after supportMinLayers={supportMinLayers}: {candidateIndices.Count}");

            // Select predictions
            HashSet<(int u, int v)> predicted = new();
            if (raTopK.HasValue && raTopK.Value > 0)
            {
                var top = candidateIndices
                    .OrderByDescending(i => raScores[i])
                    .Take(raTopK.Value)
                    .ToList();
                foreach (var i in top) predicted.Add(sourceEdges[i]);
                Console.WriteLine($"Predicted edges using top-K RA (K={raTopK.Value}).");
            }
            else
            {
                foreach (var i in candidateIndices)
                {
                    if (raScores[i] >= raMinScore) predicted.Add(sourceEdges[i]);
                }
                Console.WriteLine($"Predicted edges using threshold raMinScore={raMinScore:F6} (predicted={predicted.Count}).");
            }

            // Evaluation if holdout applied (true positives are removedEdges)
            double precision = double.NaN, recall = double.NaN, f1 = double.NaN;
            if (holdoutFrac.HasValue && removedEdges.Count > 0)
            {
                int tp = predicted.Count(e => removedEdges.Contains(e));
                int fp = predicted.Count - tp;
                int fn = removedEdges.Count - tp;
                precision = (tp + fp) == 0 ? double.NaN : (double)tp / (tp + fp);
                recall = removedEdges.Count == 0 ? double.NaN : (double)tp / removedEdges.Count;
                f1 = (precision > 0 && recall > 0) ? 2 * precision * recall / (precision + recall) : double.NaN;
                Console.WriteLine($"Holdout evaluation: tp={tp}, fp={fp}, fn={fn}, precision={precision:F4}, recall={recall:F4}, F1={f1:F4}");
            }

            // OUTPUT CSV of all source edges with metrics
            string csvPath = outputPrefix + "_link_prediction.csv";
            using (var w = new StreamWriter(csvPath))
            {
                w.WriteLine("u,v,inSource,inTargetPostHoldout,supportCount,supportFrac,RA,predicted,wasHoldout" + (holdoutFrac.HasValue ? ",tp" : ""));
                for (int idx = 0; idx < sourceEdges.Count; idx++)
                {
                    var (u, v) = sourceEdges[idx];
                    bool inTarget = tgt[u, v] != 0;
                    int supCnt = layerPresenceCounts[idx];
                    double supFrac = (double)supCnt / Lcount;
                    double ra = raScores[idx];
                    bool isPred = predicted.Contains((u, v));
                    bool wasHold = holdoutFrac.HasValue && removedEdges.Contains((u, v));
                    string tpFlag = holdoutFrac.HasValue ? (wasHold && isPred ? "1" : "0") : "";
                    w.WriteLine(string.Join(',', new[]
                    {
                        u.ToString(), v.ToString(), "1", inTarget ? "1" : "0", supCnt.ToString(), supFrac.ToString("F6", CultureInfo.InvariantCulture),
                        ra.ToString("F6", CultureInfo.InvariantCulture), isPred ? "1" : "0", wasHold ? "1" : "0", tpFlag
                    }));
                }
            }
            Console.WriteLine($"Saved prediction metrics to {csvPath}");

            // Save summary
            using (var w = new StreamWriter(outputPrefix + "_summary.txt"))
            {
                w.WriteLine($"Layers: {Lcount}, Nodes: {N}");
                w.WriteLine($"SourceLayer={sourceLayer}, TargetLayer={targetLayer}");
                w.WriteLine($"SourceEdges={sourceEdges.Count}, EmpiricalConfidence={ruleConfidence:F6}");
                w.WriteLine($"AvgRA_present={Avg(presentScores):F6}, AvgRA_absent={Avg(absentScores):F6}");
                w.WriteLine($"SupportMinLayers={supportMinLayers}");
                if (raTopK.HasValue) w.WriteLine($"Selection=TopK, K={raTopK.Value}"); else w.WriteLine($"Selection=Threshold, raMinScore={raMinScore:F6}");
                w.WriteLine($"PredictedEdges={predicted.Count}");
                if (holdoutFrac.HasValue)
                {
                    w.WriteLine($"HoldoutFrac={holdoutFrac.Value:F4}, RemovedEdges={removedEdges.Count}");
                    w.WriteLine($"Precision={(double.IsNaN(precision)?"" : precision.ToString("F6", CultureInfo.InvariantCulture))}, Recall={(double.IsNaN(recall)?"" : recall.ToString("F6", CultureInfo.InvariantCulture))}, F1={(double.IsNaN(f1)?"" : f1.ToString("F6", CultureInfo.InvariantCulture))}");
                }
                w.WriteLine($"Seed={seed}");
            }
            Console.WriteLine($"Saved summary to {outputPrefix}_summary.txt");

            Console.WriteLine("Experiment complete.");
        }
    }*/
}