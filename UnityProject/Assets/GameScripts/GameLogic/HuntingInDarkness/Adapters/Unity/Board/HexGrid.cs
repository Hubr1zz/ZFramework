using System.Collections.Generic;
using HuntingInDarkness.GameCore.Board;
using UnityEngine;

namespace GameplayBase.Board
{
    /// <summary>Legacy Unity adapter for <see cref="HexGridMap"/>.</summary>
    public class HexGrid : IBoardGrid
    {
        private readonly HexGridMap _grid;

        private HexGrid(HexGridMap grid) => _grid = grid;

        public static HexGrid CreateRadial(int radius) =>
            new HexGrid(HexGridMap.CreateRadial(radius));

        public bool IsValidCoord(Vector2Int coord) => _grid.Contains(ToCore(coord));

        public List<Vector2Int> GetNeighbors(Vector2Int coord) =>
            ToUnity(_grid.GetNeighbors(ToCore(coord)));

        public int GetDistance(Vector2Int a, Vector2Int b) =>
            _grid.GetDistance(ToCore(a), ToCore(b));

        public List<Vector2Int> GetInRange(Vector2Int center, int range) =>
            ToUnity(_grid.GetInRange(ToCore(center), range));

        public List<Vector2Int> GetAllCoords() => ToUnity(_grid.GetAll());

        public Vector3 CoordToWorld(Vector2Int coord, float cellSize)
        {
            float x = cellSize * (Mathf.Sqrt(3f) * coord.x + Mathf.Sqrt(3f) / 2f * coord.y);
            float z = cellSize * (1.5f * coord.y);
            return new Vector3(x, 0f, z);
        }

        private static GridPosition ToCore(Vector2Int value) =>
            new GridPosition(value.x, value.y);

        private static List<Vector2Int> ToUnity(IReadOnlyList<GridPosition> values)
        {
            var result = new List<Vector2Int>(values.Count);
            foreach (GridPosition value in values)
                result.Add(new Vector2Int(value.X, value.Y));
            return result;
        }
    }
}
