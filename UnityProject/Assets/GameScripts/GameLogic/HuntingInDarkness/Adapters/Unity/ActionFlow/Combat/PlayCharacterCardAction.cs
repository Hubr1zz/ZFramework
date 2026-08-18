using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.Board;
using GameplayBase.Card.CharacterActionCard;
using HuntingInDarkness.Combat;
using HuntingInDarkness.GameCore.Cards;

namespace HuntingInDarkness.ActionFlow.Combat
{
    public readonly struct CombatCardCommandResult
    {
        public bool Success { get; }
        public string Reason { get; }
        public int CardInstanceId { get; }

        public CombatCardCommandResult(bool success, string reason, int cardInstanceId)
        {
            Success = success;
            Reason = reason ?? string.Empty;
            CardInstanceId = cardInstanceId;
        }

        public static CombatCardCommandResult Failed(string reason, int cardInstanceId = -1) => new(false, reason, cardInstanceId);
    }

    /// <summary>
    /// 一次玩家行动卡意图的权威 Root。准备阶段只收集选择；所有费用重验通过后，
    /// 才依次执行效果、提交卡牌状态并暂存提交后事实。
    /// </summary>
    public sealed class PlayCharacterCardAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly CharacterActionCardInstance card;
        private readonly int targetEntityId;
        private readonly IGameContext gameContext;
        private readonly IBoardQuery boardQuery;
        private readonly IBoardCommand boardCommand;
        private readonly ActionCardCostService costService;
        private readonly FlipConditionEvaluator flipEvaluator;
        private readonly ActionEventOutbox eventOutbox;
        private readonly Func<int, bool> canOwnerAct;
        private readonly IReactorEntity sourceEntity;
        private readonly IReactorEntity targetEntity;

        public PlayCharacterCardAction(CharacterActionCardInstance card, int targetEntityId, IGameContext gameContext, IBoardQuery boardQuery, IBoardCommand boardCommand, ActionCardCostService costService, FlipConditionEvaluator flipEvaluator, ActionEventOutbox eventOutbox, Func<int, bool> canOwnerAct, IReactorEntity sourceEntity, IReactorEntity targetEntity)
        {
            this.card = card ?? throw new ArgumentNullException(nameof(card));
            this.targetEntityId = targetEntityId;
            this.gameContext = gameContext ?? throw new ArgumentNullException(nameof(gameContext));
            this.boardQuery = boardQuery;
            this.boardCommand = boardCommand;
            this.costService = costService ?? throw new ArgumentNullException(nameof(costService));
            this.flipEvaluator = flipEvaluator ?? throw new ArgumentNullException(nameof(flipEvaluator));
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            this.canOwnerAct = canOwnerAct ?? throw new ArgumentNullException(nameof(canOwnerAct));
            this.sourceEntity = sourceEntity ?? throw new ArgumentNullException(nameof(sourceEntity));
            this.targetEntity = targetEntity ?? throw new ArgumentNullException(nameof(targetEntity));
            IsAttack = GetEffects().Exists(effect => effect?.TargetType == TargetType.SingleEnemy);
        }

        public int CardInstanceId => card.InstanceId;
        public int OwnerCharacterId => card.OwnerCharacterId;
        public int TargetEntityId => targetEntityId;
        public bool IsAttack { get; }
        public CombatCardCommandResult Result { get; private set; }
        public IReactorEntity Source => sourceEntity;
        public IReactorEntity Target => targetEntity;

        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanStart(out string reason)) return Fail(reason);

            List<CharacterActionCardEffect> effects = GetEffects();
            var effectContext = new ActionCardContext
            {
                SourceCharacterId = card.OwnerCharacterId,
                TargetEntityId = targetEntityId,
                GameContext = gameContext,
                BoardQuery = boardQuery,
                BoardCommand = boardCommand,
                ActionQueue = null
            };

            ActionCardCostTransaction transaction = await costService.PrepareAsync(card);
            if (transaction == null) return Cancel("未能准备行动费用", effects);
            if (!await PrepareEffectsAsync(effects, effectContext, cancellationToken)) return Cancel("行动准备已取消", effects);
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanStart(out reason)) return Cancel(reason, effects);
            if (!transaction.TryCommit(card.OwnerCharacterId, costService)) return Cancel("行动费用已发生变化", effects);

            try
            {
                foreach (CharacterActionCardEffect effect in effects)
                {
                    if (effect == null) continue;
                    if (effect is IPlayablePreparedActionEffect prepared)
                    {
                        if (!prepared.IsPrepared) return Fail("行动效果没有完成准备");
                        try
                        {
                            await context.AwaitPresentationAsync(prepared.ExecutePreparedAsync(effectContext));
                        }
                        finally
                        {
                            prepared.ResetPreparation();
                        }
                        continue;
                    }
                    if (!effect.CanExecute(effectContext)) continue;
                    await context.AwaitPresentationAsync(effect.ExecuteAsync(effectContext));
                }
            }
            finally
            {
                PlayableActionPreparation.Reset(effects);
            }

            card.MarkUsed();
            eventOutbox.Stage(new CardPlayedEvent
            {
                CardInstanceId = card.InstanceId,
                OwnerCharacterId = card.OwnerCharacterId,
                TimePointCost = card.TimePointCost
            });
            if (flipEvaluator.TryApplyAfterCardPlayed(card.InstanceId, card.OwnerCharacterId, out CardFlippedEvent flippedEvent))
                eventOutbox.Stage(flippedEvent);
            eventOutbox.Stage(new CombatActionCommittedEvent
            {
                CardInstanceId = card.InstanceId,
                OwnerCharacterId = card.OwnerCharacterId,
                IsAttack = IsAttack
            });
            Result = new CombatCardCommandResult(true, string.Empty, card.InstanceId);
            return ActionOutcome.Success();
        }

        private bool CanStart(out string reason)
        {
            if (!card.CanPlay)
            {
                reason = "行动卡当前不可使用";
                return false;
            }
            if (!canOwnerAct(card.OwnerCharacterId))
            {
                reason = "猎人当前无法行动";
                return false;
            }
            foreach (CharacterActionCardEffect effect in GetEffects())
            {
                if (effect?.Targeting == null) continue;
                IReadOnlyList<UnityEngine.Vector2Int> tiles = effect.Targeting.GetValidTiles(boardQuery, card.OwnerCharacterId);
                if (tiles.Count > 0) continue;
                reason = "没有合法目标";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private async UniTask<bool> PrepareEffectsAsync(IReadOnlyList<CharacterActionCardEffect> effects, ActionCardContext effectContext, CancellationToken cancellationToken)
        {
            foreach (CharacterActionCardEffect effect in effects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (effect is not IPlayablePreparedActionEffect prepared) continue;
                prepared.ResetPreparation();
                if (!effect.CanExecute(effectContext) || !await prepared.PrepareAsync(effectContext)) return false;
            }
            return true;
        }

        private List<CharacterActionCardEffect> GetEffects() => card.CurrentFace == CardFace.FaceUp ? card.FaceUpEffects : card.FaceDownEffects;

        private ActionOutcome Cancel(string reason, IReadOnlyList<CharacterActionCardEffect> effects)
        {
            PlayableActionPreparation.Reset(effects);
            Result = CombatCardCommandResult.Failed(reason, card.InstanceId);
            return ActionOutcome.Cancelled(reason);
        }

        private ActionOutcome Fail(string reason)
        {
            Result = CombatCardCommandResult.Failed(reason, card.InstanceId);
            return ActionOutcome.Failure(reason);
        }
    }
}
