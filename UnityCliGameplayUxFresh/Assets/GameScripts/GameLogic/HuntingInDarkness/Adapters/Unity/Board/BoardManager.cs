using System.Collections.Generic;
using GameplayBase.Board;
using HuntingInDarkness.GameCore.Board;
using UnityEngine;

namespace GameplayBase
{
    /// <summary>
    /// Unity coordinate adapter over the pure <see cref="BoardState"/> rule model.
    /// World-space conversion stays here; placement and distance rules live in GameCore.
    /// </summary>
    public class BoardManager : IBoardQuery, IBoardCommand
    {
        private readonly BoardState _board;
        private readonly float _cellSize;

        public BoardManager(int arenaRadius, float cellSize)
        {
            _cellSize = cellSize;
            _board = new BoardState(HexGridMap.CreateRadial(arenaRadius));
        }

        public List<Vector2Int> GetAllCoords() => ToUnity(_board.GetAllPositions());

        public Vector3 TileToWorld(Vector2Int coord)
        {
            float x = _cellSize * (Mathf.Sqrt(3f) * coord.x + Mathf.Sqrt(3f) / 2f * coord.y);
            float z = _cellSize * (1.5f * coord.y);
            return new Vector3(x, 0f, z);
        }

        public float CellSize => _cellSize;

        public bool IsValidTile(Vector2Int tile) => _board.IsValid(ToCore(tile));

        public int? GetEntityAt(Vector2Int tile) => _board.GetEntityAt(ToCore(tile));

        public Vector2Int GetEntityPosition(int entityId) =>
            ToUnity(_board.GetEntityPosition(entityId));

        public List<Vector2Int> GetTilesInRange(Vector2Int center, int range) =>
            ToUnity(_board.GetInRange(ToCore(center), range));

        public int GetDistance(Vector2Int a, Vector2Int b) =>
            _board.GetDistance(ToCore(a), ToCore(b));

        public bool HasEntity(int entityId) => _board.HasEntity(entityId);

        public HexDirection GetEntityFacing(int entityId) =>
            (HexDirection)_board.GetEntityFacing(entityId);

        public void PlaceEntity(int entityId, Vector2Int tile) =>
            _board.Place(entityId, ToCore(tile));

        public void PlaceEntity(int entityId, Vector2Int tile, HexDirection facing) =>
            _board.Place(entityId, ToCore(tile), (HexFacing)facing);

        public void SetEntityFacing(int entityId, HexDirection facing) =>
            _board.SetFacing(entityId, (HexFacing)facing);

        public void MoveEntity(int entityId, Vector2Int target) =>
            _board.Move(entityId, ToCore(target));

        public void RemoveEntity(int entityId) => _board.Remove(entityId);

        private static GridPosition ToCore(Vector2Int value) =>
            new GridPosition(value.x, value.y);

        private static Vector2Int ToUnity(GridPosition value) =>
            new Vector2Int(value.X, value.Y);

        private static List<Vector2Int> ToUnity(IReadOnlyList<GridPosition> values)
        {
            var result = new List<Vector2Int>(values.Count);
            foreach (GridPosition value in values)
                result.Add(ToUnity(value));
            return result;
        }
    }
}
