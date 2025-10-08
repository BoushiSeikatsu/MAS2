using System;
using System.Collections.Generic;
using System.IO;
using MAS2;

namespace MAS2
{
    public class NetworkGenerator
    {
        /// <summary>
        /// Generates a network using the Link Selection Model.
        /// </summary>
        /// <param name="nodeCount">Total number of nodes in the network.</param>
        /// <returns>DokSparseMatrix<int> representing the adjacency matrix.</returns>
        public static DokSparseMatrix<int> GenerateLinkSelectionModel(int nodeCount)
        {
            if (nodeCount < 2)
                throw new ArgumentException("Network must have at least 2 nodes.");

            var matrix = new DokSparseMatrix<int>(nodeCount, nodeCount);
            var links = new List<(int, int)>();

            // Start with two nodes connected
            matrix[0, 1] = 1;
            matrix[1, 0] = 1;
            links.Add((0, 1));

            var rand = new Random();

            for (int newNode = 2; newNode < nodeCount; newNode++)
            {
                // Select a link at random
                var selectedLink = links[rand.Next(links.Count)];
                // Randomly pick one endpoint
                int endpoint = rand.Next(2) == 0 ? selectedLink.Item1 : selectedLink.Item2;
                // Connect new node to the selected endpoint
                matrix[newNode, endpoint] = 1;
                matrix[endpoint, newNode] = 1;
                // Add new link to the list
                links.Add((newNode, endpoint));
            }

            return matrix;
        }

        /// <summary>
        /// Generates a network using the Copying Model.
        /// </summary>
        /// <param name="nodeCount">Total number of nodes in the network.</param>
        /// <param name="p">Probability of random connection (0 <= p <= 1).</param>
        /// <returns>DokSparseMatrix<int> representing the adjacency matrix.</returns>
        public static DokSparseMatrix<int> GenerateCopyingModel(int nodeCount, double p)
        {
            if (nodeCount < 2)
                throw new ArgumentException("Network must have at least 2 nodes.");
            if (p < 0 || p > 1)
                throw new ArgumentException("Probability p must be between 0 and 1.");

            var matrix = new DokSparseMatrix<int>(nodeCount, nodeCount);
            var rand = new Random();

            // Start with two nodes connected
            matrix[0, 1] = 1;
            matrix[1, 0] = 1;

            for (int newNode = 2; newNode < nodeCount; newNode++)
            {
                // Select a node u at random from existing nodes
                int u = rand.Next(newNode);
                if (rand.NextDouble() < p)
                {
                    // With probability p, connect to u
                    matrix[newNode, u] = 1;
                    matrix[u, newNode] = 1;
                }
                else
                {
                    // With probability 1-p, copy one of u's outgoing links
                    var outgoing = new List<int>();
                    for (int target = 0; target < newNode; target++)
                    {
                        if (matrix[u, target] != 0)
                            outgoing.Add(target);
                    }
                    if (outgoing.Count > 0)
                    {
                        int target = outgoing[rand.Next(outgoing.Count)];
                        matrix[newNode, target] = 1;
                        matrix[target, newNode] = 1;
                    }
                    else
                    {
                        // If u has no outgoing links, fallback to random connection
                        matrix[newNode, u] = 1;
                        matrix[u, newNode] = 1;
                    }
                }
            }

            return matrix;
        }

        /// <summary>
        /// Saves a DokSparseMatrix<int> as an edge list to a file.
        /// </summary>
        /// <param name="matrix">The matrix to save.</param>
        /// <param name="filePath">The file path to save to.</param>
        public static void SaveMatrixAsEdgeList(DokSparseMatrix<int> matrix, string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                for (int i = 0; i < matrix.Rows; i++)
                {
                    for (int j = i + 1; j < matrix.Columns; j++) // Only upper triangle for undirected
                    {
                        if (matrix[i, j] != 0)
                        {
                            writer.WriteLine($"{i} {j} {matrix[i, j]}");
                        }
                    }
                }
            }
        }
    }
}
