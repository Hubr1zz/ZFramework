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

    /// <summary>一次行动卡意图的权威 Root。准备、效果和提交均展开为同一因果链里的 Child Actions。</summary>
    public sealed class PlayCharacterCardAction : CompositeGameAction, ISourceAction, ITargetAction
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
        private readonly Func<int, IReactorEntity> resolveEntity;
        private readonly List<CharacterActionCardEffect> effects;
        private readonly List<GameAction> effectActions = new();
        private readonly List<CardLinkTrigger> costLinkTriggers = new();
        private ActionCardContext effectContext;
        private FinalizeCharacterCardAction finalizationAction;
        private int effectIndex;
        private bool preparationCompleted;
        private bool costLinksScheduled;
        private bool finalizationScheduled;
        private bool finalLinksScheduled;

        public PlayCharacterCardAction(CharacterActionCardInstance card, int targetEntityId, IGameContext gameContext, IBoardQuery boardQuery, IBoardCommand boardCommand, ActionCardCostService costService, FlipConditionEvaluator flipEvaluator, ActionEventOutbox eventOutbox, Func<int, bool> canOwnerAct, IReactorEntity sourceEntity, IReactorEntity targetEntity, Func<int, IReactorEntity> resolveEntity)
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
            this.resolveEntity = resolveEntity ?? throw new ArgumentNullException(nameof(resolveEntity));
            effects = GetEffects();
            IsAttack = effects.Exists(effect => effect?.TargetType == TargetType.SingleEnemy);
        }

        public int CardInstanceId => card.InstanceId;
        public int OwnerCharacterId => card.OwnerCharacterId;
        public int TargetEntityId => targetEntityId;
        public bool IsAttack { get; }
        public CombatCardCommandResult Result { get; private set; }
        public IReactorEntity Source => sourceEntity;
        public IReactorEntity Target => targetEntity;

        protected override GameAction GetNextChild(CompositeExecutionContext context)
        {
            if (context.CompletedCount == 0)
                return new PrepareCharacterCardAction(this);
            if (!preparationCompleted)
                return null;

            if (!costLinksScheduled)
            {
                costLinksScheduled = true;
                if (costLinkTriggers.Count > 0)
                    return new ResolveCardLinkChainAction(costLinkTriggers, flipEvaluator, eventOutbox, sourceEntity, resolveEntity);
            }

            if (effectIndex < effectActions.Count)
                return effectActions[effectIndex++];

            if (!finalizationScheduled)
            {
                finalizationScheduled = true;
                finalizationAction = new FinalizeCharacterCardAction(card, flipEvaluator, eventOutbox, IsAttack, sourceEntity, targetEntity);
                return finalizationAction;
            }
            if (!finalLinksScheduled)
            {
                finalLinksScheduled = true;
                if (context.LastOutcome.IsSuccess && finalizationAction.LinkTrigger.HasValue)
                    return new ResolveCardLinkChainAction(new[] { finalizationAction.LinkTrigger.Value }, flipEvaluator, eventOutbox, sourceEntity, resolveEntity);
            }
            return null;
        }

        protected override ActionOutcome Resolve(CompositeExecutionContext context)
        {
            PlayableActionPreparation.Reset(effects);
            if (context.CompletedCount == 0)
            {
                Result = CombatCardCommandResult.Failed("行动没有执行", card.InstanceId);
                return ActionOutcome.Failure(Result.Reason);
            }
            if (!context.LastOutcome.IsSuccess)
            {
                Result = CombatCardCommandResult.Failed(context.LastOutcome.Reason, card.InstanceId);
                return context.LastOutcome;
            }
            if (!finalizationScheduled)
            {
                Result = CombatCardCommandResult.Failed("行动没有完成提交", card.InstanceId);
                return ActionOutcome.Failure(Result.Reason);
            }
            Result = new CombatCardCommandResult(true, string.Empty, card.InstanceId);
            return ActionOutcome.Success();
        }

        internal async UniTask<ActionOutcome> PrepareAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanStart(out string reason)) return ActionOutcome.Failure(reason);
            effectContext = new ActionCardContext
            {
                SourceCharacterId = card.OwnerCharacterId,
                TargetEntityId = targetEntityId,
                GameContext = gameContext,
                BoardQuery = boardQuery,
                BoardCommand = boardCommand
            };

            ActionCardCostTransaction transaction = await costService.PrepareAsync(card, cancellationToken);
            if (transaction == null) return ActionOutcome.Cancelled("未能准备行动费用");
            foreach (CharacterActionCardEffect effect in effects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (effect is not IPlayablePreparedActionEffect prepared) continue;
                prepared.ResetPreparation();
                if (!effect.CanExecute(effectContext) || !await prepared.PrepareAsync(effectContext, cancellationToken))
                {
                    PlayableActionPreparation.Reset(effects);
                    return ActionOutcome.Cancelled("行动准备已取消");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!CanStart(out reason)) return ActionOutcome.Cancelled(reason);
            effectActions.Clear();
            foreach (CharacterActionCardEffect effect in effects)
            {
                if (effect == null || !effect.CanExecute(effectContext)) continue;
                if (effect is IPlayableQueuedActionEffect queuedEffect)
                {
                    GameAction queuedAction = queuedEffect.CreateAction(effectContext, eventOutbox, sourceEntity, targetEntity);
                    if (queuedAction == null)
                    {
                        PlayableActionPreparation.Reset(effects);
                        return ActionOutcome.Cancelled($"无法创建行动效果：{effect.Description}");
                    }
                    effectActions.Add(queuedAction);
                    continue;
                }
                effectActions.Add(new ExecuteCharacterCardEffectAction(effect, effectContext, sourceEntity, targetEntity));
            }
            var costFlipEvents = new List<CardFlippedEvent>();
            if (!costService.TryCommitWithCardFlipEvents(card.OwnerCharacterId, transaction, costFlipEvents)) return ActionOutcome.Cancelled("行动费用已发生变化");
            foreach (CardFlippedEvent evt in costFlipEvents)
            {
                eventOutbox.Stage(evt);
                costLinkTriggers.Add(CardLinkTrigger.Flipped(evt.CardInstanceId, evt.OwnerCharacterId));
            }
            if (costFlipEvents.Count > 0)
                eventOutbox.PublishCheckpoint();
            preparationCompleted = true;
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
            foreach (CharacterActionCardEffect effect in effects)
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

        private List<CharacterActionCardEffect> GetEffects() => card.CurrentFace == CardFace.FaceUp ? card.FaceUpEffects : card.FaceDownEffects;
    }

    public sealed class PrepareCharacterCardAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly PlayCharacterCardAction root;

        internal PrepareCharacterCardAction(PlayCharacterCardAction root)
        {
            this.root = root;
        }

        public IReactorEntity Source => root.Source;
        public IReactorEntity Target => root.Target;
        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken) => root.PrepareAsync(cancellationToken);
    }

    public sealed class ExecuteCharacterCardEffectAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly CharacterActionCardEffect effect;
        private readonly ActionCardContext effectContext;

        internal ExecuteCharacterCardEffectAction(CharacterActionCardEffect effect, ActionCardContext effectContext, IReactorEntity source, IReactorEntity target)
        {
            this.effect = effect;
            this.effectContext = effectContext;
            Source = source;
            Target = target;
        }

        public override string DebugName => $"CardEffect:{effect.GetType().Name}";
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            if (effect is IPlayablePreparedActionEffect prepared)
            {
                if (!prepared.IsPrepared) return ActionOutcome.Failure("行动效果没有完成准备");
                try
                {
                    await context.AwaitPresentationAsync(prepared.ExecutePreparedAsync(effectContext, cancellationToken));
                }
                finally
                {
                    prepared.ResetPreparation();
                }
                return ActionOutcome.Success();
            }
            if (effect is IPlayableCancellableActionEffect cancellableEffect)
            {
                await context.AwaitPresentationAsync(cancellableEffect.ExecuteAsync(effectContext, cancellationToken));
                return ActionOutcome.Success();
            }
            await context.AwaitPresentationAsync(effect.ExecuteAsync(effectContext).AttachExternalCancellation(cancellationToken));
            return ActionOutcome.Success();
        }
    }

    public sealed class FinalizeCharacterCardAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly CharacterActionCardInstance card;
        private readonly FlipConditionEvaluator flipEvaluator;
        private readonly ActionEventOutbox eventOutbox;
        private readonly bool isAttack;

        internal FinalizeCharacterCardAction(CharacterActionCardInstance card, FlipConditionEvaluator flipEvaluator, ActionEventOutbox eventOutbox, bool isAttack, IReactorEntity source, IReactorEntity target)
        {
            this.card = card;
            this.flipEvaluator = flipEvaluator;
            this.eventOutbox = eventOutbox;
            this.isAttack = isAttack;
            Source = source;
            Target = target;
        }

        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }
        public CardLinkTrigger? LinkTrigger { get; private set; }
        public override ReactionPhases OpenReactionPhases => ReactionPhases.AfterResolved;

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            if (!card.CanPlay) return UniTask.FromResult(ActionOutcome.Failure("行动卡状态已发生变化"));
            card.MarkUsed();
            eventOutbox.Stage(new CardPlayedEvent
            {
                CardInstanceId = card.InstanceId,
                OwnerCharacterId = card.OwnerCharacterId,
                TimePointCost = card.TimePointCost
            });
            if (flipEvaluator.TryApplyAfterCardPlayed(card.InstanceId, card.OwnerCharacterId, out CardFlippedEvent flippedEvent))
            {
                eventOutbox.Stage(flippedEvent);
                LinkTrigger = CardLinkTrigger.Flipped(flippedEvent.CardInstanceId, flippedEvent.OwnerCharacterId);
            }
            eventOutbox.Stage(new CombatActionCommittedEvent
            {
                CardInstanceId = card.InstanceId,
                OwnerCharacterId = card.OwnerCharacterId,
                IsAttack = isAttack
            });
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }
}
