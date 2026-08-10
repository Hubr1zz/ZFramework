using System;
using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Board
{
    /// <summary>Pure axial-coordinate hex grid used by combat and reusable map modules.</summary>
    public sealed class HexGridMap
    {
        private static readonly GridPosition[] Directions =
        {
            new GridPosition(1, 0),
            new GridPosition(-1, 0),
            new GridPosition(0, 1),
            new GridPosition(0, -1),
            new GridPosition(1, -1),
            new GridPosition(-1, 1)
        };

        private readonly HashSet<GridPosition> _positions;

        private HexGridMap(HashSet<GridPosition> positions) => _positions = positions;

        public static HexGridMap CreateRadial(int radius)
        {
            int safeRadius = Math.Max(0, radius);
            var positions = new HashSet<GridPosition>();
            for (int q = -safeRadius; q <= safeRadius; q++)
            for (int r = -safeRadius; r <= safeRadius; r++)
            {
                if (Math.Abs(q) + Math.Abs(r) + Math.Abs(q + r) <= 2 * safeRadius)
                    positions.Add(new GridPosition(q, r));
            }
            return new HexGridMap(positions);
        }

        public bool Contains(GridPosition position) => _positions.Contains(position);

        public int GetDistance(GridPosition a, GridPosition b)
        {
            int dx = a.X - b.X;
            int dy = a.Y - b.Y;
            return (Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dx + dy)) / 2;
        }

        public List<GridPosition> GetNeighbors(GridPosition center)
        {
            var result = new List<GridPosition>(Directions.Length);
            foreach (GridPosition direction in Directions)
            {
                GridPosition neighbor = center + direction;
                if (_positions.Contains(neighbor))
                    result.Add(neighbor);
            }
            return result;
        }

        public List<GridPosition> GetInRange(GridPosition center, int range)
        {
            var result = new List<GridPosition>();
            foreach (GridPosition position in _positions)
                if (GetDistance(center, position) <= range)
                    result.Add(position);
            return result;
        }

        public List<GridPosition> GetAll() => new List<GridPosition>(_positions);
    }
}
