using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MAS2
{
    /// <summary>
    /// Helper class to generate formatted comparison tables from Cviko6 results.
    /// Can be used to create presentation-ready tables.
    /// </summary>
    public static class RelevanceTableGenerator
    {
        /// <summary>
        /// Generates a comprehensive comparison table showing top nodes across different metrics.
        /// Saves as both CSV and formatted text for easy copying to presentations.
        /// </summary>
        public static void GenerateComparisonTable(
            MultilayerNetwork ml,
            double[] occupationCentrality,
            string outputPrefix = "comparison_table")
        {
            int n = ml.NodeCount;
            int Lc = ml.Layers.Count;
            int topN = Math.Min(15, n); // Top 15 nodes

            // Get degrees for all layers
            var degPerLayer = ml.GetDegreesPerLayer();

            // Collect all data
            var data = new List<NodeMetrics>();
            for (int i = 0; i < n; i++)
            {
                var metrics = new NodeMetrics
                {
                    NodeId = i,
                    OccupationCentrality = occupationCentrality[i],
                    RelevanceByLayer = new double[Lc],
                    ExclusiveRelevanceByLayer = new double[Lc],
                    DegreeByLayer = new int[Lc]
                };

                for (int L = 0; L < Lc; L++)
                {
                    metrics.RelevanceByLayer[L] = ml.LayerRelevance(i, L);
                    metrics.ExclusiveRelevanceByLayer[L] = ml.ExclusiveLayerRelevance(i, L);
                    metrics.DegreeByLayer[L] = degPerLayer[L][i];
                }

                metrics.AvgRelevance = metrics.RelevanceByLayer.Average();
                metrics.AvgExclusiveRelevance = metrics.ExclusiveRelevanceByLayer.Average();
                metrics.TotalDegree = metrics.DegreeByLayer.Sum();

                data.Add(metrics);
            }

            // Generate CSV with full data
            string csvPath = $"{outputPrefix}.csv";
            using (var w = new StreamWriter(csvPath))
            {
                var headers = new List<string> { "Node", "OccCentrality", "TotalDegree", "AvgRelevance", "AvgExclusiveRelevance" };
                for (int L = 0; L < Lc; L++)
                {
                    headers.Add($"Deg_L{L}");
                    headers.Add($"Rel_L{L}");
                    headers.Add($"ExRel_L{L}");
                }
                w.WriteLine(string.Join(',', headers));

                foreach (var m in data.OrderByDescending(x => x.OccupationCentrality))
                {
                    var values = new List<string>
                    {
                        m.NodeId.ToString(),
                        m.OccupationCentrality.ToString("F6"),
                        m.TotalDegree.ToString(),
                        m.AvgRelevance.ToString("F4"),
                        m.AvgExclusiveRelevance.ToString("F4")
                    };
                    for (int L = 0; L < Lc; L++)
                    {
                        values.Add(m.DegreeByLayer[L].ToString());
                        values.Add(m.RelevanceByLayer[L].ToString("F4"));
                        values.Add(m.ExclusiveRelevanceByLayer[L].ToString("F4"));
                    }
                    w.WriteLine(string.Join(',', values));
                }
            }
            Console.WriteLine($"Saved comprehensive comparison table: {csvPath}");

            // Generate formatted text table for presentation
            string txtPath = $"{outputPrefix}_top{topN}.txt";
            using (var w = new StreamWriter(txtPath))
            {
                w.WriteLine("=" .PadRight(80, '='));
                w.WriteLine($"TOP {topN} NODES - MULTILAYER NETWORK ANALYSIS");
                w.WriteLine("=".PadRight(80, '='));
                w.WriteLine();

                // Top by Occupation Centrality
                w.WriteLine("TOP NODES BY OCCUPATION CENTRALITY:");
                w.WriteLine("-".PadRight(80, '-'));
                w.WriteLine($"{"Rank",-6} {"Node",-6} {"OccCent",-10} {"TotDeg",-8} {"AvgRel",-8} {"AvgExRel",-10}");
                w.WriteLine("-".PadRight(80, '-'));
                int rank = 1;
                foreach (var m in data.OrderByDescending(x => x.OccupationCentrality).Take(topN))
                {
                    w.WriteLine($"{rank,-6} {m.NodeId,-6} {m.OccupationCentrality,-10:F6} {m.TotalDegree,-8} {m.AvgRelevance,-8:F4} {m.AvgExclusiveRelevance,-10:F4}");
                    rank++;
                }
                w.WriteLine();

                // Top by Average Relevance
                w.WriteLine("TOP NODES BY AVERAGE RELEVANCE:");
                w.WriteLine("-".PadRight(80, '-'));
                w.WriteLine($"{"Rank",-6} {"Node",-6} {"AvgRel",-10} {"OccCent",-10} {"TotDeg",-8}");
                w.WriteLine("-".PadRight(80, '-'));
                rank = 1;
                foreach (var m in data.OrderByDescending(x => x.AvgRelevance).Take(topN))
                {
                    w.WriteLine($"{rank,-6} {m.NodeId,-6} {m.AvgRelevance,-10:F6} {m.OccupationCentrality,-10:F6} {m.TotalDegree,-8}");
                    rank++;
                }
                w.WriteLine();

                // Per-layer analysis
                for (int L = 0; L < Lc; L++)
                {
                    w.WriteLine($"LAYER {L} - TOP NODES BY RELEVANCE:");
                    w.WriteLine("-".PadRight(80, '-'));
                    w.WriteLine($"{"Rank",-6} {"Node",-6} {"Relevance",-12} {"ExclusiveRel",-14} {"Degree",-8}");
                    w.WriteLine("-".PadRight(80, '-'));
                    rank = 1;
                    foreach (var m in data.OrderByDescending(x => x.RelevanceByLayer[L]).Take(topN))
                    {
                        w.WriteLine($"{rank,-6} {m.NodeId,-6} {m.RelevanceByLayer[L],-12:F6} {m.ExclusiveRelevanceByLayer[L],-14:F6} {m.DegreeByLayer[L],-8}");
                        rank++;
                    }
                    w.WriteLine();
                }

                w.WriteLine("=" .PadRight(80, '='));
                w.WriteLine("Legend:");
                w.WriteLine("  OccCent  = Occupation Centrality (from random walk)");
                w.WriteLine("  TotDeg   = Total degree across all layers");
                w.WriteLine("  AvgRel   = Average relevance across all layers");
                w.WriteLine("  AvgExRel = Average exclusive relevance across all layers");
                w.WriteLine("  Relevance = Fraction of node's neighbors in this layer");
                w.WriteLine("  ExclusiveRel = Fraction of neighbors unique to this layer");
                w.WriteLine("=".PadRight(80, '='));
            }
            Console.WriteLine($"Saved formatted comparison table: {txtPath}");
        }

        private class NodeMetrics
        {
            public int NodeId { get; set; }
            public double OccupationCentrality { get; set; }
            public double[] RelevanceByLayer { get; set; }
            public double[] ExclusiveRelevanceByLayer { get; set; }
            public int[] DegreeByLayer { get; set; }
            public double AvgRelevance { get; set; }
            public double AvgExclusiveRelevance { get; set; }
            public int TotalDegree { get; set; }
        }
    }
}
