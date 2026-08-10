using System.Collections.Generic;
using GameplayBase;
using UnityEngine;

namespace SO.Combat
{
    /// <summary>
    /// 战斗生成期上下文，供动态组件生成规则查询棋盘 / 已放置组件。
    /// 由 BattleGenerator 实现。
    /// </summary>
    public interface IBattleGenContext
    {
        IBoardQuery Board { get; }

        /// <summary>地图半径（用于『远离边界 N 格』之类规则）。</summary>
        int MapRadius { get; }

        /// <summary>某个格子当前是否被实体或组件占用。</summary>
        bool IsOccupied(Vector2Int tile);

        /// <summary>按组件 Key 返回所有已放置组件的位置（如查询所有『石头』）。</summary>
        IReadOnlyList<Vector2Int> GetPlacedComponentTiles(string componentKey);

        /// <summary>所有合法且未占用的格子。</summary>
        IReadOnlyList<Vector2Int> GetFreeTiles();
    }

    /// <summary>
    /// 动态组件生成规则数据基类。可序列化、多态（[SerializeReference]）。
    /// 子类描述『该组件能生成在哪些格子』的约束，BattleGenerator 求各规则交集后落子。
    /// </summary>
    [System.Serializable]
    public abstract class ComponentSpawnRuleData
    {
        /// <summary>
        /// 解析出满足本规则的候选格子集合。
        /// 返回 false 或空集表示本规则当前无解。
        /// </summary>
        public abstract bool TryResolveCandidates(
            IBattleGenContext ctx, out List<Vector2Int> candidates);
    }

    // ─── 占位子类（字段齐全，规则体留待实现）───────────────────────────────

    /// <summary>只能在距离地图边界至少 N 格的位置生成。</summary>
    [System.Serializable]
    public class AwayFromBoundaryRuleData : ComponentSpawnRuleData
    {
        [Min(0)] public int minDistanceFromBoundary = 3;

        public override bool TryResolveCandidates(
            IBattleGenContext ctx, out List<Vector2Int> candidates)
        {
            // TODO: 实现『远离边界 minDistanceFromBoundary 格』筛选。
            candidates = new List<Vector2Int>();
            return false;
        }
    }

    /// <summary>只能在与指定 Key 组件相邻的位置生成。</summary>
    [System.Serializable]
    public class AdjacentToComponentRuleData : ComponentSpawnRuleData
    {
        public string targetComponentKey;

        public override bool TryResolveCandidates(
            IBattleGenContext ctx, out List<Vector2Int> candidates)
        {
            // TODO: 实现『与 targetComponentKey 组件相邻』筛选。
            candidates = new List<Vector2Int>();
            return false;
        }
    }
}
