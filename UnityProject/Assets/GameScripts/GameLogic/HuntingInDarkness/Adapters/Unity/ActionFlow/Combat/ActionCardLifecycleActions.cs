using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.Card.CharacterActionCard;
using GameplayBase.CombatSystem.Cards.FlipConditions;
using HuntingInDarkness.Combat;
using HuntingInDarkness.GameCore.Cards;

namespace HuntingInDarkness.ActionFlow.Combat
{
    public readonly struct CardRestoreCommandResult
    {
        public bool Success { get; }
        public string Reason { get; }
        public int CardInstanceId { get; }

        public CardRestoreCommandResult(bool success, string reason, int cardInstanceId)
        {
            Success = success;
            Reason = reason ?? string.Empty;
            CardInstanceId = cardInstanceId;
        }

        public static CardRestoreCommandResult Failed(string reason, int cardInstanceId = -1) => new(false, reason, cardInstanceId);
    }

    public sealed class RestoreCharacterCardAction : CompositeGameAction, ISourceAction, ITargetAction
    {
        private readonly CharacterActionCardInstance card;
        private readonly ActionCardCostService costService;
        private readonly FlipConditionEvaluator flipEvaluator;
        private readonly ActionEventOutbox eventOutbox;
        private readonly Func<int, IReactorEntity> resolveEntity;
        private ActionCardCostTransaction transaction;
        private FinalizeCardRestoreAction finalizationAction;
        private bool preparationCompleted;
        private bool finalizationScheduled;
        private bool linksScheduled;

        public RestoreCharacterCardAction(CharacterActionCardInstance card, ActionCardCostService costService, FlipConditionEvaluator flipEvaluator, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target, Func<int, IReactorEntity> resolveEntity)
        {
            this.card = card ?? throw new ArgumentNullException(nameof(card));
            this.costService = costService ?? throw new ArgumentNullException(nameof(costService));
            this.flipEvaluator = flipEvaluator ?? throw new ArgumentNullException(nameof(flipEvaluator));
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            this.resolveEntity = resolveEntity ?? throw new ArgumentNullException(nameof(resolveEntity));
        }

        public int CardInstanceId => card.InstanceId;
        public int OwnerCharacterId => card.OwnerCharacterId;
        public CardRestoreCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override GameAction GetNextChild(CompositeExecutionContext context)
        {
            if (context.CompletedCount == 0) return new PrepareCardRestoreAction(this);
            if (!preparationCompleted) return null;
            if (!finalizationScheduled)
            {
                finalizationScheduled = true;
                finalizationAction = new FinalizeCardRestoreAction(this);
                return finalizationAction;
            }
            if (!linksScheduled)
            {
                linksScheduled = true;
                if (context.LastOutcome.IsSuccess && finalizationAction.LinkTriggers.Count > 0)
                    return new ResolveCardLinkChainAction(finalizationAction.LinkTriggers, flipEvaluator, eventOutbox, Source, resolveEntity);
            }
            return null;
        }

        protected override ActionOutcome Resolve(CompositeExecutionContext context)
        {
            if (context.CompletedCount == 0 || !context.LastOutcome.IsSuccess)
            {
                string reason = context.CompletedCount == 0 ? "恢复没有执行" : context.LastOutcome.Reason;
                Result = CardRestoreCommandResult.Failed(reason, card.InstanceId);
                return context.CompletedCount == 0 ? ActionOutcome.Failure(reason) : context.LastOutcome;
            }
            if (!finalizationScheduled)
            {
                Result = CardRestoreCommandResult.Failed("恢复没有完成提交", card.InstanceId);
                return ActionOutcome.Failure(Result.Reason);
            }
            Result = new CardRestoreCommandResult(true, string.Empty, card.InstanceId);
            return ActionOutcome.Success();
        }

        internal async UniTask<ActionOutcome> PrepareAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!flipEvaluator.CanManuallyRestore(card)) return ActionOutcome.Failure("行动卡当前不可主动恢复");

            var costs = new List<ActionCardCostDefinition>();
            foreach (IFlipCondition condition in card.RestoreConditions)
                if (condition is IPreparedActionCardRestoreCost preparedCost)
                    costs.Add(preparedCost.Cost);

            transaction = costs.Count > 0 ? await costService.PrepareInspirationCostsAsync(card.OwnerCharacterId, costs, cancellationToken) : null;
            if (costs.Count > 0 && transaction == null) return ActionOutcome.Cancelled("未能准备恢复费用");
            cancellationToken.ThrowIfCancellationRequested();
            if (!flipEvaluator.CanManuallyRestore(card)) return ActionOutcome.Cancelled("行动卡恢复条件已发生变化");
            preparationCompleted = true;
            return ActionOutcome.Success();
        }

        internal ActionOutcome Commit(ICollection<CardLinkTrigger> linkTriggers)
        {
            if (!flipEvaluator.CanManuallyRestore(card)) return ActionOutcome.Failure("行动卡恢复条件已发生变化");
            var costFlipEvents = new List<CardFlippedEvent>();
            if (!costService.TryCommitWithCardFlipEvents(card.OwnerCharacterId, transaction, costFlipEvents)) return ActionOutcome.Cancelled("恢复费用已发生变化");
            foreach (CardFlippedEvent evt in costFlipEvents)
            {
                eventOutbox.Stage(evt);
                linkTriggers.Add(CardLinkTrigger.Flipped(evt.CardInstanceId, evt.OwnerCharacterId));
            }
            if (!flipEvaluator.TryApplyManualRestore(card, out CardRestoredEvent restoredEvent)) return ActionOutcome.Failure("行动卡恢复提交失败");
            eventOutbox.Stage(restoredEvent);
            linkTriggers.Add(CardLinkTrigger.Restored(restoredEvent.CardInstanceId, restoredEvent.OwnerCharacterId));
            eventOutbox.PublishCheckpoint();
            return ActionOutcome.Success();
        }
    }

    public sealed class PrepareCardRestoreAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly RestoreCharacterCardAction root;

        internal PrepareCardRestoreAction(RestoreCharacterCardAction root) => this.root = root;

        public IReactorEntity Source => root.Source;
        public IReactorEntity Target => root.Target;
        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken) => root.PrepareAsync(cancellationToken);
    }

    public sealed class FinalizeCardRestoreAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly RestoreCharacterCardAction root;
        private readonly List<CardLinkTrigger> linkTriggers = new();

        internal FinalizeCardRestoreAction(RestoreCharacterCardAction root) => this.root = root;

        public IReactorEntity Source => root.Source;
        public IReactorEntity Target => root.Target;
        public IReadOnlyList<CardLinkTrigger> LinkTriggers => linkTriggers;
        public override ReactionPhases OpenReactionPhases => ReactionPhases.AfterResolved;
        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken) => UniTask.FromResult(root.Commit(linkTriggers));
    }

    public sealed class BurstCharacterCardAction : CompositeGameAction, ISourceAction, ITargetAction
    {
        private readonly CharacterActionCardInstance card;
        private readonly IGameContext gameContext;
        private readonly ActionEventOutbox eventOutbox;
        private readonly Action<int, int> applyTimePointReward;
        private readonly FlipConditionEvaluator flipEvaluator;
        private readonly Func<int, IReactorEntity> resolveEntity;
        private readonly List<CharacterActionCardEffect> effects = new();
        private readonly List<GameAction> effectActions = new();
        private FinalizeCardBurstAction finalizationAction;
        private int effectIndex;
        private bool preparationCompleted;
        private bool finalizationScheduled;
        private bool linksScheduled;

        public BurstCharacterCardAction(CharacterActionCardInstance card, IGameContext gameContext, ActionEventOutbox eventOutbox, Action<int, int> applyTimePointReward, FlipConditionEvaluator flipEvaluator, IReactorEntity source, IReactorEntity target, Func<int, IReactorEntity> resolveEntity)
        {
            this.card = card ?? throw new ArgumentNullException(nameof(card));
            this.gameContext = gameContext ?? throw new ArgumentNullException(nameof(gameContext));
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            this.applyTimePointReward = applyTimePointReward ?? throw new ArgumentNullException(nameof(applyTimePointReward));
            this.flipEvaluator = flipEvaluator ?? throw new ArgumentNullException(nameof(flipEvaluator));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            this.resolveEntity = resolveEntity ?? throw new ArgumentNullException(nameof(resolveEntity));
        }

        public int CardInstanceId => card.InstanceId;
        public int OwnerCharacterId => card.OwnerCharacterId;
        public DiscardResult Result { get; private set; }
        public string FailureReason { get; private set; } = string.Empty;
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override GameAction GetNextChild(CompositeExecutionContext context)
        {
            if (context.CompletedCount == 0) return new PrepareCardBurstAction(this);
            if (!preparationCompleted) return null;
            if (effectIndex < effectActions.Count) return effectActions[effectIndex++];
            if (!finalizationScheduled)
            {
                finalizationScheduled = true;
                finalizationAction = new FinalizeCardBurstAction(this);
                return finalizationAction;
            }
            if (!linksScheduled)
            {
                linksScheduled = true;
                if (context.LastOutcome.IsSuccess)
                    return new ResolveCardLinkChainAction(finalizationAction.LinkTriggers, flipEvaluator, eventOutbox, Source, resolveEntity);
            }
            return null;
        }

        protected override ActionOutcome Resolve(CompositeExecutionContext context)
        {
            ResetPreparation();
            if (context.CompletedCount == 0 || !finalizationScheduled)
            {
                FailureReason = context.CompletedCount == 0 ? "爆发没有执行" : context.LastOutcome.Reason;
                return context.CompletedCount == 0 ? ActionOutcome.Failure(FailureReason) : context.LastOutcome;
            }
            if (!context.LastOutcome.IsSuccess)
            {
                FailureReason = context.LastOutcome.Reason;
                return context.LastOutcome;
            }
            return ActionOutcome.Success();
        }

        internal async UniTask<ActionOutcome> PrepareAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanStart()) return ActionOutcome.Failure("行动卡当前不能爆发");

            var effectContext = new ActionCardContext
            {
                SourceCharacterId = card.OwnerCharacterId,
                TargetEntityId = card.OwnerCharacterId,
                GameContext = gameContext
            };
            effects.Clear();
            effectActions.Clear();
            if (card.BurstReward?.bonusEffects != null)
            {
                foreach (CharacterActionCardEffectData effectData in card.BurstReward.bonusEffects)
                {
                    CharacterActionCardEffect effect = effectData?.CreateRuntime();
                    if (effect == null) continue;
                    effect.Targeting = effectData.targeting;
                    effects.Add(effect);
                }
            }

            foreach (CharacterActionCardEffect effect in effects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (effect is not IPlayablePreparedActionEffect prepared) continue;
                prepared.ResetPreparation();
                if (!effect.CanExecute(effectContext) || !await prepared.PrepareAsync(effectContext, cancellationToken))
                {
                    ResetPreparation();
                    return ActionOutcome.Cancelled("爆发奖励选择已取消");
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanStart()) return ActionOutcome.Cancelled("行动卡爆发状态已发生变化");

            foreach (CharacterActionCardEffect effect in effects)
            {
                if (!effect.CanExecute(effectContext)) continue;
                if (effect is IPlayableQueuedActionEffect queuedEffect)
                {
                    GameAction action = queuedEffect.CreateAction(effectContext, eventOutbox, Source, Target);
                    if (action == null)
                    {
                        ResetPreparation();
                        return ActionOutcome.Cancelled($"无法创建爆发奖励：{effect.Description}");
                    }
                    effectActions.Add(action);
                    continue;
                }
                effectActions.Add(new ExecuteCharacterCardEffectAction(effect, effectContext, Source, Target));
            }
            preparationCompleted = true;
            return ActionOutcome.Success();
        }

        internal ActionOutcome Commit(ICollection<CardLinkTrigger> linkTriggers)
        {
            if (!CanStart()) return ActionOutcome.Failure("行动卡爆发状态已发生变化");
            BurstRewardData reward = card.BurstReward;
            CardFace oldFace = card.CurrentFace;
            card.SetFace(CardFace.FaceDown);
            eventOutbox.Stage(new CardFlippedEvent
            {
                CardInstanceId = card.InstanceId,
                OwnerCharacterId = card.OwnerCharacterId,
                OldFace = oldFace,
                NewFace = CardFace.FaceDown
            });
            linkTriggers.Add(CardLinkTrigger.Flipped(card.InstanceId, card.OwnerCharacterId));
            eventOutbox.Stage(new CardDiscardedEvent
            {
                CardInstanceId = card.InstanceId,
                OwnerCharacterId = card.OwnerCharacterId,
                CurrencyReward = reward.currencyReward,
                TimePointReward = reward.timePointReward
            });
            linkTriggers.Add(CardLinkTrigger.Discarded(card.InstanceId, card.OwnerCharacterId));
            eventOutbox.PublishCheckpoint();
            if (reward.timePointReward != 0)
                applyTimePointReward(card.OwnerCharacterId, reward.timePointReward);
            Result = new DiscardResult
            {
                Success = true,
                CardInstanceId = card.InstanceId,
                OwnerCharacterId = card.OwnerCharacterId,
                CurrencyReward = reward.currencyReward,
                TimePointReward = reward.timePointReward
            };
            return ActionOutcome.Success();
        }

        internal void ResetPreparation() => PlayableActionPreparation.Reset(effects);

        private bool CanStart() => card.CurrentFace == CardFace.FaceUp && card.CanDiscard && card.BurstReward?.enabled == true;

    }

    public sealed class PrepareCardBurstAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly BurstCharacterCardAction root;

        internal PrepareCardBurstAction(BurstCharacterCardAction root) => this.root = root;

        public IReactorEntity Source => root.Source;
        public IReactorEntity Target => root.Target;
        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken) => root.PrepareAsync(cancellationToken);
    }

    public sealed class FinalizeCardBurstAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly BurstCharacterCardAction root;
        private readonly List<CardLinkTrigger> linkTriggers = new();

        internal FinalizeCardBurstAction(BurstCharacterCardAction root) => this.root = root;

        public IReactorEntity Source => root.Source;
        public IReactorEntity Target => root.Target;
        public IReadOnlyList<CardLinkTrigger> LinkTriggers => linkTriggers;
        public override ReactionPhases OpenReactionPhases => ReactionPhases.AfterResolved;
        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken) => UniTask.FromResult(root.Commit(linkTriggers));
    }
}
