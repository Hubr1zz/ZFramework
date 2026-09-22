using System.Collections.Generic;
using GameplayBase;
using GameplayBase.Board;
using UnityEngine;

namespace GameplayBase.CombatSystem
{
    /// <summary>
    /// 目标/范围规则：描述一个效果相对攻击者位置与朝向的合法目标格。
    /// 「范围属于目标选择的一部分」——挂在行动卡效果数据上，可序列化、多态。
    /// 用于：① 攻击前的范围门控；② 鼠标悬浮时的范围预览高亮。
    /// </summary>
    [System.Serializable]
    public abstract class TargetingRuleData
    {
        /// <summary>人类可读的范围描述（UI 预览用）。</summary>
        public abstract string Describe();

        /// <summary>返回相对攻击者的合法目标格（已过滤越界）。</summary>
        public abstract List<Vector2Int> GetValidTiles(IBoardQuery board, int attackerId);
    }

    /// <summary>正前方 N 格（沿攻击者当前朝向）。默认仅正前方 1 格。</summary>
    [System.Serializable]
    public class FrontTileTargetingData : TargetingRuleData
    {
        [Min(1)] public int depth = 1;

        public override string Describe() =>
            depth == 1 ? "正前方 1 格" : $"正前方 {depth} 格";

        public override List<Vector2Int> GetValidTiles(IBoardQuery board, int attackerId)
        {
            var result = new List<Vector2Int>();
            if (board == null) return result;

            var origin = board.GetEntityPosition(attackerId);
            var facing = board.GetEntityFacing(attackerId);
            var offset = HexDirections.Offset(facing);

            for (int d = 1; d <= depth; d++)
            {
                var tile = origin + offset * d;
                if (board.IsValidTile(tile)) result.Add(tile);
            }
            return result;
        }
    }

    /// <summary>以攻击者为中心、半径 N 的范围（含全向）。</summary>
    [System.Serializable]
    public class RangeTargetingData : TargetingRuleData
    {
        [Min(1)] public int range = 1;

        public override string Describe() => $"半径 {range} 范围";

        public override List<Vector2Int> GetValidTiles(IBoardQuery board, int attackerId)
        {
            if (board == null) return new List<Vector2Int>();
            var origin = board.GetEntityPosition(attackerId);
            var tiles = board.GetTilesInRange(origin, range);
            tiles.RemoveAll(t => t == origin);
            return tiles;
        }
    }

    /// <summary>仅自身所在格。</summary>
    [System.Serializable]
    public class SelfTargetingData : TargetingRuleData
    {
        public override string Describe() => "自身";

        public override List<Vector2Int> GetValidTiles(IBoardQuery board, int attackerId)
        {
            var result = new List<Vector2Int>();
            if (board != null) result.Add(board.GetEntityPosition(attackerId));
            return result;
        }
    }
}
