using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using UnityEngine;

namespace HuntingInDarkness.ActionFlow.Combat
{
    public readonly struct CardLinkTrigger
    {
        public CardLinkTrigger(FlipTriggerTiming timing, int sourceCardId, int sourceOwnerId)
        {
            Timing = timing;
            SourceCardId = sourceCardId;
            SourceOwnerId = sourceOwnerId;
        }

        public FlipTriggerTiming Timing { get; }
        public int SourceCardId { get; }
        public int SourceOwnerId { get; }

        public static CardLinkTrigger Flipped(int cardId, int ownerId) => new(FlipTriggerTiming.OnOtherCardFlipped, cardId, ownerId);
        public static CardLinkTrigger Restored(int cardId, int ownerId) => new(FlipTriggerTiming.OnOtherCardRestored, cardId, ownerId);
        public static CardLinkTrigger Discarded(int cardId, int ownerId) => new(FlipTriggerTiming.OnOtherCardDiscarded, cardId, ownerId);
    }

    /// <summary>按事实 FIFO、卡牌 ID 升序展开跨卡联动；每次实际状态变化仍是独立可响应 Action。</summary>
    public sealed class ResolveCardLinkChainAction : CompositeGameAction, ISourceAction, ITargetAction
    {
        private readonly Queue<CardLinkTrigger> pendingTriggers;
        private readonly FlipConditionEvaluator flipEvaluator;
        private readonly ActionEventOutbox eventOutbox;
        private readonly Func<int, IReactorEntity> resolveOwner;
        private IReadOnlyList<CharacterActionCardInstance> candidates;
        private CardLinkTrigger currentTrigger;
        private ResolveLinkedCardTransitionAction lastAction;
        private int candidateIndex;

        public ResolveCardLinkChainAction(IReadOnlyList<CardLinkTrigger> triggers, FlipConditionEvaluator flipEvaluator, ActionEventOutbox eventOutbox, IReactorEntity combat, Func<int, IReactorEntity> resolveOwner)
        {
            pendingTriggers = new Queue<CardLinkTrigger>(triggers ?? Array.Empty<CardLinkTrigger>());
            this.flipEvaluator = flipEvaluator ?? throw new ArgumentNullException(nameof(flipEvaluator));
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            this.resolveOwner = resolveOwner ?? throw new ArgumentNullException(nameof(resolveOwner));
            Source = combat ?? throw new ArgumentNullException(nameof(combat));
            Target = combat;
        }

        public int ChangedCardCount { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }
        public override ReactionPhases OpenReactionPhases => ReactionPhases.AfterResolved;

        protected override GameAction GetNextChild(CompositeExecutionContext context)
        {
            if (lastAction != null)
            {
                if (context.LastOutcome.IsSuccess && lastAction.Changed)
                {
                    ChangedCardCount++;
                    pendingTriggers.Enqueue(lastAction.CreatedTrigger);
                }
                lastAction = null;
            }

            while (true)
            {
                if (candidates == null)
                {
                    if (pendingTriggers.Count == 0) return null;
                    currentTrigger = pendingTriggers.Dequeue();
                    candidates = flipEvaluator.GetRegisteredCardsInStableOrder();
                    candidateIndex = 0;
                }

                while (candidateIndex < candidates.Count)
                {
                    CharacterActionCardInstance card = candidates[candidateIndex++];
                    if (card == null || card.InstanceId == currentTrigger.SourceCardId || !flipEvaluator.IsLinkedTransitionCandidate(card, currentTrigger.Timing)) continue;
                    lastAction = new ResolveLinkedCardTransitionAction(card, currentTrigger, flipEvaluator, eventOutbox, resolveOwner(currentTrigger.SourceOwnerId), resolveOwner(card.OwnerCharacterId));
                    return lastAction;
                }

                candidates = null;
            }
        }

        protected override ActionOutcome Resolve(CompositeExecutionContext context) => ActionOutcome.Success();
    }

    public sealed class ResolveLinkedCardTransitionAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly CharacterActionCardInstance card;
        private readonly CardLinkTrigger trigger;
        private readonly FlipConditionEvaluator flipEvaluator;
        private readonly ActionEventOutbox eventOutbox;

        internal ResolveLinkedCardTransitionAction(CharacterActionCardInstance card, CardLinkTrigger trigger, FlipConditionEvaluator flipEvaluator, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.card = card;
            this.trigger = trigger;
            this.flipEvaluator = flipEvaluator;
            this.eventOutbox = eventOutbox;
            Source = source;
            Target = target;
        }

        public override string DebugName => $"CardLink:{trigger.Timing}:{trigger.SourceCardId}->{card.InstanceId}";
        public int CardInstanceId => card.InstanceId;
        public int OwnerCharacterId => card.OwnerCharacterId;
        public int TriggerSourceCardId => trigger.SourceCardId;
        public FlipTriggerTiming TriggerTiming => trigger.Timing;
        public bool Changed { get; private set; }
        public CardLinkTrigger CreatedTrigger { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            CardFlippedEvent? flippedEvent;
            CardRestoredEvent? restoredEvent;
            try
            {
                Changed = flipEvaluator.TryApplyLinkedTransition(card, trigger.Timing, trigger.SourceCardId, out flippedEvent, out restoredEvent);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return UniTask.FromResult(ActionOutcome.Failure($"卡牌联动条件异常：{card.InstanceId}"));
            }
            if (flippedEvent.HasValue)
            {
                CardFlippedEvent evt = flippedEvent.Value;
                eventOutbox.Stage(evt);
                CreatedTrigger = CardLinkTrigger.Flipped(evt.CardInstanceId, evt.OwnerCharacterId);
            }
            if (restoredEvent.HasValue)
            {
                CardRestoredEvent evt = restoredEvent.Value;
                eventOutbox.Stage(evt);
                CreatedTrigger = CardLinkTrigger.Restored(evt.CardInstanceId, evt.OwnerCharacterId);
            }
            if (Changed)
                eventOutbox.PublishCheckpoint();
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }
}
