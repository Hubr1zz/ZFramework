using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.Card.CharacterActionCard;
using GameplayBase.Card.Effect;
using GameplayBase.CombatSystem;
using HuntingInDarkness.ActionFlow;
using SO.Character;
using UnityEngine;

namespace HuntingInDarkness.Combat
{
    [System.Serializable]
    public sealed class PlayableMoveEffectData : CharacterActionCardEffectData
    {
        [Min(1)] public int moveRange = 2;
        public override CharacterActionCardEffect CreateRuntime() => new PlayablePreparedMoveEffect(moveRange);
    }

    public sealed class PlayablePreparedMoveEffect : CharacterActionCardEffect, IPlayablePreparedActionEffect
    {
        private readonly int moveRange;
        private Vector2Int origin;
        private Vector2Int target;

        public override string Description => $"移动最多 {moveRange} 格";
        public override TargetType TargetType => TargetType.BoardTile;
        public bool IsPrepared { get; private set; }

        public PlayablePreparedMoveEffect(int moveRange)
        {
            this.moveRange = Mathf.Max(1, moveRange);
        }

        public override bool CanExecute(ActionCardContext context) => context?.BoardQuery != null && context.BoardCommand != null;
        public override void Execute(ActionCardContext context) => ExecuteAsync(context).Forget();
        public override UniTask ExecuteAsync(ActionCardContext context) => ExecutePreparedAsync(context);

        public async UniTask<bool> PrepareAsync(ActionCardContext context, CancellationToken cancellationToken = default)
        {
            var input = GetInput(context);
            if (input == null) return false;

            origin = context.BoardQuery.GetEntityPosition(context.SourceCharacterId);
            if (!context.BoardQuery.IsValidTile(origin)) return false;

            List<Vector2Int> candidates = context.BoardQuery.GetTilesInRange(origin, moveRange);
            candidates.RemoveAll(tile => tile == origin || context.BoardQuery.GetEntityAt(tile).HasValue);
            if (candidates.Count == 0)
            {
                await input.ShowResult("附近没有可以移动到的空地块。");
                return false;
            }

            Vector2Int? selected = await input.RequestSelectTile($"选择移动目标（范围 {moveRange}）", candidates, cancellationToken);
            if (!selected.HasValue || !candidates.Contains(selected.Value)) return false;

            target = selected.Value;
            IsPrepared = true;
            return true;
        }

        public UniTask ExecutePreparedAsync(ActionCardContext context, CancellationToken cancellationToken = default)
        {
            if (!IsPrepared) return UniTask.CompletedTask;

            context.BoardCommand.MoveEntity(context.SourceCharacterId, target);
            EventBus.Publish(new EntityMovedEvent { EntityId = context.SourceCharacterId, FromTile = origin, ToTile = target });
            return UniTask.CompletedTask;
        }

        public void ResetPreparation()
        {
            IsPrepared = false;
            origin = default;
            target = default;
        }

        private static IPlayerInputProvider GetInput(ActionCardContext context)
        {
            if (context?.GameContext is ICombatProvider provider) return provider.CombatManager?.InputProvider;
            return null;
        }
    }

    public sealed class PlayablePreparedAttackEffect : CharacterActionCardEffect, IPlayablePreparedActionEffect, IPlayableQueuedActionEffect
    {
        private readonly PlayableLoadoutWeaponResolver weaponResolver = new();
        private CardTactics.CombatSystem.CombatManager combatManager;
        private CharacterCombatStats stats;
        private WeaponData weapon;

        public override string Description => "攻击Boss";
        public override TargetType TargetType => TargetType.SingleEnemy;
        public bool IsPrepared { get; private set; }

        public override bool CanExecute(ActionCardContext context) => context?.GameContext is ICombatProvider && context.GameContext is ICombatRuntimeDataProvider;
        public override void Execute(ActionCardContext context) => ExecuteAsync(context).Forget();
        public override UniTask ExecuteAsync(ActionCardContext context) => ExecutePreparedAsync(context);

        public async UniTask<bool> PrepareAsync(ActionCardContext context, CancellationToken cancellationToken = default)
        {
            if (context.GameContext is not ICombatProvider combatProvider || context.GameContext is not ICombatRuntimeDataProvider combatData) return false;
            combatManager = combatProvider.CombatManager;
            stats = combatData.GetCharacterData(context.SourceCharacterId)?.CombatStats;
            if (combatManager == null || stats == null) return false;

            weapon = await weaponResolver.ResolveAsync(context, combatManager.InputProvider, cancellationToken);
            IsPrepared = weapon != null;
            return IsPrepared;
        }

        public async UniTask ExecutePreparedAsync(ActionCardContext context, CancellationToken cancellationToken = default)
        {
            if (!IsPrepared) return;
            await combatManager.CharacterAttackBoss(context.SourceCharacterId, stats, weapon, cancellationToken);
        }

        public GameAction CreateAction(ActionCardContext context, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            if (!IsPrepared || combatManager == null) return null;
            return combatManager.CreateCharacterAttackAction(context.SourceCharacterId, stats, weapon, eventOutbox, source, target);
        }

        public void ResetPreparation()
        {
            IsPrepared = false;
            combatManager = null;
            stats = null;
            weapon = null;
        }
    }

    [System.Serializable]
    public sealed class PlayableEncourageEffectData : CharacterActionCardEffectData
    {
        public override CharacterActionCardEffect CreateRuntime() => new PlayablePreparedEncourageEffect();
    }

    public sealed class PlayablePreparedEncourageEffect : CharacterActionCardEffect, IPlayablePreparedActionEffect
    {
        private ICombatRuntimeDataProvider combatData;
        private ICombatActionCommands combatCommands;
        private IPlayerInputProvider input;
        private int targetId = -1;

        public override string Description => "使一名加班中的队友恢复 1 时点";
        public override TargetType TargetType => TargetType.SingleAlly;
        public bool IsPrepared { get; private set; }

        public override bool CanExecute(ActionCardContext context) => context?.GameContext is ICombatProvider && context.GameContext is ICombatRuntimeDataProvider && context.GameContext is ICombatActionCommands;
        public override void Execute(ActionCardContext context) => ExecuteAsync(context).Forget();
        public override UniTask ExecuteAsync(ActionCardContext context) => ExecutePreparedAsync(context);

        public async UniTask<bool> PrepareAsync(ActionCardContext context, CancellationToken cancellationToken = default)
        {
            if (context?.GameContext is not ICombatProvider combatProvider || context.GameContext is not ICombatRuntimeDataProvider runtimeData || context.GameContext is not ICombatActionCommands commands) return false;
            combatData = runtimeData;
            combatCommands = commands;
            input = combatProvider.CombatManager?.InputProvider;
            if (input == null) return false;

            var candidates = new List<int>();
            foreach (ICharacterState character in context.GameContext.PlayerCharacters)
                if (character.Id != context.SourceCharacterId && combatCommands.GetTimelineStatus(character.Id) == HuntingInDarkness.GameCore.Combat.TimelineActionStatus.Overtime)
                    candidates.Add(character.Id);

            if (candidates.Count == 0)
            {
                await input.ShowResult("当前没有加班中的队友需要鼓舞。", cancellationToken);
                return false;
            }

            int selectedId = await input.RequestSelectTarget("选择要鼓舞的队友", candidates, cancellationToken);
            if (!candidates.Contains(selectedId)) return false;

            targetId = selectedId;
            IsPrepared = true;
            return true;
        }

        public async UniTask ExecutePreparedAsync(ActionCardContext context, CancellationToken cancellationToken = default)
        {
            if (!IsPrepared || combatData == null || combatCommands == null || input == null) return;

            string targetName = combatData.GetCharacterData(targetId)?.Name ?? $"猎人 #{targetId}";
            bool succeeded = combatCommands.TryRelieveOvertimeCharacter(targetId);
            await input.ShowResult(succeeded ? $"{targetName} 受到鼓舞，恢复了 1 时点。" : "鼓舞未能生效，目标状态已经改变。", cancellationToken);
        }

        public void ResetPreparation()
        {
            combatData = null;
            combatCommands = null;
            input = null;
            targetId = -1;
            IsPrepared = false;
        }
    }
}
