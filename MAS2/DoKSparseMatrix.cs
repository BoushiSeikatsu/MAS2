using MAS2;
using System;
using System.Collections.Generic;
using System.Text;

namespace MAS2
{
    public class DokSparseMatrix<T> where T : struct, IEquatable<T>
    {
        private readonly Dictionary<MatrixKey, T> _elements;
        private readonly int _rows;
        private readonly int _columns;

        public int Rows => _rows;
        public int Columns => _columns;

        public DokSparseMatrix(int rows, int columns)
        {
            if (rows <= 0 || columns <= 0)
            {
                throw new ArgumentException("Matrix dimensions must be positive.");
            }
            _rows = rows;
            _columns = columns;
            _elements = new Dictionary<MatrixKey, T>();
        }

        /// <summary>
        /// Creates a DokSparseMatrix from a file containing graph edge data.
        /// </summary>
        /// <param name="filePath">The path to the input file.</param>
        /// <param name="valueParser">A function to parse the score string into the generic type T.</param>
        /// <param name="delimiter">The character used to separate values on a line.</param>
        /// <returns>A new DokSparseMatrix representing the graph from the file.</returns>
        public static DokSparseMatrix<T> FromFile(string filePath, Func<string, T> valueParser, char delimiter = ' ')
        {
            var nodeMap = new Dictionary<string, int>();
            var edges = new List<(string p1, string p2, string score)>();
            int nextIndex = 0;

            // First pass: Read the file to discover all unique nodes and store edge information.
            // This builds a map from string identifiers to integer indices.
            using (var reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var parts = line.Split(delimiter);
                    if (parts.Length < 2) continue; // Skip malformed lines

                    var p1 = parts[0];
                    var p2 = parts[1];
                    var score = parts.Length > 2 ? parts[2] : "1"; // Default score if missing

                    if (!nodeMap.ContainsKey(p1)) nodeMap.Add(p1, nextIndex++);
                    if (!nodeMap.ContainsKey(p2)) nodeMap.Add(p2, nextIndex++);

                    edges.Add((p1, p2, score));
                }
            }

            int nodeCount = nodeMap.Count;
            if (nodeCount == 0)
            {
                return new DokSparseMatrix<T>(1, 1); // Return an empty matrix if file is empty
            }

            var matrix = new DokSparseMatrix<T>(nodeCount, nodeCount);

            // Second pass: Use the map to populate the matrix with the stored edges.
            foreach (var edge in edges)
            {
                int rowIndex = nodeMap[edge.p1];
                int colIndex = nodeMap[edge.p2];
                T value = valueParser(edge.score);

                // Assuming an undirected graph, set the value for both (i, j) and (j, i)
                matrix[rowIndex, colIndex] = value;
                matrix[colIndex, rowIndex] = value;
            }

            Console.WriteLine($"Loaded matrix with {nodeCount} nodes from '{filePath}'.");
            return matrix;
        }


        public T this[int row, int column]
        {
            get => GetValue(row, column);
            set => SetValue(row, column, value);
        }

        public T GetValue(int row, int column)
        {
            ValidateCoordinates(row, column);
            return _elements.TryGetValue(new MatrixKey(row, column), out var value) ? value : default;
        }

        public void SetValue(int row, int column, T value)
        {
            ValidateCoordinates(row, column);
            var key = new MatrixKey(row, column);
            if (EqualityComparer<T>.Default.Equals(value, default))
            {
                _elements.Remove(key);
            }
            else
            {
                _elements[key] = value;
            }
        }

        public DokSparseMatrix<T> Add(DokSparseMatrix<T> other)
        {
            if (Rows != other.Rows || Columns != other.Columns)
            {
                throw new ArgumentException("Matrices must have the same dimensions for addition.");
            }

            var result = new DokSparseMatrix<T>(Rows, Columns);

            foreach (var (key, value) in _elements)
            {
                result.SetValue(key.Row, key.Column, (dynamic)value + other.GetValue(key.Row, key.Column));
            }

            foreach (var (key, value) in other._elements)
            {
                if (!_elements.ContainsKey(key))
                {
                    result.SetValue(key.Row, key.Column, value);
                }
            }
            return result;
        }

        public DokSparseMatrix<T> Multiply(DokSparseMatrix<T> other)
        {
            if (Columns != other.Rows)
            {
                throw new ArgumentException("The number of columns in the first matrix must equal the number of rows in the second matrix for multiplication.");
            }

            var result = new DokSparseMatrix<T>(Rows, other.Columns);

            foreach (var (thisKey, thisValue) in _elements)
            {
                for (int j = 0; j < other.Columns; j++)
                {
                    if (other.GetValue(thisKey.Column, j) is T otherValue && !EqualityComparer<T>.Default.Equals(otherValue, default))
                    {
                        result[thisKey.Row, j] = (dynamic)result[thisKey.Row, j] + (dynamic)thisValue * otherValue;
                    }
                }
            }

            return result;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Rows; i++)
            {
                for (int j = 0; j < Columns; j++)
                {
                    sb.Append(GetValue(i, j) + "\t");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private void ValidateCoordinates(int row, int column)
        {
            if (row < 0 || row >= _rows || column < 0 || column >= _columns)
            {
                throw new IndexOutOfRangeException("Matrix coordinates are out of range.");
            }
        }
    }
}