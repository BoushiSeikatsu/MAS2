using System;

namespace MAS2
{
    public struct MatrixKey : IEquatable<MatrixKey>
    {
        public int Row { get; }
        public int Column { get; }

        public MatrixKey(int row, int column)
        {
            Row = row;
            Column = column;
        }

        public bool Equals(MatrixKey other)
        {
            return Row == other.Row && Column == other.Column;
        }

        public override bool Equals(object obj)
        {
            return obj is MatrixKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Row, Column);
        }
    }
}