using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameplayBase.CombatSystem
{
    public enum TargetSelectionType
    {
        HitLocation,
        EnemyUnit,
        AllyUnit,
        Tile,
    }

    public struct TargetSelection
    {
        public TargetSelectionType Type;
        public int? UnitId;
        public Vector2Int? Tile;

        public static TargetSelection ForUnit(int unitId, TargetSelectionType type) =>
            new() { Type = type, UnitId = unitId };

        public static TargetSelection ForTile(Vector2Int coord) =>
            new() { Type = TargetSelectionType.Tile, Tile = coord };
    }

    /// <summary>
    /// 目标选择策略接口。
    /// 每种攻击默认持有一个实现；特殊战斗通过 ITargetSelectorOverride 覆盖。
    /// </summary>
    public interface ITargetSelector
    {
        TargetSelectionType SelectionType { get; }

        /// <summary>当前上下文下是否存在至少一个合法目标。用于战前 UI 禁用判断。</summary>
        bool HasValidTargets(AttackContext ctx);

        /// <summary>
        /// 请求玩家做出选择。
        /// 返回 null 表示取消或无合法目标，调用方应中止攻击。
        /// </summary>
        UniTask<TargetSelection?> RequestSelection(
            string prompt, AttackContext ctx, IPlayerInputProvider input, CancellationToken cancellationToken = default);
    }

    /// <summary>拦截并替换攻击目标选择方式的覆盖器接口。</summary>
    public interface ITargetSelectorOverride
    {
        /// <summary>返回 true 表示命中，out result 为替换后的选择器。</summary>
        bool TryOverride(AttackContext context, out ITargetSelector result);
    }
}
