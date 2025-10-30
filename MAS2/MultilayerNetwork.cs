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

        // ---------- New measures: Relevance and Exclusive Relevance ----------

        /// <summary>
        /// Relevance of a single layer for a node, set-based as in the slides.
        /// relevance(a, L) = |neighborhood(a, L)| / |neighborhood(a, allLayers)|
        /// If the node has no neighbors in the union graph, returns 0.
        /// </summary>
        public double LayerRelevance(int nodeIndex, int layerIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= NodeCount) throw new ArgumentOutOfRangeException(nameof(nodeIndex));
            if (layerIndex < 0 || layerIndex >= Layers.Count) throw new ArgumentOutOfRangeException(nameof(layerIndex));

            var allLayers = Enumerable.Range(0, Layers.Count);
            var allNeighbors = GetNeighbors(nodeIndex, allLayers);
            int denom = allNeighbors.Count;
            if (denom == 0) return 0.0;

            var onLayer = GetNeighbors(nodeIndex, new[] { layerIndex });
            return (double)onLayer.Count / denom;
        }

        /// <summary>
        /// Exclusive relevance of a single layer for a node, set-based.
        /// ER(i, layer) = |exclusiveNeighbors(i, {layer})| / |neighbors(i, allLayers)|.
        /// If the node has no neighbors in the union graph, returns 0.
        /// </summary>
        public double ExclusiveLayerRelevance(int nodeIndex, int layerIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= NodeCount) throw new ArgumentOutOfRangeException(nameof(nodeIndex));
            if (layerIndex < 0 || layerIndex >= Layers.Count) throw new ArgumentOutOfRangeException(nameof(layerIndex));

            var allLayers = Enumerable.Range(0, Layers.Count);
            var allNeighbors = GetNeighbors(nodeIndex, allLayers);
            int denom = allNeighbors.Count;
            if (denom == 0) return 0.0;

            var exclusive = ExclusiveNeighborhood(nodeIndex, new[] { layerIndex });
            return (double)exclusive.Count / denom;
        }

        // ---------- Flattening: unweighted and weighted ----------

        /// <summary>
        /// Unweighted flattening (union) of all layers.
        /// An edge exists if it exists in at least one layer; weight is 1.
        /// </summary>
        public DokSparseMatrix<int> FlattenUnweighted()
        {
            if (Layers.Count == 0) return new DokSparseMatrix<int>(0, 0);
            int n = NodeCount;
            var flat = new DokSparseMatrix<int>(n, n);
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    int present = 0;
                    for (int li = 0; li < Layers.Count; li++)
                    {
                        if (Layers[li][i, j] != 0) { present = 1; break; }
                    }
                    if (present != 0)
                    {
                        flat[i, j] = 1;
                    }
                }
            }
            return flat;
        }

        /// <summary>
        /// Weighted flattening (sum) of all layers.
        /// If bySum is true, sums actual integer weights; otherwise uses layer-count multiplicity.
        /// </summary>
        public DokSparseMatrix<int> FlattenWeighted(bool bySum = true)
        {
            if (Layers.Count == 0) return new DokSparseMatrix<int>(0, 0);
            int n = NodeCount;
            var flat = new DokSparseMatrix<int>(n, n);
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    int w = 0;
                    if (bySum)
                    {
                        for (int li = 0; li < Layers.Count; li++) w += Layers[li][i, j];
                    }
                    else
                    {
                        for (int li = 0; li < Layers.Count; li++) if (Layers[li][i, j] != 0) w++;
                    }
                    if (w != 0) flat[i, j] = w;
                }
            }
            return flat;
        }

        // ---------- Random walk and occupation centrality ----------

        /// <summary>
        /// Runs a simple random walk over the multilayer network and returns occupation centrality
        /// (fraction of time spent at each node).
        /// Step rule: choose a layer according to "layerWeights" (uniform if null), then move to a random neighbor
        /// in that layer; if no neighbor exists in the chosen layer, the walker stays in place. If the node has
        /// no neighbors in any layer, occasional random jump avoids trapping.
        /// </summary>
        public double[] OccupationCentrality(int steps = 100_000, int? startNode = null, double[] layerWeights = null, int seed = 42)
        {
            int n = NodeCount;
            var occ = new double[n];
            if (n == 0 || steps <= 0) return occ;

            int Lcount = Layers.Count;
            if (Lcount == 0) return occ;

            // Normalize weights
            double[] weights = new double[Lcount];
            if (layerWeights == null || layerWeights.Length != Lcount)
            {
                for (int i = 0; i < Lcount; i++) weights[i] = 1.0 / Lcount;
            }
            else
            {
                double sumw = layerWeights.Sum();
                if (sumw <= 0) { for (int i = 0; i < Lcount; i++) weights[i] = 1.0 / Lcount; }
                else { for (int i = 0; i < Lcount; i++) weights[i] = layerWeights[i] / sumw; }
            }

            var rng = new Random(seed);
            int cur = startNode.HasValue ? Math.Clamp(startNode.Value, 0, n - 1) : rng.Next(n);

            // Precompute CDF for layer selection
            double[] cdf = new double[Lcount];
            double acc = 0.0;
            for (int i = 0; i < Lcount; i++) { acc += weights[i]; cdf[i] = acc; }

            for (int t = 0; t < steps; t++)
            {
                occ[cur] += 1.0;

                // choose layer
                double r = rng.NextDouble();
                int chosen = 0;
                while (chosen < Lcount && r > cdf[chosen]) chosen++;
                if (chosen >= Lcount) chosen = Lcount - 1;

                // collect neighbors in chosen layer
                var L = Layers[chosen];
                var neigh = new List<int>();
                for (int j = 0; j < n; j++) if (L[cur, j] != 0) neigh.Add(j);

                if (neigh.Count > 0)
                {
                    cur = neigh[rng.Next(neigh.Count)];
                }
                else
                {
                    // fallback: try aggregate neighbors
                    var allNeigh = GetNeighbors(cur, Enumerable.Range(0, Lcount));
                    if (allNeigh.Count > 0)
                    {
                        // pick deterministic index from RNG
                        int idx = rng.Next(allNeigh.Count);
                        cur = allNeigh.ElementAt(idx);
                    }
                    else
                    {
                        // isolated: random jump
                        cur = rng.Next(n);
                    }
                }
            }

            // normalize
            for (int i = 0; i < n; i++) occ[i] /= steps;
            return occ;
        }

        // ---------- Cviko7 helpers moved here ----------

        public struct LayerOverlapStats
        {
            public int Layer;
            public int Edges;
            public int SharedEdges;
            public double SharedFrac;
            public int UniqueEdges;
            public double UniqueFrac;
        }

        /// <summary>
        /// Analyze cross-layer edge overlap and return selected layers that share many edges across layers
        /// and have few unique-only edges.
        /// </summary>
        /// <param name="minSharedFrac">Minimum fraction of a layer's edges that must be shared across >= edgeSharedMinLayers layers.</param>
        /// <param name="edgeSharedMinLayers">An edge is considered "shared" if it appears in at least this many layers. If null => ceil(0.5 * L).</param>
        /// <param name="uniqueMaxLayers">An edge is considered "too unique" if it appears in <= this many layers.</param>
        /// <param name="maxUniqueFrac">Maximum allowed fraction of unique edges in a layer.</param>
        /// <param name="summary">Per-layer statistics for diagnostics.</param>
        public List<int> SelectLayersByEdgeOverlap(double minSharedFrac, int? edgeSharedMinLayers, int uniqueMaxLayers, double maxUniqueFrac, out List<LayerOverlapStats> summary)
        {
            int Lcount = Layers.Count;
            int n = NodeCount;
            int sharedK = edgeSharedMinLayers ?? Math.Max(2, (int)Math.Ceiling(0.5 * Lcount));

            // presence[i,j] = in how many layers edge (i,j) exists; undirected so consider i<j only
            var presence = new int[n, n];
            for (int li = 0; li < Lcount; li++)
            {
                var A = Layers[li];
                for (int i = 0; i < n; i++)
                    for (int j = i + 1; j < n; j++)
                        if (A[i, j] != 0) presence[i, j]++;
            }

            summary = new List<LayerOverlapStats>(Lcount);
            for (int li = 0; li < Lcount; li++)
            {
                var A = Layers[li];
                int edgesInLayer = 0, sharedInLayer = 0, uniqueInLayer = 0;
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        if (A[i, j] != 0)
                        {
                            edgesInLayer++;
                            int p = presence[i, j];
                            if (p >= sharedK) sharedInLayer++;
                            if (p <= uniqueMaxLayers) uniqueInLayer++;
                        }
                    }
                }
                double sharedFrac = edgesInLayer == 0 ? 0.0 : (double)sharedInLayer / edgesInLayer;
                double uniqueFrac = edgesInLayer == 0 ? 0.0 : (double)uniqueInLayer / edgesInLayer;
                summary.Add(new LayerOverlapStats
                {
                    Layer = li,
                    Edges = edgesInLayer,
                    SharedEdges = sharedInLayer,
                    SharedFrac = sharedFrac,
                    UniqueEdges = uniqueInLayer,
                    UniqueFrac = uniqueFrac
                });
            }

            // Select layers meeting thresholds; if none, fallback to top by sharedFrac
            var selected = summary
                .Where(s => s.SharedFrac >= minSharedFrac && s.UniqueFrac <= maxUniqueFrac)
                .Select(s => s.Layer)
                .ToList();
            if (selected.Count == 0)
            {
                selected = summary
                    .OrderByDescending(s => s.SharedFrac)
                    .ThenBy(s => s.UniqueFrac)
                    .Take(Math.Max(1, Lcount / 2))
                    .Select(s => s.Layer)
                    .ToList();
            }
            return selected;
        }

        /// <summary>
        /// Flatten only the selected layers; if weighted=false, union with weight 1; if weighted=true, either sum or layer-count.
        /// </summary>
        public DokSparseMatrix<int> FlattenLayers(IEnumerable<int> selectedLayers, bool weighted = false, bool bySum = true)
        {
            int n = NodeCount;
            var flat = new DokSparseMatrix<int>(n, n);
            var sel = selectedLayers?.ToHashSet() ?? new HashSet<int>(Enumerable.Range(0, Layers.Count));
            if (!weighted)
            {
                foreach (var li in sel)
                {
                    if (li < 0 || li >= Layers.Count) continue;
                    var A = Layers[li];
                    for (int i = 0; i < n; i++)
                        for (int j = i + 1; j < n; j++)
                            if (A[i, j] != 0) { flat[i, j] = 1; flat[j, i] = 1; }
                }
            }
            else
            {
                foreach (var li in sel)
                {
                    if (li < 0 || li >= Layers.Count) continue;
                    var A = Layers[li];
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n; j++)
                        {
                            int add = bySum ? A[i, j] : (A[i, j] != 0 ? 1 : 0);
                            if (add != 0) flat[i, j] = flat[i, j] + add;
                        }
                }
            }
            return flat;
        }

        /// <summary>
        /// Detect communities on a flattened adjacency via Label Propagation and compute Newman-Girvan modularity.
        /// Returns node labels, list of communities (nodes per community), and modularity.
        /// </summary>
        public (int[] labels, List<List<int>> communities, double modularity) CommunitiesAndModularity(DokSparseMatrix<int> flat, int maxIter = 100, int seed = 42)
        {
            var labels = LabelPropagation(flat, maxIter, seed);
            double Q = ComputeModularity(flat, labels);
            var groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < labels.Length; i++)
            {
                int c = labels[i];
                if (!groups.TryGetValue(c, out var list)) { list = new List<int>(); groups[c] = list; }
                list.Add(i);
            }
            var communities = groups.Values.ToList();
            return (labels, communities, Q);
        }

        // Simple Label Propagation (unweighted, undirected)
        private static int[] LabelPropagation(DokSparseMatrix<int> A, int maxIter = 100, int seed = 42)
        {
            int n = A.Rows;
            var labels = new int[n];
            for (int i = 0; i < n; i++) labels[i] = i;
            var rng = new Random(seed);
            var order = Enumerable.Range(0, n).ToArray();

            bool changed = true;
            int iter = 0;
            var counts = new Dictionary<int, int>();
            while (changed && iter < maxIter)
            {
                changed = false;
                iter++;
                // shuffle processing order
                for (int k = 0; k < n; k++)
                {
                    int r = rng.Next(k, n);
                    (order[k], order[r]) = (order[r], order[k]);
                }
                foreach (var u in order)
                {
                    counts.Clear();
                    // collect neighbor labels
                    for (int v = 0; v < n; v++)
                    {
                        if (A[u, v] != 0)
                        {
                            int lab = labels[v];
                            counts[lab] = counts.TryGetValue(lab, out var c) ? c + 1 : 1;
                        }
                    }
                    if (counts.Count == 0) continue;
                    int bestLab = labels[u];
                    int bestCnt = -1;
                    foreach (var kv in counts)
                    {
                        if (kv.Value > bestCnt || (kv.Value == bestCnt && kv.Key < bestLab))
                        {
                            bestCnt = kv.Value; bestLab = kv.Key;
                        }
                    }
                    if (bestLab != labels[u]) { labels[u] = bestLab; changed = true; }
                }
            }
            return labels;
        }

        // Newman–Girvan modularity for undirected graphs
        private static double ComputeModularity(DokSparseMatrix<int> A, int[] labels)
        {
            int n = A.Rows;
            // Compute degrees and total edge weight (2m)
            var deg = new double[n];
            double twoM = 0.0;
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    int w = A[i, j];
                    if (w != 0)
                    {
                        deg[i] += w;
                        if (i < j) twoM += w;
                    }
                }
            }
            twoM *= 2.0;
            if (twoM <= 0) return 0.0;

            // Compute Q = sum_c (e_c - a_c^2)
            var commNodes = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                int c = labels[i];
                if (!commNodes.TryGetValue(c, out var list)) { list = new List<int>(); commNodes[c] = list; }
                list.Add(i);
            }

            double Q = 0.0;
            foreach (var kv in commNodes)
            {
                var nodes = kv.Value;
                double e_c_twom = 0.0; // edges within community counted once divided by (2m)
                double a_c = 0.0;      // fraction of stubs (sum deg)/(2m)
                double sumDeg = 0.0;
                foreach (var i in nodes) sumDeg += deg[i];
                a_c = sumDeg / twoM;
                for (int ix = 0; ix < nodes.Count; ix++)
                {
                    int i = nodes[ix];
                    for (int jx = ix + 1; jx < nodes.Count; jx++)
                    {
                        int j = nodes[jx];
                        int w = A[i, j];
                        if (w != 0) e_c_twom += w / twoM;
                    }
                }
                Q += (e_c_twom - a_c * a_c);
            }
            return Q;
        }
    }
}
