using MAS2;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace MAS2
{
    public class Analyzer<T> where T : struct, IEquatable<T>
    {
        private readonly DokSparseMatrix<T> _matrix;
        private readonly int _nodeCount;

        public Analyzer(DokSparseMatrix<T> matrix)
        {
            if (matrix.Rows != matrix.Columns)
            {
                throw new ArgumentException("The adjacency matrix must be square to represent a graph.");
            }
            _matrix = matrix;
            _nodeCount = matrix.Rows;
        }

        /// <summary>
        /// Computes the degree of each node in parallel.
        /// Complexity: O(N^2) where N is the number of nodes, as it checks every potential edge.
        /// </summary>
        /// <returns>A tuple containing an array with the degree of each node and the computation time.</returns>
        public (int[] degrees, TimeSpan duration) GetDegrees()
        {
            var stopwatch = Stopwatch.StartNew();
            var degrees = new int[_nodeCount];
            Parallel.For(0, _nodeCount, i =>
            {
                int degree = 0;
                for (int j = 0; j < _nodeCount; j++)
                {
                    if (!_matrix[i, j].Equals(default(T)))
                    {
                        degree++;
                    }
                }
                degrees[i] = degree;
            });
            stopwatch.Stop();
            return (degrees, stopwatch.Elapsed);
        }

        // --- Other analysis methods remain unchanged ---

        public (double average, int max) GetAverageAndMaximumDegree(int[] degrees)
        {
            if (degrees == null || degrees.Length == 0) return (0, 0);
            double sum = 0;
            int max = 0;
            for (int i = 0; i < degrees.Length; i++)
            {
                sum += degrees[i];
                if (degrees[i] > max) max = degrees[i];
            }
            return (sum / degrees.Length, max);
        }

        public Dictionary<int, int> GetDegreeDistribution(int[] degrees)
        {
            var distribution = new ConcurrentDictionary<int, int>();
            Parallel.ForEach(degrees, degree =>
            {
                distribution.AddOrUpdate(degree, 1, (key, count) => count + 1);
            });
            return new Dictionary<int, int>(distribution);
        }

        /// <summary>
        /// Computes the clustering coefficient for each node in parallel.
        /// Complexity: O(N * k_max^2) where k_max is the maximum degree. In the worst-case (a dense graph), this approaches O(N^3).
        /// </summary>
        /// <param name="degrees">An array of node degrees.</param>
        /// <returns>A tuple containing an array of clustering coefficients and the computation time.</returns>
        public (double[] coefficients, TimeSpan duration) GetClusteringCoefficients(int[] degrees)
        {
            var stopwatch = Stopwatch.StartNew();
            var coefficients = new double[_nodeCount];
            Parallel.For(0, _nodeCount, i =>
            {
                if (degrees[i] < 2)
                {
                    coefficients[i] = 0.0;
                    return;
                }

                var neighbors = new List<int>();
                for (int j = 0; j < _nodeCount; j++)
                {
                    if (!_matrix[i, j].Equals(default(T)))
                    {
                        neighbors.Add(j);
                    }
                }

                int connectedNeighbors = 0;
                for (int j = 0; j < neighbors.Count; j++)
                {
                    for (int k = j + 1; k < neighbors.Count; k++)
                    {
                        if (!_matrix[neighbors[j], neighbors[k]].Equals(default(T)))
                        {
                            connectedNeighbors++;
                        }
                    }
                }
                coefficients[i] = (2.0 * connectedNeighbors) / (degrees[i] * (degrees[i] - 1));
            });
            stopwatch.Stop();
            return (coefficients, stopwatch.Elapsed);
        }

        public Dictionary<int, double> GetClusteringDistribution(int[] degrees, double[] coefficients)
        {
            var degreeGroups = new ConcurrentDictionary<int, (double sum, int count)>();
            Parallel.For(0, _nodeCount, i =>
            {
                degreeGroups.AddOrUpdate(degrees[i],
                                        (coefficients[i], 1),
                                        (key, existing) => (existing.sum + coefficients[i], existing.count + 1));
            });
            return degreeGroups.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.sum / kvp.Value.count);
        }

        /// <summary>
        /// Computes the number of common neighbors for each pair of nodes in parallel.
        /// Complexity: O(N^3) because it iterates through all pairs of nodes (i, j) and then checks every other node k.
        /// </summary>
        /// <returns>A tuple containing a 2D array of common neighbors and the computation time.</returns>
        public (int[,] commonNeighbors, TimeSpan duration) GetCommonNeighbors()
        {
            var stopwatch = Stopwatch.StartNew();
            var commonNeighbors = new int[_nodeCount, _nodeCount];
            Parallel.For(0, _nodeCount, i =>
            {
                for (int j = i + 1; j < _nodeCount; j++)
                {
                    int count = 0;
                    for (int k = 0; k < _nodeCount; k++)
                    {
                        if (!_matrix[i, k].Equals(default(T)) && !_matrix[j, k].Equals(default(T)))
                        {
                            count++;
                        }
                    }
                    commonNeighbors[i, j] = count;
                    commonNeighbors[j, i] = count;
                }
            });
            stopwatch.Stop();
            return (commonNeighbors, stopwatch.Elapsed);
        }

        public (double average, int max) GetAverageAndMaximumCommonNeighbors(int[,] commonNeighbors)
        {
            long sum = 0;
            int max = 0;
            int pairCount = 0;
            for (int i = 0; i < _nodeCount; i++)
            {
                for (int j = i + 1; j < _nodeCount; j++)
                {
                    sum += commonNeighbors[i, j];
                    if (commonNeighbors[i, j] > max) max = commonNeighbors[i, j];
                    pairCount++;
                }
            }
            return pairCount == 0 ? (0, 0) : ((double)sum / pairCount, max);
        }

        /// <summary>
        /// Finds the clique (simplex) with the highest average edge weight.
        /// Each clique is represented as a list of node indices (matrix indices).
        /// Returns the clique and its average edge weight.
        /// </summary>
        public (List<int> clique, double averageWeight) FindCliqueWithHighestAverageEdgeWeight(List<List<int>> cliques)
        {
            List<int> bestClique = null;
            double bestAverage = double.MinValue;
            foreach (var clique in cliques)
            {
                int pairCount = 0;
                double sumWeight = 0.0;
                for (int i = 0; i < clique.Count; i++)
                {
                    for (int j = i + 1; j < clique.Count; j++)
                    {
                        // Only consider pairs (i, j) where i < j
                        var weightObj = _matrix[clique[i], clique[j]];
                        double weight = Convert.ToDouble(weightObj);
                        sumWeight += weight;
                        pairCount++;
                    }
                }
                double avg = pairCount > 0 ? sumWeight / pairCount : 0.0;
                if (avg > bestAverage)
                {
                    bestAverage = avg;
                    bestClique = clique;
                }
            }
            return (bestClique, bestAverage);
        }
    }
}