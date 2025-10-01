using System;
using System.Collections.Generic;
using System.Linq;
using MAS2;

namespace MAS2
{
    public static class LPAlgorithms
    {
        // Common Neighbors
        public static int CommonNeighbors(DokSparseMatrix<int> matrix, int u, int v)
        {
            int count = 0;
            for (int k = 0; k < matrix.Rows; k++)
            {
                if (matrix[u, k] != 0 && matrix[v, k] != 0)
                    count++;
            }
            return count;
        }

        // Jaccard Coefficient
        public static double JaccardCoefficient(DokSparseMatrix<int> matrix, int u, int v)
        {
            int intersection = 0, union = 0;
            for (int k = 0; k < matrix.Rows; k++)
            {
                bool uHas = matrix[u, k] != 0;
                bool vHas = matrix[v, k] != 0;
                if (uHas && vHas) intersection++;
                if (uHas || vHas) union++;
            }
            return union == 0 ? 0 : (double)intersection / union;
        }

        // Adamic-Adar Index
        public static double AdamicAdar(DokSparseMatrix<int> matrix, int u, int v)
        {
            double score = 0.0;
            for (int k = 0; k < matrix.Rows; k++)
            {
                if (matrix[u, k] != 0 && matrix[v, k] != 0)
                {
                    int degree = 0;
                    for (int j = 0; j < matrix.Rows; j++)
                        if (matrix[k, j] != 0) degree++;
                    if (degree > 1)
                        score += 1.0 / Math.Log(degree);
                }
            }
            return score;
        }

        // Preferential Attachment
        public static int PreferentialAttachment(DokSparseMatrix<int> matrix, int u, int v)
        {
            int degU = 0, degV = 0;
            for (int k = 0; k < matrix.Rows; k++)
            {
                if (matrix[u, k] != 0) degU++;
                if (matrix[v, k] != 0) degV++;
            }
            return degU * degV;
        }

        // Resource Allocation Index
        public static double ResourceAllocation(DokSparseMatrix<int> matrix, int u, int v)
        {
            double score = 0.0;
            for (int k = 0; k < matrix.Rows; k++)
            {
                if (matrix[u, k] != 0 && matrix[v, k] != 0)
                {
                    int degree = 0;
                    for (int j = 0; j < matrix.Rows; j++)
                        if (matrix[k, j] != 0) degree++;
                    if (degree > 0)
                        score += 1.0 / degree;
                }
            }
            return score;
        }

        // Cosine Similarity
        public static double CosineSimilarity(DokSparseMatrix<int> matrix, int u, int v)
        {
            int dot = 0, normU = 0, normV = 0;
            for (int k = 0; k < matrix.Rows; k++)
            {
                int a = matrix[u, k];
                int b = matrix[v, k];
                dot += a * b;
                normU += a * a;
                normV += b * b;
            }
            double denom = Math.Sqrt(normU) * Math.Sqrt(normV);
            return denom == 0 ? 0 : dot / denom;
        }

        // Sorensen Index
        public static double SorensenIndex(DokSparseMatrix<int> matrix, int u, int v)
        {
            int intersection = 0, degU = 0, degV = 0;
            for (int k = 0; k < matrix.Rows; k++)
            {
                bool uHas = matrix[u, k] != 0;
                bool vHas = matrix[v, k] != 0;
                if (uHas && vHas) intersection++;
                if (uHas) degU++;
                if (vHas) degV++;
            }
            int denom = degU + degV;
            return denom == 0 ? 0 : (2.0 * intersection) / denom;
        }

        // CAR-based Common Neighbor Index
        public static double CARIndex(DokSparseMatrix<int> matrix, int u, int v)
        {
            double score = 0.0;
            for (int k = 0; k < matrix.Rows; k++)
            {
                if (matrix[u, k] != 0 && matrix[v, k] != 0)
                {
                    int degU = 0, degV = 0;
                    for (int j = 0; j < matrix.Rows; j++)
                    {
                        if (matrix[u, j] != 0) degU++;
                        if (matrix[v, j] != 0) degV++;
                    }
                    score += 1.0 / (degU + degV - 2);
                }
            }
            return score;
        }
    }
}
