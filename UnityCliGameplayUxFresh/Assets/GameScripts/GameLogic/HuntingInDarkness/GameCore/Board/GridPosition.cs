using System;

namespace HuntingInDarkness.GameCore.Board
{
    public readonly struct GridPosition : IEquatable<GridPosition>
    {
        public static readonly GridPosition Zero = new GridPosition(0, 0);

        public int X { get; }
        public int Y { get; }

        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(GridPosition other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPosition other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public override string ToString() => $"({X}, {Y})";

        public static GridPosition operator +(GridPosition left, GridPosition right) =>
            new GridPosition(left.X + right.X, left.Y + right.Y);
        public static bool operator ==(GridPosition left, GridPosition right) => left.Equals(right);
        public static bool operator !=(GridPosition left, GridPosition right) => !left.Equals(right);
    }
}
