using System.Collections.Generic;
using GameplayBase.Board;
using UnityEngine;

namespace GameplayBase
{
    /// <summary>
    /// 空对象模式（Null Object）实现 IBoardQuery/IBoardCommand。
    /// 在无棋盘的测试场景或子系统单元测试中作为占位符注入，
    /// 避免空引用；正式运行时由 BoardManager 替代。
    /// </summary>
    public class BoardStub : IBoardQuery, IBoardCommand
    {
        public bool IsValidTile(Vector2Int tile) => false;
        public int? GetEntityAt(Vector2Int tile) => null;
        public Vector2Int GetEntityPosition(int entityId) => Vector2Int.zero;
        public List<Vector2Int> GetTilesInRange(Vector2Int center, int range) => new();
        public int GetDistance(Vector2Int a, Vector2Int b) => 0;
        public HexDirection GetEntityFacing(int entityId) => HexDirection.E;
        public void MoveEntity(int entityId, Vector2Int target) { }
        public void PlaceEntity(int entityId, Vector2Int tile) { }
        public void PlaceEntity(int entityId, Vector2Int tile, HexDirection facing) { }
        public void RemoveEntity(int entityId) { }
        public void SetEntityFacing(int entityId, HexDirection facing) { }
    }
}
