using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MAS2;

//Kaskádový model, jeden vrchol může ovlivnit pouze jednou, jakmile jednou ovlivním už nikdy nemužu ovlivnit znovu
/*public class PrvniProjekts
{
    enum EdgeType
    {
        A2Q = 1, // Answer to Question
        C2Q = 2, // Comment to Question
        C2A = 4  // Comment to Answer
    }

    struct TemporalEdge
    {
        public int Source;
        public int Target;
        public long Timestamp;
        public EdgeType Type;
    }

    public static void Main(string[] args)
    {
        // Paths to the datasets
        string pathA2Q = @"E:\C#Projects\MAS2\MAS2\data\sx-superuser-a2q.txt";
        string pathC2Q = @"E:\C#Projects\MAS2\MAS2\data\sx-superuser-c2q.txt";
        string pathC2A = @"E:\C#Projects\MAS2\MAS2\data\sx-superuser-c2a.txt";
        
        // Output directory
        string outputDir = @"E:\C#Projects\MAS2\MAS2\data\PrvniProjekt";
        Directory.CreateDirectory(outputDir);

        Console.WriteLine("--- Data Loading and Pre-processing (Multiplex) ---");

        // 1. Data Loading and Parsing
        // ID Mapping: Original ID -> Internal ID (0 to N-1)
        var originalToInternal = new Dictionary<int, int>();
        var edges = new List<TemporalEdge>();
        int nextId = 0;

        void LoadFile(string path, EdgeType type)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"Warning: File not found: {path}");
                return;
            }
            Console.WriteLine($"Loading {type} from {Path.GetFileName(path)}...");
            int count = 0;
            foreach (var line in File.ReadLines(path))
            {
                var parts = line.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) continue;

                if (int.TryParse(parts[0], out int uOrig) &&
                    int.TryParse(parts[1], out int vOrig) &&
                    long.TryParse(parts[2], out long timestamp))
                {
                    // Map IDs
                    if (!originalToInternal.TryGetValue(uOrig, out int u))
                    {
                        u = nextId++;
                        originalToInternal[uOrig] = u;
                    }
                    if (!originalToInternal.TryGetValue(vOrig, out int v))
                    {
                        v = nextId++;
                        originalToInternal[vOrig] = v;
                    }

                    edges.Add(new TemporalEdge { Source = u, Target = v, Timestamp = timestamp, Type = type });
                    count++;
                }
            }
            Console.WriteLine($"  Loaded {count} edges.");
        }

        LoadFile(pathA2Q, EdgeType.A2Q);
        LoadFile(pathC2Q, EdgeType.C2Q);
        LoadFile(pathC2A, EdgeType.C2A);

        Console.WriteLine($"Total loaded edges: {edges.Count}");
        Console.WriteLine($"Total unique nodes: {nextId}");

        if (edges.Count == 0)
        {
            Console.WriteLine("No edges loaded.");
            return;
        }

        // Sorting: Sort all edges chronologically
        edges.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
        Console.WriteLine("Edges sorted chronologically.");

        // 2. Defining Time Frames (Windowing)
        // Granularity: 1 month
        // We will group edges by (Year, Month)
        
        var batches = new SortedDictionary<DateTime, List<TemporalEdge>>();
        
        foreach (var edge in edges)
        {
            DateTime date = UnixTimeStampToDateTime(edge.Timestamp);
            // Key is the first day of the month
            DateTime monthKey = new DateTime(date.Year, date.Month, 1);

            if (!batches.ContainsKey(monthKey))
            {
                batches[monthKey] = new List<TemporalEdge>();
            }
            batches[monthKey].Add(edge);
        }

        Console.WriteLine($"Split data into {batches.Count} monthly batches.");

        // --- Cumulative Graph Structures ---
        // We use the global node count 'nextId' for the cumulative matrix dimensions
        var cumulativeMatrix = new DokSparseMatrix<int>(nextId, nextId);
        // Separate matrix for A2Q layer (Directed) for static analysis
        var a2qMatrix = new DokSparseMatrix<int>(nextId, nextId);
        
        var cumulativeNodeSet = new HashSet<int>();
        // We track edges manually to avoid recounting non-zeros every time, 
        // though GetDegrees() effectively counts them too.
        // Since it's undirected, we store 1 for (u,v) and (v,u). 
        // M is the number of unique edges.
        int cumulativeEdgeCount = 0;

        // Analysis per batch
        Console.WriteLine("\n--- Monthly Network Analysis ---");
        Console.WriteLine("Format: Month | Type | N | M | AvgDeg | AvgCC | GiantComp% | EdgeTypes(A2Q/C2Q/C2A)");
        
        DateTime startMonth = batches.Keys.First();

        // Lists for Densification Analysis
        var densificationXs = new List<double>();
        var densificationYs = new List<double>();
        int batchIndex = 0;

        foreach (var kvp in batches)
        {
            DateTime month = kvp.Key;
            var batchEdges = kvp.Value;
            double daysSinceStart = (month - startMonth).TotalDays;
            
            // =================================================================================
            // 1. SNAPSHOT GRAPH ANALYSIS
            // =================================================================================
            
            // Identify active nodes in this batch to create a compact matrix
            var activeNodes = new HashSet<int>();
            int countA2Q = 0, countC2Q = 0, countC2A = 0;

            foreach (var edge in batchEdges)
            {
                activeNodes.Add(edge.Source);
                activeNodes.Add(edge.Target);
                if (edge.Type == EdgeType.A2Q) countA2Q++;
                else if (edge.Type == EdgeType.C2Q) countC2Q++;
                else if (edge.Type == EdgeType.C2A) countC2A++;
            }

            // Map global IDs to local batch IDs (0 to N-1)
            var localMap = new Dictionary<int, int>();
            int localIndex = 0;
            foreach (var nodeId in activeNodes)
            {
                localMap[nodeId] = localIndex++;
            }

            int batchNodeCount = activeNodes.Count;
            var snapshotMatrix = new DokSparseMatrix<int>(batchNodeCount, batchNodeCount);
            
            // Fill snapshot matrix
            foreach (var edge in batchEdges)
            {
                int u = localMap[edge.Source];
                int v = localMap[edge.Target];

                // Multiplex: OR the edge types
                int currentVal = snapshotMatrix[u, v];
                int newVal = currentVal | (int)edge.Type;
                
                snapshotMatrix[u, v] = newVal;
                if (u != v) snapshotMatrix[v, u] = newVal; // Undirected
            }

            var snapAnalyzer = new Analyzer<int>(snapshotMatrix);
            var (snapDegrees, _) = snapAnalyzer.GetDegrees();
            var (snapAvgDegree, _) = snapAnalyzer.GetAverageAndMaximumDegree(snapDegrees);
            
            var (snapCC, _) = snapAnalyzer.GetClusteringCoefficients(snapDegrees);
            double snapAvgCC = snapCC.Length > 0 ? snapCC.Average() : 0.0;

            // Giant Component for Snapshot
            int snapMaxComp = snapAnalyzer.GetLargestConnectedComponentSize();
            double snapGiantPct = batchNodeCount > 0 ? (double)snapMaxComp / batchNodeCount * 100.0 : 0;

            Console.WriteLine($"{month:yyyy-MM} | SNAP | {batchNodeCount,5} | {batchEdges.Count,6} | {snapAvgDegree,6:F2} | {snapAvgCC,6:F4} | {snapGiantPct,6:F1}% | {countA2Q}/{countC2Q}/{countC2A}");

            // =================================================================================
            // 2. CUMULATIVE GRAPH ANALYSIS
            // =================================================================================

            // Update cumulative graph
            foreach (var edge in batchEdges)
            {
                int u = edge.Source;
                int v = edge.Target;
                int typeVal = (int)edge.Type;

                // Update A2Q Matrix (Directed: u -> v)
                if (edge.Type == EdgeType.A2Q)
                {
                    // Just mark existence, or count? Usually existence for structural analysis.
                    // But multiple answers might imply stronger link. Let's stick to existence (1).
                    a2qMatrix[u, v] = 1;
                }

                // Check if edge exists (undirected, so check one direction)
                int currentVal = cumulativeMatrix[u, v];
                
                // If this specific edge type didn't exist before, we might consider it a "new edge" in a multiplex sense,
                // but for M_cum (total unique edges regardless of type), we only count if currentVal was 0.
                // However, if we want M to represent "multiplex edges", it's complicated.
                // Let's stick to "number of pairs connected by at least one layer".
                if (currentVal == 0)
                {
                    cumulativeEdgeCount++;
                }

                int newVal = currentVal | typeVal;
                if (newVal != currentVal)
                {
                    cumulativeMatrix[u, v] = newVal;
                    if (u != v)
                    {
                        cumulativeMatrix[v, u] = newVal;
                    }
                }
                
                cumulativeNodeSet.Add(u);
                cumulativeNodeSet.Add(v);
            }

            int N_cum = cumulativeNodeSet.Count;
            int M_cum = cumulativeEdgeCount;

            // Collect data for Densification Analysis
            if (N_cum > 0)
            {
                densificationXs.Add(batchIndex);
                densificationYs.Add((double)M_cum / N_cum);
            }
            batchIndex++;

            // Analyze Cumulative
            var cumAnalyzer = new Analyzer<int>(cumulativeMatrix);
            
            // Degree
            var (cumDegrees, _) = cumAnalyzer.GetDegrees();
            // Avg Degree = 2 * M / N_cum
            double cumAvgDegree = N_cum > 0 ? (2.0 * M_cum) / N_cum : 0.0;

            // Clustering
            var (cumCC, _) = cumAnalyzer.GetClusteringCoefficients(cumDegrees);
            double cumSumCC = 0;
            foreach(int id in cumulativeNodeSet)
            {
                cumSumCC += cumCC[id];
            }
            double cumAvgCC = N_cum > 0 ? cumSumCC / N_cum : 0.0;

            // Giant Component
            int cumMaxComp = cumAnalyzer.GetLargestConnectedComponentSize();
            double cumGiantPct = N_cum > 0 ? (double)cumMaxComp / N_cum * 100.0 : 0;

            Console.WriteLine($"{month:yyyy-MM} | CUMUL| {N_cum,5} | {M_cum,6} | {cumAvgDegree,6:F2} | {cumAvgCC,6:F4} | {cumGiantPct,6:F1}%");

            // Save Degree Distribution for Cumulative Graph
            var cumDegDist = cumAnalyzer.GetDegreeDistribution(cumDegrees);
            //SaveDegreeDistribution(cumDegDist, $"dist_cum_{month:yyyy-MM}.csv");
        }

        // =================================================================================
        // FINAL STATISTICS TABLE
        // =================================================================================
        Console.WriteLine("\n--- Final Network Statistics ---");
        var statsAnalyzer = new Analyzer<int>(cumulativeMatrix);
        int finalNodeCount = nextId; 
        int finalEdgeCount = cumulativeEdgeCount;
        
        // Time span
        var firstDate = batches.Keys.First();
        var lastDate = batches.Keys.Last();
        string timeSpan = $"{firstDate:yyyy/MM} - {lastDate:yyyy/MM}";

        // Avg Degree
        var (finalStatsDegrees, _) = statsAnalyzer.GetDegrees();
        var (finalAvgDeg, _) = statsAnalyzer.GetAverageAndMaximumDegree(finalStatsDegrees);

        // Components
        int componentCount = statsAnalyzer.GetConnectedComponentsCount();

        // Density
        // Density = 2*E / (N*(N-1))
        double density = finalNodeCount > 1 ? (2.0 * finalEdgeCount) / ((double)finalNodeCount * (finalNodeCount - 1)) : 0;

        Console.WriteLine("Tabulka 1: Přehled základních metrik sítě");
        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"{"Metrika",-40} {"Hodnota",15}");
        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"{"Celkový počet uzlů (|V|)",-40} {finalNodeCount,15}");
        Console.WriteLine($"{"Celkový počet hran (|E|)",-40} {finalEdgeCount,15}");
        Console.WriteLine($"{"Časové rozpětí dat",-40} {timeSpan,15}");
        Console.WriteLine($"{"Průměrný stupeň uzlu (Average Degree)",-40} {finalAvgDeg,15:F4}");
        Console.WriteLine($"{"Počet propojených komponent",-40} {componentCount,15}");
        Console.WriteLine($"{"Hustota sítě (Density)",-40} {density,15:E4}");
        Console.WriteLine("--------------------------------------------------");

        // =================================================================================
        // 3. STATIC ANALYSIS (Expert Identification, Power Law, Reciprocity)
        // =================================================================================
        Console.WriteLine("\n--- Static Analysis on Final A2Q Network ---");
        
        var a2qAnalyzer = new Analyzer<int>(a2qMatrix);
        
        // 1. Reciprocity
        double reciprocity = a2qAnalyzer.CalculateReciprocity();
        Console.WriteLine($"Reciprocity (A2Q): {reciprocity:P2}");
        Console.WriteLine("  (Low reciprocity is expected in Q&A: Experts help newbies who don't help back)");

        // 2. Expert Identification (HITS & PageRank)
        Console.WriteLine("\nCalculating HITS and PageRank...");
        var (hubs, authorities) = a2qAnalyzer.CalculateHITS();
        var pageRank = a2qAnalyzer.CalculatePageRank();

        // Top 10 Hubs (Helpers - High Out-Degree / Hub Score)
        // Note: In our A2Q graph, u -> v means u answered v.
        // High Out-Degree = Answered many questions = Helper.
        // HITS Hubs = Point to good Authorities. If Authorities are Seekers, Hubs are Helpers.
        var topHubs = hubs.OrderByDescending(kvp => kvp.Value).Take(10).ToList();
        Console.WriteLine("\nTop 10 Hubs (Helpers/Experts by HITS):");
        foreach (var h in topHubs) Console.WriteLine($"  Node {h.Key}: Score {h.Value:F6}");

        // Top 10 Authorities (Seekers - High In-Degree / Auth Score)
        var topAuths = authorities.OrderByDescending(kvp => kvp.Value).Take(10).ToList();
        Console.WriteLine("\nTop 10 Authorities (Seekers/Askers by HITS):");
        foreach (var a in topAuths) Console.WriteLine($"  Node {a.Key}: Score {a.Value:F6}");

        // Top 10 PageRank
        var topPR = pageRank.OrderByDescending(kvp => kvp.Value).Take(10).ToList();
        Console.WriteLine("\nTop 10 PageRank (Important Users):");
        foreach (var pr in topPR) Console.WriteLine($"  Node {pr.Key}: Score {pr.Value:F6}");

        // Save Top 10 lists to CSV
        try
        {
            string topNodesPath = Path.Combine(outputDir, "top_nodes_analysis.csv");
            using (var writer = new StreamWriter(topNodesPath))
            {
                writer.WriteLine("Rank,Hub_Node,Hub_Score,Auth_Node,Auth_Score,PR_Node,PR_Score");
                for (int i = 0; i < 10; i++)
                {
                    var h = i < topHubs.Count ? topHubs[i] : new KeyValuePair<int, double>(-1, 0);
                    var a = i < topAuths.Count ? topAuths[i] : new KeyValuePair<int, double>(-1, 0);
                    var p = i < topPR.Count ? topPR[i] : new KeyValuePair<int, double>(-1, 0);
                    
                    writer.WriteLine($"{i+1},{h.Key},{h.Value:F6},{a.Key},{a.Value:F6},{p.Key},{p.Value:F6}");
                }
            }
            Console.WriteLine($"Saved top nodes analysis to {topNodesPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving top nodes CSV: {ex.Message}");
        }

        // 3. Degree Distribution (Power Law) for A2Q Out-Degree (Answers given)
        var (outDegrees, _) = a2qAnalyzer.GetDegrees(); // GetDegrees counts rows (out-degree)
        var outDegDist = a2qAnalyzer.GetDegreeDistribution(outDegrees);
        
        // Save and Plot
        var outDegXs = outDegDist.Keys.Where(k => k > 0).OrderBy(k => k).Select(k => (double)k).ToArray();
        var outDegYs = outDegXs.Select(k => (double)outDegDist[(int)k]).ToArray();
        
        try 
        {
            ChartGenerator.SaveDistributionPng(Path.Combine(outputDir, "a2q_out_degree_distribution.png"), outDegXs, outDegYs, "A2Q Out-Degree Distribution (Answers Given)", logX: true, logY: true);
            Console.WriteLine("\nSaved a2q_out_degree_distribution.png (Check for Power Law)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving A2Q degree chart: {ex.Message}");
        }

        // --- Final Chart Generation ---
        Console.WriteLine("\nGenerating final charts...");
        var finalAnalyzer = new Analyzer<int>(cumulativeMatrix);
        var (finalDegrees, _) = finalAnalyzer.GetDegrees();
        var finalDegDist = finalAnalyzer.GetDegreeDistribution(finalDegrees);
        
        // Prepare data for Degree Distribution Chart
        // Filter out degree 0
        var degXs = finalDegDist.Keys.Where(k => k > 0).OrderBy(k => k).Select(k => (double)k).ToArray();
        var degYs = degXs.Select(k => (double)finalDegDist[(int)k]).ToArray();
        
        try 
        {
            ChartGenerator.SaveDistributionPng(Path.Combine(outputDir, "final_degree_distribution.png"), degXs, degYs, "Degree Distribution (Log-Log)", logX: true, logY: true);
            Console.WriteLine("Saved final_degree_distribution.png");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving degree chart: {ex.Message}");
        }

        // Clustering Coefficient Distribution
        var (finalCC, _) = finalAnalyzer.GetClusteringCoefficients(finalDegrees);
        var finalClustDist = finalAnalyzer.GetClusteringDistribution(finalDegrees, finalCC);
        
        // Prepare data for Clustering Chart
        var clustXs = finalClustDist.Keys.Where(k => k > 0).OrderBy(k => k).Select(k => (double)k).ToArray();
        var clustYs = clustXs.Select(k => finalClustDist[(int)k]).ToArray();

        try
        {
            ChartGenerator.SaveDistributionPng(Path.Combine(outputDir, "final_clustering_distribution.png"), clustXs, clustYs, "Clustering Coefficient vs Degree (Log-Log)", logX: true, logY: true);
            Console.WriteLine("Saved final_clustering_distribution.png");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving clustering chart: {ex.Message}");
        }

        // =================================================================================
        // 4. CASCADE MODEL SIMULATION (Independent Cascade Model)
        // =================================================================================
        Console.WriteLine("\n--- Independent Cascade Model Simulation ---");
        
        // Parameters
        int seedCount = 50;
        double probability = 0.05; // P = 0.05
        
        // Prepare graph structure for simulation (Adjacency List for faster traversal)
        // We only care about nodes that actually exist in the cumulative graph
        var adjList = new Dictionary<int, List<int>>();
        var elements = cumulativeMatrix.GetType().GetField("_elements", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(cumulativeMatrix) as Dictionary<MatrixKey, int>;
        
        foreach (var kvp in elements)
        {
            int u = kvp.Key.Row;
            int v = kvp.Key.Column;
            if (!adjList.ContainsKey(u)) adjList[u] = new List<int>();
            adjList[u].Add(v);
        }
        
        var allNodes = cumulativeNodeSet.ToList();
        
        // --- Variant 1: Random Seeds ---
        var random = new Random();
        var randomSeeds = allNodes.OrderBy(x => random.Next()).Take(seedCount).ToList();
        Console.WriteLine($"Running simulation with {seedCount} RANDOM seeds...");
        var randomCurve = RunIndependentCascade(adjList, randomSeeds, probability);
        
        // --- Variant 2: Hub Seeds (Highest Degree) ---
        // We already have finalDegrees calculated
        var hubSeeds = allNodes.OrderByDescending(id => finalDegrees[id]).Take(seedCount).ToList();
        Console.WriteLine($"Running simulation with {seedCount} HUB seeds...");
        var hubCurve = RunIndependentCascade(adjList, hubSeeds, probability);

        // --- Plotting Results ---
        try 
        {
            // Prepare data for plotting
            // X axis: Time steps (0, 1, 2...)
            // Y axis: Total Infected Count
            
            // Determine max steps to align arrays if needed, or just plot separate series
            double[] xRandom = Enumerable.Range(0, randomCurve.Count).Select(i => (double)i).ToArray();
            double[] yRandom = randomCurve.Select(i => (double)i).ToArray();
            
            double[] xHub = Enumerable.Range(0, hubCurve.Count).Select(i => (double)i).ToArray();
            double[] yHub = hubCurve.Select(i => (double)i).ToArray();

            ChartGenerator.SaveLineChart(
                Path.Combine(outputDir, "cascade_simulation.png"), 
                new[] { "Random Seeds", "Hub Seeds" }, 
                new[] { xRandom, xHub }, 
                new[] { yRandom, yHub }, 
                $"Independent Cascade Model (P={probability})"
            );
            Console.WriteLine("Saved cascade_simulation.png");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving cascade chart: {ex.Message}");
        }

        // =================================================================================
        // 5. DYNAMIC ANALYSIS (User Lifecycle)
        // =================================================================================
        Console.WriteLine("\n--- Dynamic Analysis: User Lifecycle ---");

        // 1. Identify Top 100 Active Users (by Total Degree)
        // finalDegrees index corresponds to Internal Node ID
        var top100Users = finalDegrees.Select((deg, index) => new { Id = index, Degree = deg })
                                      .OrderByDescending(x => x.Degree)
                                      .Take(100)
                                      .Select(x => x.Id)
                                      .ToList();

        // Map UserID -> Rank (0 to 99) for plotting
        // Rank 0 = Most Active
        var userRankMap = top100Users.Select((id, index) => new { id, index })
                                     .ToDictionary(x => x.id, x => x.index);

        // 2. Collect Monthly Activity
        // We want to plot: X = Month Index, Y = User Rank
        var lifeCycleXs = new List<double>();
        var lifeCycleYs = new List<double>();
        
        // Also prepare CSV data
        var csvLines = new List<string>();
        csvLines.Add("UserRank,UserID,MonthIndex,MonthDate,ActivityCount");

        int monthIndex = 0;
        foreach (var kvp in batches)
        {
            DateTime month = kvp.Key;
            var batchEdges = kvp.Value;
            
            // Count activity for top 100 users in this batch
            var batchActivity = new Dictionary<int, int>();
            foreach (var u in top100Users) batchActivity[u] = 0;

            foreach (var edge in batchEdges)
            {
                if (batchActivity.ContainsKey(edge.Source)) batchActivity[edge.Source]++;
                if (batchActivity.ContainsKey(edge.Target)) batchActivity[edge.Target]++;
            }

            foreach (var u in top100Users)
            {
                int count = batchActivity[u];
                if (count > 0)
                {
                    // Add point for plot
                    lifeCycleXs.Add(monthIndex);
                    lifeCycleYs.Add(userRankMap[u]);
                    
                    // Add line for CSV
                    csvLines.Add($"{userRankMap[u]},{u},{monthIndex},{month:yyyy-MM},{count}");
                }
            }
            monthIndex++;
        }

        // 3. Save CSV
        try
        {
            string lifecycleCsvPath = Path.Combine(outputDir, "user_lifecycle.csv");
            File.WriteAllLines(lifecycleCsvPath, csvLines);
            Console.WriteLine($"Saved user lifecycle data to {lifecycleCsvPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving lifecycle CSV: {ex.Message}");
        }

        // 4. Plot Lifecycle (Dot Plot)
        try
        {
            // We use the Scatter plot. X = Month, Y = User Rank.
            // Users are sorted by activity (Rank 0 = Most Active).
            // Rank 0 will be at the bottom of the Y-axis.
            
            ChartGenerator.SaveDistributionPng(
                Path.Combine(outputDir, "user_lifecycle.png"), 
                lifeCycleXs.ToArray(), 
                lifeCycleYs.ToArray(), 
                "User Lifecycle (Top 100 Active Users)", 
                logX: false, 
                logY: false
            );
            Console.WriteLine("Saved user_lifecycle.png (X: Month Index, Y: User Rank 0-99)");
            Console.WriteLine("  Interpretation: Continuous lines = Evergreens; Short segments = Burnout/Transient.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving lifecycle chart: {ex.Message}");
        }

        // 5. Plot Lifecycle (Heatmap)
        try
        {
            // Prepare 2D array for heatmap: [UserRank, MonthIndex]
            // Rows = Users (0 to 99), Cols = Months (0 to batches.Count - 1)
            int numUsers = 100;
            int numMonths = batches.Count;
            double[,] heatmapData = new double[numUsers, numMonths];

            // Fill data
            // We need to re-iterate or use the CSV data we just collected.
            // Let's re-use the logic since we didn't store the raw counts in a structured way for heatmap.
            
            int mIndex = 0;
            foreach (var kvp in batches)
            {
                var batchEdges = kvp.Value;
                var batchActivity = new Dictionary<int, int>();
                foreach (var u in top100Users) batchActivity[u] = 0;

                foreach (var edge in batchEdges)
                {
                    if (batchActivity.ContainsKey(edge.Source)) batchActivity[edge.Source]++;
                    if (batchActivity.ContainsKey(edge.Target)) batchActivity[edge.Target]++;
                }

                foreach (var u in top100Users)
                {
                    int rank = userRankMap[u];
                    // Log scale for better visibility of differences? Or raw?
                    // Raw counts can be very skewed. Log(count + 1) is often better for heatmaps.
                    int count = batchActivity[u];
                    heatmapData[rank, mIndex] = count > 0 ? Math.Log10(count + 1) : 0;
                }
                mIndex++;
            }

            ChartGenerator.SaveHeatmap(
                Path.Combine(outputDir, "user_lifecycle_heatmap.png"),
                heatmapData,
                "User Lifecycle Heatmap (Log10 Activity)",
                "Time (Months)",
                "User Rank (0=Top)"
            );
            Console.WriteLine("Saved user_lifecycle_heatmap.png");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving lifecycle heatmap: {ex.Message}");
        }

        // =================================================================================
        // 6. DYNAMIC ANALYSIS (Network Densification)
        // =================================================================================
        Console.WriteLine("\n--- Dynamic Analysis: Network Densification ---");
        try
        {
            ChartGenerator.SaveSimpleLineChart(
                Path.Combine(outputDir, "network_densification.png"),
                densificationXs.ToArray(),
                densificationYs.ToArray(),
                "Network Densification (Edges/Nodes Ratio)",
                "Time (Months)",
                "E/N Ratio"
            );
            Console.WriteLine("Saved network_densification.png");
            Console.WriteLine("  Hypothesis: Ratio increases over time (Densification Power Law).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving densification chart: {ex.Message}");
        }
    }

    private static void SaveDegreeDistribution(Dictionary<int, int> distribution, string filename)
    {
        try 
        {
            // Filter out degree 0 (nodes not yet in graph)
            var lines = distribution.Where(k => k.Key > 0)
                                    .OrderBy(k => k.Key)
                                    .Select(kvp => $"{kvp.Key},{kvp.Value}");
            File.WriteAllLines(filename, new[] { "Degree,Count" }.Concat(lines));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save distribution to {filename}: {ex.Message}");
        }
    }

    private static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
    {
        // Unix timestamp is seconds past epoch
        System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
        dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
        return dtDateTime;
    }

    /// <summary>
    /// Runs the Independent Cascade Model simulation.
    /// </summary>
    /// <param name="adjList">Adjacency list of the graph.</param>
    /// <param name="seeds">Initial set of active nodes.</param>
    /// <param name="probability">Probability of infection.</param>
    /// <returns>A list of total infected counts at each time step.</returns>
    private static List<int> RunIndependentCascade(Dictionary<int, List<int>> adjList, List<int> seeds, double probability)
    {
        var activeSet = new HashSet<int>(seeds);
        var newlyActivated = new List<int>(seeds);
        var history = new List<int> { activeSet.Count };
        var random = new Random();

        // To ensure a node attempts to infect a neighbor only once, 
        // we iterate only over the *newly* activated nodes from the previous step.
        // Once a node has attempted to infect its neighbors, it stays active but doesn't try again.
        
        while (newlyActivated.Count > 0)
        {
            var nextStepActivated = new List<int>();

            foreach (var u in newlyActivated)
            {
                if (adjList.TryGetValue(u, out var neighbors))
                {
                    foreach (var v in neighbors)
                    {
                        if (!activeSet.Contains(v))
                        {
                            // Attempt to infect
                            if (random.NextDouble() < probability)
                            {
                                activeSet.Add(v);
                                nextStepActivated.Add(v);
                            }
                        }
                    }
                }
            }

            newlyActivated = nextStepActivated;
            history.Add(activeSet.Count);
        }

        return history;
    }
}*/