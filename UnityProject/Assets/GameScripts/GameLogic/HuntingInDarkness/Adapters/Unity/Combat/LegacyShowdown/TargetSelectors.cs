using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameplayBase.CombatSystem
{
    // ═══════════════════════════════════════════
    // 格子选择
    // ═══════════════════════════════════════════

    public class TileTargetSelector : ITargetSelector
    {
        private readonly List<Vector2Int> _validTiles;

        public TileTargetSelector(List<Vector2Int> validTiles)
        {
            _validTiles = validTiles ?? new List<Vector2Int>();
        }

        public TargetSelectionType SelectionType => TargetSelectionType.Tile;

        public bool HasValidTargets(AttackContext ctx) => _validTiles.Count > 0;

        public async UniTask<TargetSelection?> RequestSelection(
            string prompt, AttackContext ctx, IPlayerInputProvider input, CancellationToken cancellationToken = default)
        {
            var tile = await input.RequestSelectTile(prompt, _validTiles, cancellationToken);
            if (tile == null) return null;
            return TargetSelection.ForTile(tile.Value);
        }
    }

    // ═══════════════════════════════════════════
    // 敌方单位选择
    // ═══════════════════════════════════════════

    public class EnemyUnitTargetSelector : ITargetSelector
    {
        private readonly List<int> _validEnemyIds;

        public EnemyUnitTargetSelector(List<int> validEnemyIds)
        {
            _validEnemyIds = validEnemyIds ?? new List<int>();
        }

        public TargetSelectionType SelectionType => TargetSelectionType.EnemyUnit;

        public bool HasValidTargets(AttackContext ctx) => _validEnemyIds.Count > 0;

        public async UniTask<TargetSelection?> RequestSelection(
            string prompt, AttackContext ctx, IPlayerInputProvider input, CancellationToken cancellationToken = default)
        {
            if (_validEnemyIds.Count == 0) return null;
            int id = await input.RequestSelectTarget(prompt, _validEnemyIds, cancellationToken);
            return TargetSelection.ForUnit(id, TargetSelectionType.EnemyUnit);
        }
    }

    // ═══════════════════════════════════════════
    // 特殊战斗：强制所有攻击改为格子选择
    // ═══════════════════════════════════════════

    /// <summary>
    /// 将所有角色攻击的目标选择替换为格子选择。
    /// 特殊战斗开始时注册到 CombatManager，结束时移除。
    /// </summary>
    public class ForceTileSelectionOverride : ITargetSelectorOverride
    {
        private readonly TileTargetSelector _selector;

        public ForceTileSelectionOverride(List<Vector2Int> validTiles)
        {
            _selector = new TileTargetSelector(validTiles);
        }

        public bool TryOverride(AttackContext context, out ITargetSelector result)
        {
            result = _selector;
            return true;
        }
    }
}
