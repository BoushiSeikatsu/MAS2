using System;
using System.Collections.Generic;
using System.Linq;

namespace MAS2
{
    /// <summary>
    /// Simple multilayer network container where each layer is stored as a DokSparseMatrix<int>.
    /// Provides methods to compute per-node measures per layer and aggregated across layers.
    /// </summary>
    public class MultilayerNetwork
    {
        public List<DokSparseMatrix<int>> Layers { get; }

        public int NodeCount => Layers.Count == 0 ? 0 : Layers[0].Rows;

        public MultilayerNetwork(IEnumerable<DokSparseMatrix<int>> layers)
        {
            Layers = layers.ToList();
            if (Layers.Count > 0)
            {
                // Ensure square and equal sizes
                int n = Layers[0].Rows;
                foreach (var L in Layers)
                {
                    if (L.Rows != L.Columns) throw new ArgumentException("Layer must be a square matrix");
                    if (L.Rows != n) throw new ArgumentException("All layers must have same node count");
                }
            }
        }

        public static MultilayerNetwork LoadFromFiles(IEnumerable<string> paths, char delimiter = ';')
        {
            var layers = new List<DokSparseMatrix<int>>();
            foreach (var p in paths)
            {
                var m = DokSparseMatrix<int>.FromFile(p, s => int.Parse(s), delimiter);
                layers.Add(m);
            }
            return new MultilayerNetwork(layers);
        }

        /// <summary>
        /// Loads a multilayer network from a single file where each line encodes an edge with node+layer pairs.
        /// Expected (whitespace separated) format per line: nodeA layerA nodeB layerB [weight]
        /// - nodeA/nodeB and layerA/layerB are parsed as integers
        /// - weight is optional (parsed as double and rounded to int); if missing, weight defaults to 1
        /// Only intra-layer edges (layerA == layerB) are added to the corresponding layer matrix.
        /// Inter-layer edges are ignored by default but counted and reported to the console.
        /// </summary>
        public static MultilayerNetwork LoadFromMultilayerEdgeFile(string path, char? delimiter = null, bool ignoreInterLayerEdges = true)
        {
            var nodeSet = new HashSet<int>();
            var layerSet = new HashSet<int>();
            var parsed = new List<(int u, int la, int v, int lb, int w)>();

            int ignoredInter = 0;

            foreach (var raw in System.IO.File.ReadLines(path))
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith("#") || line.StartsWith("//")) continue;

                string[] parts;
                if (delimiter.HasValue)
                    parts = line.Split(new[] { delimiter.Value }, StringSplitOptions.RemoveEmptyEntries);
                else
                    parts = System.Text.RegularExpressions.Regex.Split(line, "\\s+");

                // normalize parts (trim)
                parts = parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
                if (parts.Length < 4) continue; // not enough data

                // parse node/layer tokens
                if (!int.TryParse(parts[0], out var u)) continue;
                if (!int.TryParse(parts[1], out var la)) continue;
                if (!int.TryParse(parts[2], out var v)) continue;
                if (!int.TryParse(parts[3], out var lb)) continue;

                double weight = 1.0;
                if (parts.Length >= 5)
                    double.TryParse(parts[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out weight);

                int wi = (int)Math.Round(weight);

                nodeSet.Add(u); nodeSet.Add(v);
                layerSet.Add(la); layerSet.Add(lb);

                parsed.Add((u, la, v, lb, wi));
            }

            // Build node id -> index map
            var nodes = nodeSet.OrderBy(x => x).ToArray();
            var nodeToIdx = new Dictionary<int, int>();
            for (int i = 0; i < nodes.Length; i++) nodeToIdx[nodes[i]] = i;

            var layersIds = layerSet.OrderBy(x => x).ToArray();
            var layerIdToIndex = new Dictionary<int, int>();
            for (int i = 0; i < layersIds.Length; i++) layerIdToIndex[layersIds[i]] = i;

            int n = nodes.Length;
            var layerMatrices = new DokSparseMatrix<int>[layersIds.Length];
            for (int i = 0; i < layersIds.Length; i++) layerMatrices[i] = new DokSparseMatrix<int>(n, n);

            // fill matrices (only intra-layer edges go to per-layer adjacency)
            foreach (var e in parsed)
            {
                if (e.la != e.lb)
                {
                    ignoredInter++;
                    if (ignoreInterLayerEdges) continue;
                }

                int iu = nodeToIdx[e.u];
                int iv = nodeToIdx[e.v];
                int layerIdx = layerIdToIndex[e.la];

                // sum duplicate weights
                int current = layerMatrices[layerIdx][iu, iv];
                layerMatrices[layerIdx][iu, iv] = current + e.w;
                // if undirected, also set symmetric
                int curr2 = layerMatrices[layerIdx][iv, iu];
                layerMatrices[layerIdx][iv, iu] = curr2 + e.w;
            }

            if (ignoredInter > 0 && ignoreInterLayerEdges)
                Console.WriteLine($"LoadFromMultilayerEdgeFile: ignored {ignoredInter} inter-layer edges from {path}");

            return new MultilayerNetwork(layerMatrices);
        }

        /// <summary>
        /// Load multiple multilayer edge files and merge them into a single multilayer network.
        /// Each file is expected to contain lines with nodeA layerA nodeB layerB [weight].
        /// </summary>
        public static MultilayerNetwork LoadFromMultilayerFiles(IEnumerable<string> paths, char? delimiter = null, bool ignoreInterLayerEdges = true)
        {
            // read all parsed edges from all files then reuse single-file loader logic by writing to a temp file? Simpler: accumulate parsed edges in-memory.
            var nodeSet = new HashSet<int>();
            var layerSet = new HashSet<int>();
            var parsed = new List<(int u, int la, int v, int lb, int w)>();

            foreach (var path in paths)
            {
                foreach (var raw in System.IO.File.ReadLines(path))
                {
                    var line = raw.Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    if (line.StartsWith("#") || line.StartsWith("//")) continue;

                    string[] parts;
                    if (delimiter.HasValue)
                        parts = line.Split(new[] { delimiter.Value }, StringSplitOptions.RemoveEmptyEntries);
                    else
                        parts = System.Text.RegularExpressions.Regex.Split(line, "\\s+");

                    parts = parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
                    if (parts.Length < 4) continue;
                    if (!int.TryParse(parts[0], out var u)) continue;
                    if (!int.TryParse(parts[1], out var la)) continue;
                    if (!int.TryParse(parts[2], out var v)) continue;
                    if (!int.TryParse(parts[3], out var lb)) continue;

                    double weight = 1.0;
                    if (parts.Length >= 5)
                        double.TryParse(parts[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out weight);
                    int wi = (int)Math.Round(weight);

                    nodeSet.Add(u); nodeSet.Add(v);
                    layerSet.Add(la); layerSet.Add(lb);
                    parsed.Add((u, la, v, lb, wi));
                }
            }

            var nodes = nodeSet.OrderBy(x => x).ToArray();
            var nodeToIdx = new Dictionary<int, int>();
            for (int i = 0; i < nodes.Length; i++) nodeToIdx[nodes[i]] = i;

            var layersIds = layerSet.OrderBy(x => x).ToArray();
            var layerIdToIndex = new Dictionary<int, int>();
            for (int i = 0; i < layersIds.Length; i++) layerIdToIndex[layersIds[i]] = i;

            int n = nodes.Length;
            var layerMatrices = new DokSparseMatrix<int>[layersIds.Length];
            for (int i = 0; i < layersIds.Length; i++) layerMatrices[i] = new DokSparseMatrix<int>(n, n);

            int ignoredInter = 0;
            foreach (var e in parsed)
            {
                if (e.la != e.lb)
                {
                    ignoredInter++;
                    if (ignoreInterLayerEdges) continue;
                }

                int iu = nodeToIdx[e.u];
                int iv = nodeToIdx[e.v];
                int layerIdx = layerIdToIndex[e.la];
                int current = layerMatrices[layerIdx][iu, iv];
                layerMatrices[layerIdx][iu, iv] = current + e.w;
                int curr2 = layerMatrices[layerIdx][iv, iu];
                layerMatrices[layerIdx][iv, iu] = curr2 + e.w;
            }

            if (ignoredInter > 0 && ignoreInterLayerEdges)
                Console.WriteLine($"LoadFromMultilayerFiles: ignored {ignoredInter} inter-layer edges across provided files");

            return new MultilayerNetwork(layerMatrices);
        }

        // Per-layer degrees: returns list of int[] (one array per layer)
        public List<int[]> GetDegreesPerLayer()
        {
            var result = new List<int[]>();
            foreach (var L in Layers)
            {
                int n = L.Rows;
                var deg = new int[n];
                for (int i = 0; i < n; i++)
                {
                    int d = 0;
                    for (int j = 0; j < n; j++)
                        if (L[i, j] != 0) d++;
                    deg[i] = d;
                }
                result.Add(deg);
            }
            return result;
        }

        // Clustering coefficients per layer
        public List<double[]> GetClusteringPerLayer()
        {
            var result = new List<double[]>();
            foreach (var L in Layers)
            {
                int n = L.Rows;
                var coeff = new double[n];
                for (int i = 0; i < n; i++)
                {
                    var neighbors = new List<int>();
                    for (int j = 0; j < n; j++) if (L[i, j] != 0) neighbors.Add(j);
                    int k = neighbors.Count;
                    if (k < 2)
                    {
                        coeff[i] = 0.0;
                        continue;
                    }
                    int connected = 0;
                    for (int a = 0; a < neighbors.Count; a++)
                    {
                        for (int b = a + 1; b < neighbors.Count; b++)
                        {
                            if (L[neighbors[a], neighbors[b]] != 0) connected++;
                        }
                    }
                    coeff[i] = (2.0 * connected) / (k * (k - 1));
                }
                result.Add(coeff);
            }
            return result;
        }

        // Aggregate adjacency (sum of layers)
        public DokSparseMatrix<int> GetAggregateMatrix()
        {
            if (Layers.Count == 0) return new DokSparseMatrix<int>(0, 0);
            int n = Layers[0].Rows;
            var agg = new DokSparseMatrix<int>(n, n);
            foreach (var L in Layers)
            {
                for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    int v = agg[i, j] + L[i, j];
                    agg[i, j] = v;
                }
            }
            return agg;
        }

        // Aggregate degree (sum of degrees across layers)
        public int[] GetAggregateDegree()
        {
            var agg = new int[NodeCount];
            foreach (var deg in GetDegreesPerLayer())
            {
                for (int i = 0; i < NodeCount; i++) agg[i] += deg[i];
            }
            return agg;
        }

        // Multiplex degree: number of layers in which node has degree > 0
        public int[] GetMultiplexDegree()
        {
            var res = new int[NodeCount];
            var per = GetDegreesPerLayer();
            for (int i = 0; i < NodeCount; i++)
            {
                int cnt = 0;
                foreach (var d in per) if (d[i] > 0) cnt++;
                res[i] = cnt;
            }
            return res;
        }

        // --- Multilayer measures requested ---

        // Get neighbors of node (by node index) considering only the provided layer indices (positions in Layers list)
        public HashSet<int> GetNeighbors(int nodeIndex, IEnumerable<int> layerIndices)
        {
            if (nodeIndex < 0 || nodeIndex >= NodeCount) throw new ArgumentOutOfRangeException(nameof(nodeIndex));
            var res = new HashSet<int>();
            foreach (var li in layerIndices)
            {
                if (li < 0 || li >= Layers.Count) continue;
                var L = Layers[li];
                for (int j = 0; j < L.Columns; j++)
                {
                    if (L[nodeIndex, j] != 0) res.Add(j);
                }
            }
            return res;
        }

        // Neighborhood centrality: size of neighbor set on layers L
        public int NeighborhoodCentrality(int nodeIndex, IEnumerable<int> layerIndices)
        {
            return GetNeighbors(nodeIndex, layerIndices).Count;
        }

        // Degree centrality on layers L: total number of (a,l,a',l') edges with l,l' in L.
        // With per-layer intra-layer adjacency this is the sum of degrees across the selected layers.
        public int DegreeCentrality(int nodeIndex, IEnumerable<int> layerIndices)
        {
            if (nodeIndex < 0 || nodeIndex >= NodeCount) throw new ArgumentOutOfRangeException(nameof(nodeIndex));
            int sum = 0;
            foreach (var li in layerIndices)
            {
                if (li < 0 || li >= Layers.Count) continue;
                var L = Layers[li];
                for (int j = 0; j < L.Columns; j++) if (L[nodeIndex, j] != 0) sum++;
            }
            return sum;
        }

        // Connective redundancy: 1 - neighborhood(a,L) / degree(a,L)
        public double ConnectiveRedundancy(int nodeIndex, IEnumerable<int> layerIndices)
        {
            int deg = DegreeCentrality(nodeIndex, layerIndices);
            if (deg == 0) return 0.0;
            int neigh = NeighborhoodCentrality(nodeIndex, layerIndices);
            return 1.0 - ((double)neigh / (double)deg);
        }

        // Exclusive neighborhood: neighbors(a,L) \ neighbors(a, complement(L))
        public HashSet<int> ExclusiveNeighborhood(int nodeIndex, IEnumerable<int> layerIndices)
        {
            var sel = new HashSet<int>(layerIndices);
            var all = Enumerable.Range(0, Layers.Count);
            var complement = all.Where(i => !sel.Contains(i));
            var neighSel = GetNeighbors(nodeIndex, sel);
            var neighOther = GetNeighbors(nodeIndex, complement);
            neighSel.ExceptWith(neighOther);
            return neighSel;
        }

        // Degree deviation: standard deviation of degrees of node across the provided layers
        public double DegreeDeviation(int nodeIndex, IEnumerable<int> layerIndices)
        {
            var degrees = new List<double>();
            foreach (var li in layerIndices)
            {
                if (li < 0 || li >= Layers.Count) continue;
                var L = Layers[li];
                int d = 0;
                for (int j = 0; j < L.Columns; j++) if (L[nodeIndex, j] != 0) d++;
                degrees.Add(d);
            }
            if (degrees.Count == 0) return 0.0;
            double mean = degrees.Average();
            double sumsq = degrees.Select(x => (x - mean) * (x - mean)).Sum();
            double variance = sumsq / degrees.Count; // population variance
            return Math.Sqrt(variance);
        }
    }
}
