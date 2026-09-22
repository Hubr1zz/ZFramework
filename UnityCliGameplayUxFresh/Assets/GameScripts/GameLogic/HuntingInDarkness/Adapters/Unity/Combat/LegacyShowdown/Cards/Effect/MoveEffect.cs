using Core;
using Cysharp.Threading.Tasks;
using GameplayBase.Card.CharacterActionCard;
using GameplayBase.CombatSystem;
using UnityEngine;

namespace GameplayBase.Card.Effect
{
    /// <summary>移动效果数据（在 CardData Inspector 中配置）</summary>
    [System.Serializable]
    public class MoveEffectData : CharacterActionCardEffectData
    {
        [UnityEngine.Min(1)] public int moveRange = 3;
        public override CharacterActionCardEffect CreateRuntime() => new MoveEffect(moveRange);
    }

    /// <summary>
    /// 让角色移动到棋盘上的目标格子。
    /// 打出卡牌后，等待玩家点击有效格子；右键取消（卡牌仍被消耗）。
    /// </summary>
    public class MoveEffect : CharacterActionCardEffect
    {
        private readonly int _moveRange;

        public override string     Description => $"移动最多 {_moveRange} 格";
        public override TargetType TargetType  => TargetType.BoardTile;

        public MoveEffect(int moveRange) => _moveRange = moveRange;

        public override bool CanExecute(ActionCardContext context) =>
            context.BoardQuery != null && context.BoardCommand != null;

        public override void Execute(ActionCardContext context)
        {
            ExecuteAsync(context).Forget();
        }

        public override async UniTask ExecuteAsync(ActionCardContext context)
        {
            var inputProvider = GetInputProvider(context);
            if (inputProvider == null)
            {
                Debug.LogWarning("[MoveEffect] 未找到 IPlayerInputProvider，移动取消。");
                return;
            }

            var origin = context.BoardQuery.GetEntityPosition(context.SourceCharacterId);
            if (!context.BoardQuery.IsValidTile(origin))
            {
                Debug.LogWarning($"[MoveEffect] 角色#{context.SourceCharacterId} 不在棋盘上，移动取消。");
                return;
            }

            var candidates = context.BoardQuery.GetTilesInRange(origin, _moveRange);
            candidates.RemoveAll(t =>
                t == origin || context.BoardQuery.GetEntityAt(t).HasValue);

            if (candidates.Count == 0)
            {
                Debug.Log("[MoveEffect] 无可用移动目标。");
                return;
            }

            Vector2Int? selected = await inputProvider.RequestSelectTile(
                $"选择移动目标（范围 {_moveRange}）", candidates);

            if (!selected.HasValue)
            {
                Debug.Log("[MoveEffect] 玩家取消移动。");
                return;
            }

            context.BoardCommand.MoveEntity(context.SourceCharacterId, selected.Value);

            EventBus.Publish(new EntityMovedEvent
            {
                EntityId = context.SourceCharacterId,
                FromTile = origin,
                ToTile   = selected.Value
            });

            Debug.Log($"[MoveEffect] 角色#{context.SourceCharacterId} " +
                      $"从 {origin} 移动到 {selected.Value}");
        }

        private static IPlayerInputProvider GetInputProvider(ActionCardContext context)
        {
            if (context.GameContext is ICombatProvider provider)
                return provider.CombatManager?.InputProvider;
            Debug.LogWarning("[MoveEffect] GameContext 未实现 ICombatProvider");
            return null;
        }
    }
}
