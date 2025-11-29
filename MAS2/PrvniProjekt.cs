using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MAS2;

//Kaskádový model, jeden vrchol může ovlivnit pouze jednou, jakmile jednou ovlivním už nikdy nemužu ovlivnit znovu
public class PrvniProjekts
{
    struct TemporalEdge
    {
        public int Source;
        public int Target;
        public long Timestamp;
    }

    public static void Main(string[] args)
    {
        // Path to the dataset
        string dataPath = @"E:\C#Projects\MAS2\MAS2\data\sx-superuser.txt";

        if (!File.Exists(dataPath))
        {
            Console.WriteLine($"Error: Data file not found at {dataPath}");
            return;
        }

        Console.WriteLine("--- Data Loading and Pre-processing ---");

        // 1. Data Loading and Parsing
        // ID Mapping: Original ID -> Internal ID (0 to N-1)
        var originalToInternal = new Dictionary<int, int>();
        var edges = new List<TemporalEdge>();
        int nextId = 0;

        // Read lines
        // Assuming format: Source Target Time
        foreach (var line in File.ReadLines(dataPath))
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

                edges.Add(new TemporalEdge { Source = u, Target = v, Timestamp = timestamp });
            }
        }

        Console.WriteLine($"Loaded {edges.Count} edges.");
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
        var cumulativeNodeSet = new HashSet<int>();
        // We track edges manually to avoid recounting non-zeros every time, 
        // though GetDegrees() effectively counts them too.
        // Since it's undirected, we store 1 for (u,v) and (v,u). 
        // M is the number of unique edges.
        int cumulativeEdgeCount = 0;

        // Analysis per batch
        Console.WriteLine("\n--- Monthly Network Analysis ---");
        Console.WriteLine("Format: Month | Type | N | M | AvgDeg | AvgCC | GiantComp%");
        
        DateTime startMonth = batches.Keys.First();

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
            foreach (var edge in batchEdges)
            {
                activeNodes.Add(edge.Source);
                activeNodes.Add(edge.Target);
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

                snapshotMatrix[u, v] = 1;
                if (u != v) snapshotMatrix[v, u] = 1;
            }

            var snapAnalyzer = new Analyzer<int>(snapshotMatrix);
            var (snapDegrees, _) = snapAnalyzer.GetDegrees();
            var (snapAvgDegree, _) = snapAnalyzer.GetAverageAndMaximumDegree(snapDegrees);
            
            var (snapCC, _) = snapAnalyzer.GetClusteringCoefficients(snapDegrees);
            double snapAvgCC = snapCC.Length > 0 ? snapCC.Average() : 0.0;

            // Giant Component for Snapshot
            int snapMaxComp = snapAnalyzer.GetLargestConnectedComponentSize();
            double snapGiantPct = batchNodeCount > 0 ? (double)snapMaxComp / batchNodeCount * 100.0 : 0;

            Console.WriteLine($"{month:yyyy-MM} | SNAP | {batchNodeCount,5} | {batchEdges.Count,6} | {snapAvgDegree,6:F2} | {snapAvgCC,6:F4} | {snapGiantPct,6:F1}%");

            // =================================================================================
            // 2. CUMULATIVE GRAPH ANALYSIS
            // =================================================================================

            // Update cumulative graph
            foreach (var edge in batchEdges)
            {
                // Check if edge exists (undirected, so check one direction)
                if (cumulativeMatrix[edge.Source, edge.Target] == 0)
                {
                    cumulativeMatrix[edge.Source, edge.Target] = 1;
                    if (edge.Source != edge.Target)
                    {
                        cumulativeMatrix[edge.Target, edge.Source] = 1;
                    }
                    cumulativeEdgeCount++;
                }
                cumulativeNodeSet.Add(edge.Source);
                cumulativeNodeSet.Add(edge.Target);
            }

            int N_cum = cumulativeNodeSet.Count;
            int M_cum = cumulativeEdgeCount;

            // Analyze Cumulative
            // Note: The matrix size is 'nextId', but many nodes might be isolated (not yet appeared).
            // However, our cumulativeNodeSet tracks nodes that have appeared.
            // The Analyzer iterates over the whole matrix dimensions for arrays like 'degrees'.
            // But GetDegrees only counts non-zero entries.
            // For averages (AvgDegree, AvgCC), we should consider only the N_cum nodes that have appeared?
            // The Analyzer methods usually divide by _nodeCount (matrix rows). 
            // If we use the huge matrix, the average will be diluted by zeros for future nodes.
            // We need to be careful. 
            // Actually, Analyzer.GetAverageAndMaximumDegree divides by degrees.Length which is _nodeCount.
            // This is incorrect for the growing graph if we initialized it with max size.
            // We should calculate averages manually using N_cum.

            var cumAnalyzer = new Analyzer<int>(cumulativeMatrix);
            
            // Degree
            var (cumDegrees, _) = cumAnalyzer.GetDegrees();
            // cumDegrees has length 'nextId'. We only care about nodes in cumulativeNodeSet?
            // Actually, nodes not in cumulativeNodeSet have degree 0.
            // Avg Degree = 2 * M / N_cum
            double cumAvgDegree = N_cum > 0 ? (2.0 * M_cum) / N_cum : 0.0;

            // Clustering
            // We only want CC for nodes that exist.
            var (cumCC, _) = cumAnalyzer.GetClusteringCoefficients(cumDegrees);
            // Filter CCs for existing nodes
            double cumSumCC = 0;
            foreach(int id in cumulativeNodeSet)
            {
                cumSumCC += cumCC[id];
            }
            double cumAvgCC = N_cum > 0 ? cumSumCC / N_cum : 0.0;

            // Giant Component
            // The method returns size in nodes.
            int cumMaxComp = cumAnalyzer.GetLargestConnectedComponentSize();
            double cumGiantPct = N_cum > 0 ? (double)cumMaxComp / N_cum * 100.0 : 0;

            Console.WriteLine($"{month:yyyy-MM} | CUMUL| {N_cum,5} | {M_cum,6} | {cumAvgDegree,6:F2} | {cumAvgCC,6:F4} | {cumGiantPct,6:F1}%");

            // Save Degree Distribution for Cumulative Graph
            // We only save it periodically or for the last one to avoid too much I/O?
            // User said "Save this data". Let's save it for every month to a file.
            var cumDegDist = cumAnalyzer.GetDegreeDistribution(cumDegrees);
            //SaveDegreeDistribution(cumDegDist, $"dist_cum_{month:yyyy-MM}.csv");
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
            ChartGenerator.SaveDistributionPng("final_degree_distribution.png", degXs, degYs, "Degree Distribution (Log-Log)", logX: true, logY: true);
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
            ChartGenerator.SaveDistributionPng("final_clustering_distribution.png", clustXs, clustYs, "Clustering Coefficient vs Degree (Log-Log)", logX: true, logY: true);
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
                "cascade_simulation.png", 
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
}