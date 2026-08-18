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
        private readonly ActionEnvironment environment;

        public PlayableCombatActionSession(IGameContext gameContext, IBoardQuery boardQuery, IBoardCommand boardCommand, ActionCardCostService costService, FlipConditionEvaluator flipEvaluator, Func<int, bool> canOwnerAct)
        {
            this.gameContext = gameContext ?? throw new ArgumentNullException(nameof(gameContext));
            this.boardQuery = boardQuery;
            this.boardCommand = boardCommand;
            this.costService = costService ?? throw new ArgumentNullException(nameof(costService));
            this.flipEvaluator = flipEvaluator ?? throw new ArgumentNullException(nameof(flipEvaluator));
            this.canOwnerAct = canOwnerAct ?? throw new ArgumentNullException(nameof(canOwnerAct));
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
