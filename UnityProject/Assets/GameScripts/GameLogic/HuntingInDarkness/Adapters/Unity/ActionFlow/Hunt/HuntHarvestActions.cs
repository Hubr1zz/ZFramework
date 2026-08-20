using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunt;
using HuntingInDarkness.Hunt;
using UnityEngine;

namespace HuntingInDarkness.ActionFlow.Hunt
{
    public struct HarvestPreparedEvent
    {
        public int HunterId;
        public string ResourceName;
        public int CardCount;
    }

    public struct HarvestCardRevealedEvent
    {
        public int HunterId;
        public string ResourceName;
        public int CardIndex;
        public bool IsHit;
        public int RevealedCount;
        public int CardCount;
    }

    public struct HarvestCommittedEvent
    {
        public int HunterId;
        public string ResourceName;
        public int ObtainedCount;
    }

    /// <summary>锁定资源点并生成不可变采集牌序；规则可在执行前覆盖牌数和命中率。</summary>
    public sealed class BeginHarvestAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly HuntManager manager;
        private readonly ResourcePointInstance point;
        private readonly HunterInstance hunter;
        private readonly ActionEventOutbox eventOutbox;

        public BeginHarvestAction(HuntManager manager, ResourcePointInstance point, HunterInstance hunter, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.point = point;
            this.hunter = hunter;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            DrawCount = Mathf.Clamp(point?.DrawCount ?? 0, 0, HarvestDrawPlan.MaximumCardCount);
        }

        public float HitChance { get; private set; } = 0.6f;
        public int DrawCount { get; private set; }
        public ItemData Resource => point?.Resource;
        public PlayableHarvestTransaction Transaction { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        public void SetHitChance(float value) => HitChance = Mathf.Clamp01(value);
        public void SetDrawCount(int value) => DrawCount = Mathf.Clamp(value, 0, HarvestDrawPlan.MaximumCardCount);

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            if (!manager.IsHarvestablePoint(point)) return UniTask.FromResult(ActionOutcome.Failure("资源点不可采集"));
            if (hunter == null) return UniTask.FromResult(ActionOutcome.Failure("没有执行采集的猎人"));
            Transaction = manager.Resources.PrepareHarvest(point, hunter, HitChance, DrawCount);
            if (Transaction == null) return UniTask.FromResult(ActionOutcome.Failure("资源点已被占用或状态已经改变"));
            eventOutbox.Stage(new HarvestPreparedEvent
            {
                HunterId = hunter.InstanceId,
                ResourceName = Transaction.ResourceName,
                CardCount = Transaction.CardCount
            });
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }

    /// <summary>推进一次玩家可见的采集步骤；最后一张揭示后尝试提交资源。</summary>
    public sealed class AdvanceHarvestAction : CompositeGameAction, ISourceAction, ITargetAction
    {
        private readonly HuntManager manager;
        private readonly PlayableHarvestTransaction transaction;
        private readonly ActionEventOutbox eventOutbox;
        private RevealHarvestCardAction revealAction;
        private CommitHarvestAction commitAction;
        private GameAction lastAction;

        public AdvanceHarvestAction(HuntManager manager, PlayableHarvestTransaction transaction, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.transaction = transaction;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public PlayableHarvestStepResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override GameAction GetNextChild(CompositeExecutionContext context)
        {
            if (context.CompletedCount > 0 && !context.LastOutcome.IsSuccess) return null;
            if (transaction == null || transaction.IsCommitted) return null;
            if (transaction.CanReveal && revealAction == null)
            {
                revealAction = new RevealHarvestCardAction(transaction, eventOutbox, Source, Target);
                lastAction = revealAction;
                return lastAction;
            }
            if (!transaction.CanReveal && transaction.IsComplete && commitAction == null)
            {
                commitAction = new CommitHarvestAction(manager, transaction, eventOutbox, Source, Target);
                lastAction = commitAction;
                return lastAction;
            }
            return null;
        }

        protected override ActionOutcome Resolve(CompositeExecutionContext context)
        {
            if (context.CompletedCount == 0)
            {
                Result = PlayableHarvestStepResult.Failed("采集事务不可推进");
                return ActionOutcome.Failure(Result.Reason);
            }
            if (!context.LastOutcome.IsSuccess)
            {
                Result = PlayableHarvestStepResult.Failed(context.LastOutcome.Reason, revealAction?.RevealedCard);
                return context.LastOutcome;
            }
            HarvestCardResult? card = revealAction?.RevealedCard;
            if (commitAction != null && transaction.IsCommitted)
            {
                Result = PlayableHarvestStepResult.Completed(card, commitAction.Obtained);
                return ActionOutcome.Success();
            }
            if (card.HasValue)
            {
                Result = PlayableHarvestStepResult.Revealed(card.Value);
                return ActionOutcome.Success();
            }
            string reason = lastAction == null ? "采集事务不可推进" : $"{lastAction.DebugName} 未完成";
            Result = PlayableHarvestStepResult.Failed(reason);
            return ActionOutcome.Failure(reason);
        }
    }

    public sealed class RevealHarvestCardAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly PlayableHarvestTransaction transaction;
        private readonly ActionEventOutbox eventOutbox;

        internal RevealHarvestCardAction(PlayableHarvestTransaction transaction, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.transaction = transaction;
            this.eventOutbox = eventOutbox;
            Source = source;
            Target = target;
        }

        public HarvestCardResult? RevealedCard { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            if (transaction == null || !transaction.CanReveal) return UniTask.FromResult(ActionOutcome.Failure("没有可揭示的采集牌"));
            HarvestCardResult card = transaction.RevealNext();
            RevealedCard = card;
            eventOutbox.Stage(new HarvestCardRevealedEvent
            {
                HunterId = transaction.HunterId,
                ResourceName = transaction.ResourceName,
                CardIndex = card.CardIndex,
                IsHit = card.IsHit,
                RevealedCount = transaction.RevealedCount,
                CardCount = transaction.CardCount
            });
            eventOutbox.PublishCheckpoint();
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }

    public sealed class CommitHarvestAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly HuntManager manager;
        private readonly PlayableHarvestTransaction transaction;
        private readonly ActionEventOutbox eventOutbox;

        internal CommitHarvestAction(HuntManager manager, PlayableHarvestTransaction transaction, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.manager = manager;
            this.transaction = transaction;
            this.eventOutbox = eventOutbox;
            Source = source;
            Target = target;
        }

        public IReadOnlyList<ItemInstance> Obtained { get; private set; } = Array.Empty<ItemInstance>();
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            if (transaction == null || !transaction.IsComplete || transaction.IsCommitted) return UniTask.FromResult(ActionOutcome.Failure("采集事务尚未达到提交条件"));
            try
            {
                Obtained = manager.Resources.CommitHarvest(transaction);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return UniTask.FromResult(ActionOutcome.Failure("采集提交失败"));
            }
            manager.NotifyResourcePointHarvested(transaction.Point);
            eventOutbox.Stage(new HarvestCommittedEvent
            {
                HunterId = transaction.HunterId,
                ResourceName = transaction.ResourceName,
                ObtainedCount = Obtained.Count
            });
            eventOutbox.PublishCheckpoint();
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }
}
