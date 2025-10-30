// Udělat flattening a na něm udělat ty komunity a modularitu, je třeba si vybrat nějaké vrstvy na kterých to chci spojit

// NOTE PRO PŘIŠTĚ: Pak je třeba tu flattened network vzít a vizualizovat 
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MAS2;

namespace MAS2
{
    public class Cviko7
    {
        // Usage: Cviko7 <layerFile1> <layerFile2> ... [--sharedMinFrac 0.5] [--edgeSharedMinLayers auto] [--uniqueMaxLayers 1] [--maxUniqueFrac 0.4]
        // If no args provided, looks for default .edges files under bin/Debug/net8.0.
        public static void Main(string[] args)
        {
            var defaultFiles = new List<string>
            {
                Path.Combine("bin", "Debug", "net8.0", "Kaktovi.edges"),
                Path.Combine("bin", "Debug", "net8.0", "Venetie.edges"),
                Path.Combine("bin", "Debug", "net8.0", "Wainwright.edges")
            };

            // Simple CLI flags
            double minSharedFrac = 0.5;     // keep layers with at least this fraction of edges that are shared widely
            int? edgeSharedMinLayersOpt = null; // if null, will default to ceil(0.5*L)
            int uniqueMaxLayers = 1;        // edges counted as "too unique" if they appear in <= this many layers
            double maxUniqueFrac = 0.4;     // drop layers whose unique-fraction exceeds this

            var layerFiles = new List<string>();
            for (int i = 0; i < (args?.Length ?? 0); i++)
            {
                var a = args[i];
                if (a == "--sharedMinFrac" && i + 1 < args.Length && double.TryParse(args[i + 1], out var f)) { minSharedFrac = f; i++; continue; }
                if (a == "--edgeSharedMinLayers" && i + 1 < args.Length && int.TryParse(args[i + 1], out var k)) { edgeSharedMinLayersOpt = k; i++; continue; }
                if (a == "--uniqueMaxLayers" && i + 1 < args.Length && int.TryParse(args[i + 1], out var u)) { uniqueMaxLayers = u; i++; continue; }
                if (a == "--maxUniqueFrac" && i + 1 < args.Length && double.TryParse(args[i + 1], out var mf)) { maxUniqueFrac = mf; i++; continue; }
                layerFiles.Add(a);
            }
            if (layerFiles.Count == 0) layerFiles = defaultFiles;

            // Filter only existing files
            var existing = layerFiles.Where(f => File.Exists(f)).ToList();
            if (existing.Count == 0)
            {
                Console.WriteLine("No layer files found. Provide file paths as arguments or place suitable .edges files in the working directory.");
                return;
            }

            Console.WriteLine($"Loading {existing.Count} layer files (multilayer edge format)...");
            var ml = MultilayerNetwork.LoadFromMultilayerFiles(existing, ' ', true);
            Console.WriteLine($"Loaded multilayer network with {ml.Layers.Count} layers and {ml.NodeCount} nodes.");

            // ---- Save full-network flattenings for comparison ----
            var flatAllUnweighted = ml.FlattenUnweighted();
            var flatAllWeighted = ml.FlattenWeighted(bySum: true);
            SaveEdgeListCsv(flatAllUnweighted, "cviko7_flatten_unweighted_all.csv");
            SaveEdgeListCsv(flatAllWeighted, "cviko7_flatten_weighted_all.csv");
            Console.WriteLine("Saved full-network flattened edge lists: cviko7_flatten_unweighted_all.csv and cviko7_flatten_weighted_all.csv");

            int L = ml.Layers.Count;
            int N = ml.NodeCount;
            int edgeSharedMinLayers = edgeSharedMinLayersOpt ?? Math.Max(2, (int)Math.Ceiling(0.5 * L));

            // Use built-in method to analyze and select layers
            var selected = ml.SelectLayersByEdgeOverlap(minSharedFrac, edgeSharedMinLayers, uniqueMaxLayers, maxUniqueFrac, out var stats);

            string summaryCsv = "layer_overlap_summary.csv";
            using (var w = new StreamWriter(summaryCsv))
            {
                w.WriteLine("Layer,Edges,SharedEdges,SharedFrac,UniqueEdges,UniqueFrac,Params(edgeSharedMinLayers,uniqueMaxLayers,minSharedFrac,maxUniqueFrac)");
                foreach (var s in stats)
                {
                    w.WriteLine(string.Join(',', new[]
                    {
                        s.Layer.ToString(), s.Edges.ToString(), s.SharedEdges.ToString(), s.SharedFrac.ToString("F6"), s.UniqueEdges.ToString(), s.UniqueFrac.ToString("F6"),
                        $"({edgeSharedMinLayers},{uniqueMaxLayers},{minSharedFrac:F2},{maxUniqueFrac:F2})"
                    }));
                }
            }
            Console.WriteLine($"Saved layer overlap summary to {summaryCsv}.");
            Console.WriteLine($"Selected layers: {string.Join(",", selected)}");
            File.WriteAllText("selected_layers.txt", string.Join(',', selected));

            // ---- Flatten selected layers (unweighted union) ----
            var flat = ml.FlattenLayers(selected, weighted: false);
            string flatCsv = "cviko7_flatten_unweighted_selected.csv";
            SaveEdgeListCsv(flat, flatCsv);
            Console.WriteLine($"Saved flattened edge list to {flatCsv}.");

            // ---- Community detection (Label Propagation) and modularity ----
            var result = ml.CommunitiesAndModularity(flat, maxIter: 100, seed: 42);
            var labels = result.labels;
            double modularity = result.modularity;
            Console.WriteLine($"Communities: {result.communities.Count}, Modularity Q = {modularity:F6}");

            // Save outputs
            using (var w = new StreamWriter("communities.csv"))
            {
                w.WriteLine("Node,Community");
                for (int i = 0; i < labels.Length; i++) w.WriteLine($"{i},{labels[i]}");
            }
            File.WriteAllText("modularity.txt", $"Q={modularity:F6}, communities={labels.Distinct().Count()}\n");

            // ---- Extra: top-2 layers by number of shared edges, flatten and community detection ----
            // "Shared" is defined using the same edgeSharedMinLayers threshold used above.
            var top2 = stats
                .OrderByDescending(s => s.SharedEdges)
                .ThenByDescending(s => s.SharedFrac)
                .Take(2)
                .Select(s => s.Layer)
                .ToList();

            if (top2.Count >= 1)
            {
                Console.WriteLine($"Top layers by shared edges: {string.Join(",", top2)}");
                var flatTop = ml.FlattenLayers(top2, weighted: false);
                string flatTopCsv = "cviko7_flatten_unweighted_top2_shared.csv";
                SaveEdgeListCsv(flatTop, flatTopCsv);
                Console.WriteLine($"Saved flattened edge list (top2 shared) to {flatTopCsv}.")
                ;

                var resTop = ml.CommunitiesAndModularity(flatTop, maxIter: 100, seed: 42);
                Console.WriteLine($"[Top2] Communities: {resTop.communities.Count}, Modularity Q = {resTop.modularity:F6}");

                using (var w2 = new StreamWriter("communities_top2.csv"))
                {
                    w2.WriteLine("Node,Community");
                    for (int i = 0; i < resTop.labels.Length; i++) w2.WriteLine($"{i},{resTop.labels[i]}");
                }
                File.WriteAllText("modularity_top2.txt", $"Q={resTop.modularity:F6}, communities={resTop.communities.Count}\n");
            }
        }

        private static void SaveEdgeListCsv(DokSparseMatrix<int> A, string path)
        {
            using var w = new StreamWriter(path);
            int n = A.Rows;
            w.WriteLine("Source,target,weight");
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    int wgt = A[i, j];
                    if (wgt != 0)
                    {
                        w.WriteLine($"{i},{j},{wgt}");
                    }
                }
            }
        }

        // Removed local community and modularity helpers in favor of MultilayerNetwork methods
    }
}