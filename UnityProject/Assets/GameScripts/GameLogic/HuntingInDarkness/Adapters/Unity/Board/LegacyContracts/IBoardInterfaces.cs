// ─── 棋盘查询 / 写入接口 ──────────────────────────────────────────────────────

using System.Collections.Generic;
using GameplayBase.Board;
using UnityEngine;

namespace GameplayBase
{
    /// <summary>棋盘只读查询，供效果/条件使用</summary>
    public interface IBoardQuery
    {
        bool         IsValidTile(Vector2Int tile);
        int?         GetEntityAt(Vector2Int tile);
        Vector2Int   GetEntityPosition(int entityId);
        List<Vector2Int> GetTilesInRange(Vector2Int center, int range);
        int          GetDistance(Vector2Int a, Vector2Int b);

        /// <summary>实体当前朝向（默认 <see cref="HexDirection.E"/>）。</summary>
        HexDirection GetEntityFacing(int entityId);
    }

    /// <summary>棋盘写入操作，仅 GameManager 持有</summary>
    public interface IBoardCommand
    {
        void MoveEntity(int entityId, Vector2Int target);
        void PlaceEntity(int entityId, Vector2Int tile);
        void PlaceEntity(int entityId, Vector2Int tile, HexDirection facing);
        void RemoveEntity(int entityId);
        void SetEntityFacing(int entityId, HexDirection facing);
    }
}
