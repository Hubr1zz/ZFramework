using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;

namespace HuntingInDarkness.ActionFlow.Combat
{
    /// <summary>下一玩家轮开始时统一结算卡牌自动翻面/恢复，避免事件监听器直接修改权威状态。</summary>
    public sealed class BeginPlayerTurnAction : CompositeGameAction, ISourceAction, ITargetAction
    {
        private readonly IReadOnlyList<CharacterActionCardInstance> cards;
        private readonly FlipConditionEvaluator flipEvaluator;
        private readonly ActionEventOutbox eventOutbox;
        private readonly IReactorEntity combat;
        private readonly Func<int, IReactorEntity> resolveOwner;
        private readonly Action resetPlayerTurn;
        private ResolveCardTurnStartAction lastAction;
        private int cardIndex;
        private bool resetScheduled;
        private bool resetFailed;

        public BeginPlayerTurnAction(IReadOnlyList<CharacterActionCardInstance> cards, FlipConditionEvaluator flipEvaluator, ActionEventOutbox eventOutbox, IReactorEntity combat, Func<int, IReactorEntity> resolveOwner, Action resetPlayerTurn)
        {
            this.cards = cards ?? Array.Empty<CharacterActionCardInstance>();
            this.flipEvaluator = flipEvaluator ?? throw new ArgumentNullException(nameof(flipEvaluator));
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            this.combat = combat ?? throw new ArgumentNullException(nameof(combat));
            this.resolveOwner = resolveOwner ?? throw new ArgumentNullException(nameof(resolveOwner));
            this.resetPlayerTurn = resetPlayerTurn ?? throw new ArgumentNullException(nameof(resetPlayerTurn));
        }

        public int ChangedCardCount { get; private set; }
        public IReactorEntity Source => combat;
        public IReactorEntity Target => combat;
        public override ReactionPhases OpenReactionPhases => ReactionPhases.AfterResolved;

        protected override GameAction GetNextChild(CompositeExecutionContext context)
        {
            if (!resetScheduled)
            {
                resetScheduled = true;
                return new ResetPlayerTurnStateAction(resetPlayerTurn, combat);
            }
            if (context.CompletedCount == 1 && lastAction == null && !context.LastOutcome.IsSuccess)
            {
                resetFailed = true;
                return null;
            }
            if (context.CompletedCount > 0 && context.LastOutcome.IsSuccess && lastAction?.Changed == true)
                ChangedCardCount++;
            while (cardIndex < cards.Count)
            {
                CharacterActionCardInstance card = cards[cardIndex++];
                if (card == null) continue;
                IReactorEntity owner = resolveOwner(card.OwnerCharacterId);
                lastAction = new ResolveCardTurnStartAction(card, flipEvaluator, eventOutbox, combat, owner);
                return lastAction;
            }
            return null;
        }

        protected override ActionOutcome Resolve(CompositeExecutionContext context) => resetFailed ? context.LastOutcome : ActionOutcome.Success();
    }

    public sealed class ResetPlayerTurnStateAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly Action resetPlayerTurn;

        internal ResetPlayerTurnStateAction(Action resetPlayerTurn, IReactorEntity combat)
        {
            this.resetPlayerTurn = resetPlayerTurn;
            Source = combat;
            Target = combat;
        }

        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }
        public override ReactionPhases OpenReactionPhases => ReactionPhases.AfterResolved;

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            resetPlayerTurn();
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }

    public sealed class ResolveCardTurnStartAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly CharacterActionCardInstance card;
        private readonly FlipConditionEvaluator flipEvaluator;
        private readonly ActionEventOutbox eventOutbox;

        internal ResolveCardTurnStartAction(CharacterActionCardInstance card, FlipConditionEvaluator flipEvaluator, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.card = card;
            this.flipEvaluator = flipEvaluator;
            this.eventOutbox = eventOutbox;
            Source = source;
            Target = target;
        }

        public int CardInstanceId => card.InstanceId;
        public int OwnerCharacterId => card.OwnerCharacterId;
        public bool Changed { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            Changed = flipEvaluator.TryApplyTurnStartTransition(card, out CardFlippedEvent? flippedEvent, out CardRestoredEvent? restoredEvent);
            if (flippedEvent.HasValue)
                eventOutbox.Stage(flippedEvent.Value);
            if (restoredEvent.HasValue)
                eventOutbox.Stage(restoredEvent.Value);
            if (Changed)
                eventOutbox.PublishCheckpoint();
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }
}
