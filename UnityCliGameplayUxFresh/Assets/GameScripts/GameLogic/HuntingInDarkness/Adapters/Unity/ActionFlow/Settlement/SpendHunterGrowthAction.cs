using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.ActionFlow.Settlement
{
    public readonly struct HunterGrowthCommandResult
    {
        public bool Succeeded { get; }
        public string Reason { get; }
        public HunterGrowthChoice Choice { get; }
        public int PreviousValue { get; }
        public int CurrentValue { get; }
        public int RemainingGrowth { get; }
        public IReadOnlyList<HunterGrowthMilestoneOutcome> Milestones { get; }

        public HunterGrowthCommandResult(bool succeeded, string reason, HunterGrowthChoice choice, int previousValue, int currentValue, int remainingGrowth, IReadOnlyList<HunterGrowthMilestoneOutcome> milestones)
        {
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
            Choice = choice;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
            RemainingGrowth = remainingGrowth;
            Milestones = milestones ?? Array.Empty<HunterGrowthMilestoneOutcome>();
        }

        public static HunterGrowthCommandResult Failed(string reason) => new(false, reason, default, 0, 0, 0, Array.Empty<HunterGrowthMilestoneOutcome>());
    }

    /// <summary>分配一点猎人成长的唯一权威提交点；Reactor 可阻止或改写本次成长方向。</summary>
    public sealed class SpendHunterGrowthAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly SettlementInstance settlement;
        private readonly HunterInstance hunter;
        private readonly ActionEventOutbox eventOutbox;
        private readonly IReactorEntity settlementEntity;
        private readonly IReactorEntity hunterEntity;

        public SpendHunterGrowthAction(SettlementInstance settlement, HunterInstance hunter, HunterGrowthChoice choice, ActionEventOutbox eventOutbox, IReactorEntity settlementEntity, IReactorEntity hunterEntity)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.hunter = hunter ?? throw new ArgumentNullException(nameof(hunter));
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            this.settlementEntity = settlementEntity ?? throw new ArgumentNullException(nameof(settlementEntity));
            this.hunterEntity = hunterEntity ?? throw new ArgumentNullException(nameof(hunterEntity));
            Choice = choice;
        }

        public HunterGrowthChoice Choice { get; private set; }
        public HunterGrowthCommandResult Result { get; private set; }
        public IReactorEntity Source => settlementEntity;
        public IReactorEntity Target => hunterEntity;

        public void SetChoice(HunterGrowthChoice choice) => Choice = choice;

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(settlement.GetHunter(hunter.InstanceId), hunter)) return Fail("猎人不属于当前营地。");
            if (!HunterAdvancementRules.CanSpendGrowth(hunter, Choice, out string reason)) return Fail(reason);

            int previousValue = Choice == HunterGrowthChoice.Courage ? hunter.Courage : hunter.Understanding;
            if (!HunterAdvancementRules.TrySpendGrowth(hunter, Choice)) return Fail("成长状态已经发生变化。");
            List<HunterGrowthMilestoneOutcome> milestones = PlayableGrowthMilestoneRuntime.ClaimHunterMilestones(hunter);
            int currentValue = Choice == HunterGrowthChoice.Courage ? hunter.Courage : hunter.Understanding;
            Result = new HunterGrowthCommandResult(true, string.Empty, Choice, previousValue, currentValue, hunter.UnspentGrowth, milestones);
            eventOutbox.Stage(new HunterGrowthSpentEvent { HunterId = hunter.InstanceId, Choice = Choice });
            foreach (HunterGrowthMilestoneOutcome milestone in milestones)
                eventOutbox.Stage(new HunterGrowthMilestoneReachedEvent(hunter.InstanceId, hunter.Name, milestone));
            eventOutbox.Stage(new SettlementTransactionCommittedEvent { TransactionId = $"hunter-growth:{hunter.InstanceId}:{Choice}", Kind = SettlementTransactionKind.HunterGrowth });
            return UniTask.FromResult(ActionOutcome.Success());
        }

        private UniTask<ActionOutcome> Fail(string reason)
        {
            Result = HunterGrowthCommandResult.Failed(reason);
            return UniTask.FromResult(ActionOutcome.Failure(reason));
        }
    }
}
