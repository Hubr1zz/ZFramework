using System;
using System.Collections.Generic;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.Board;
using GameplayBase.Card.CharacterActionCard;
using HuntingInDarkness.Combat;

namespace HuntingInDarkness.ActionFlow.Combat
{
    /// <summary>单场战斗独占的 ActionQueue 环境；战斗对象和状态仍由 PlayableCombatSession 持有。</summary>
    public sealed class PlayableCombatActionSession : IDisposable
    {
        private readonly IGameContext gameContext;
        private readonly IBoardQuery boardQuery;
        private readonly IBoardCommand boardCommand;
        private readonly ActionCardCostService costService;
        private readonly FlipConditionEvaluator flipEvaluator;
        private readonly Func<int, bool> canOwnerAct;
        private readonly Action<int, int> applyTimePointReward;
        private readonly Action resetPlayerTurn;
        private readonly ActionEnvironment environment;

        public PlayableCombatActionSession(IGameContext gameContext, IBoardQuery boardQuery, IBoardCommand boardCommand, ActionCardCostService costService, FlipConditionEvaluator flipEvaluator, Func<int, bool> canOwnerAct, Action<int, int> applyTimePointReward, Action resetPlayerTurn)
        {
            this.gameContext = gameContext ?? throw new ArgumentNullException(nameof(gameContext));
            this.boardQuery = boardQuery;
            this.boardCommand = boardCommand;
            this.costService = costService ?? throw new ArgumentNullException(nameof(costService));
            this.flipEvaluator = flipEvaluator ?? throw new ArgumentNullException(nameof(flipEvaluator));
            this.canOwnerAct = canOwnerAct ?? throw new ArgumentNullException(nameof(canOwnerAct));
            this.applyTimePointReward = applyTimePointReward ?? throw new ArgumentNullException(nameof(applyTimePointReward));
            this.resetPlayerTurn = resetPlayerTurn ?? throw new ArgumentNullException(nameof(resetPlayerTurn));
            environment = new ActionEnvironment(new ActionEnvironmentConfiguration
            {
                Name = "Combat",
                Kind = ActionEnvironmentKind.Combat,
                MaxActionsPerChain = 256,
                TraceCapacity = 48
            });
        }

        public bool IsActive => !environment.IsDisposed;
        public ReactorRegistry Reactors => environment.Reactors;
        public ReactionGateRegistry ReactionGates => environment.ReactionGates;

        public async UniTask<CombatCardCommandResult> PlayCardAsync(CharacterActionCardInstance card, int targetEntityId)
        {
            if (!IsActive) return CombatCardCommandResult.Failed("战斗会话已经结束", card?.InstanceId ?? -1);
            if (card == null) return CombatCardCommandResult.Failed("行动卡不存在");

            var outbox = new ActionEventOutbox();
            ReactorEntityHandle source = environment.EntityHandles.GetOrCreate("hunter", card.OwnerCharacterId.ToString(), GetEntityName(card.OwnerCharacterId));
            ReactorEntityHandle target = ResolveTarget(card, targetEntityId);
            var action = new PlayCharacterCardAction(card, targetEntityId, gameContext, boardQuery, boardCommand, costService, flipEvaluator, outbox, canOwnerAct, source, target);
            try
            {
                ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
                if (outcome.IsSuccess) return action.Result;
                return string.IsNullOrWhiteSpace(action.Result.Reason) ? CombatCardCommandResult.Failed(outcome.Reason, card.InstanceId) : action.Result;
            }
            finally
            {
                PlayableActionPreparation.Reset(card.CurrentFace == CardFace.FaceUp ? card.FaceUpEffects : card.FaceDownEffects);
            }
        }

        public async UniTask<BossTurnCommandResult> ExecuteBossTurnAsync(IReadOnlyList<BossActionRequest> requests)
        {
            if (!IsActive) return new BossTurnCommandResult(false, "战斗会话已经结束", 0);
            var outbox = new ActionEventOutbox();
            ReactorEntityHandle boss = environment.EntityHandles.GetOrCreate("boss", gameContext.Boss.Id.ToString(), gameContext.Boss.Name);
            ReactorEntityHandle combat = environment.EntityHandles.GetOrCreate("combat", "active", "战斗");
            var action = new ExecuteBossTurnAction(requests, gameContext, boardQuery, outbox, boss, combat, ResolveEntity);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            return new BossTurnCommandResult(outcome.IsSuccess, outcome.Reason, action.ExecutedCardCount);
        }

        public async UniTask<CardRestoreCommandResult> RestoreCardAsync(CharacterActionCardInstance card)
        {
            if (!IsActive) return CardRestoreCommandResult.Failed("战斗会话已经结束", card?.InstanceId ?? -1);
            if (card == null) return CardRestoreCommandResult.Failed("行动卡不存在");
            var outbox = new ActionEventOutbox();
            ReactorEntityHandle owner = ResolveEntity(card.OwnerCharacterId);
            var action = new RestoreCharacterCardAction(card, costService, flipEvaluator, outbox, owner, owner);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            return outcome.IsSuccess ? action.Result : CardRestoreCommandResult.Failed(outcome.Reason, card.InstanceId);
        }

        public async UniTask<DiscardResult> BurstCardAsync(CharacterActionCardInstance card)
        {
            if (!IsActive || card == null) return DiscardResult.Failed;
            var outbox = new ActionEventOutbox();
            ReactorEntityHandle owner = ResolveEntity(card.OwnerCharacterId);
            var action = new BurstCharacterCardAction(card, gameContext, outbox, applyTimePointReward, owner, owner);
            try
            {
                ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
                return outcome.IsSuccess ? action.Result : DiscardResult.Failed;
            }
            finally
            {
                action.ResetPreparation();
            }
        }

        public async UniTask<bool> BeginPlayerTurnAsync(IReadOnlyList<CharacterActionCardInstance> cards)
        {
            if (!IsActive) return false;
            var orderedCards = cards == null ? new List<CharacterActionCardInstance>() : new List<CharacterActionCardInstance>(cards);
            orderedCards.Sort((left, right) => (left?.InstanceId ?? int.MaxValue).CompareTo(right?.InstanceId ?? int.MaxValue));
            var outbox = new ActionEventOutbox();
            ReactorEntityHandle combat = environment.EntityHandles.GetOrCreate("combat", "active", "战斗");
            var action = new BeginPlayerTurnAction(orderedCards, flipEvaluator, outbox, combat, ResolveEntity, resetPlayerTurn);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            return outcome.IsSuccess;
        }

        public void Dispose() => environment.Dispose();

        private ReactorEntityHandle ResolveTarget(CharacterActionCardInstance card, int targetEntityId)
        {
            if (targetEntityId >= 0)
                return environment.EntityHandles.GetOrCreate("combatant", targetEntityId.ToString(), GetEntityName(targetEntityId));
            foreach (CharacterActionCardEffect effect in card.CurrentFace == CardFace.FaceUp ? card.FaceUpEffects : card.FaceDownEffects)
                if (effect?.TargetType == TargetType.SingleEnemy && gameContext.Boss != null)
                    return environment.EntityHandles.GetOrCreate("boss", gameContext.Boss.Id.ToString(), gameContext.Boss.Name);
            return environment.EntityHandles.GetOrCreate("combat", "active", "战斗");
        }

        private ReactorEntityHandle ResolveEntity(int entityId) => environment.EntityHandles.GetOrCreate("combatant", entityId.ToString(), GetEntityName(entityId));

        private string GetEntityName(int entityId)
        {
            foreach (ICharacterState character in gameContext.PlayerCharacters)
                if (character.Id == entityId)
                    return character.Name;
            if (gameContext.Boss?.Id == entityId) return gameContext.Boss.Name;
            return $"战斗实体 {entityId}";
        }
    }
}
