using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;

namespace HuntingInDarkness.ActionFlow.Settlement
{
    public readonly struct WeaponTrainingCommandResult
    {
        public bool Success { get; }
        public string Reason { get; }
        public WeaponMasteryGainOutcome MasteryOutcome { get; }

        public WeaponTrainingCommandResult(bool success, string reason, WeaponMasteryGainOutcome masteryOutcome)
        {
            Success = success;
            Reason = reason ?? string.Empty;
            MasteryOutcome = masteryOutcome;
        }

        public static WeaponTrainingCommandResult Failed(string reason) => new(false, reason, default);
    }

    /// <summary>
    /// 一次武器训练的唯一权威提交点。Before Reactor 可调整本次费用或经验，
    /// 但最终资格与库存会在真正执行时重新验证。
    /// </summary>
    public sealed class TrainWeaponAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly SettlementInstance settlement;
        private readonly HunterInstance hunter;
        private readonly WeaponMasteryFamilyDefinition family;
        private readonly string requiredInventionId;
        private readonly string costResourceId;
        private readonly ActionEventOutbox eventOutbox;
        private readonly IReactorEntity settlementEntity;
        private readonly IReactorEntity hunterEntity;

        public TrainWeaponAction(SettlementInstance settlement, HunterInstance hunter, WeaponMasteryFamilyDefinition family, string requiredInventionId, string costResourceId, int resourceCost, int experience, ActionEventOutbox eventOutbox, IReactorEntity settlementEntity, IReactorEntity hunterEntity)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.hunter = hunter ?? throw new ArgumentNullException(nameof(hunter));
            this.family = family ?? throw new ArgumentNullException(nameof(family));
            this.requiredInventionId = requiredInventionId ?? string.Empty;
            this.costResourceId = costResourceId ?? string.Empty;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            this.settlementEntity = settlementEntity ?? throw new ArgumentNullException(nameof(settlementEntity));
            this.hunterEntity = hunterEntity ?? throw new ArgumentNullException(nameof(hunterEntity));
            SetResourceCost(resourceCost);
            SetExperience(experience);
        }

        public int ResourceCost { get; private set; }
        public int Experience { get; private set; }
        public WeaponTrainingCommandResult Result { get; private set; }
        public IReactorEntity Source => settlementEntity;
        public IReactorEntity Target => hunterEntity;

        public void SetResourceCost(int value) => ResourceCost = Math.Max(0, value);
        public void SetExperience(int value) => Experience = Math.Max(1, value);

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(settlement.GetHunter(hunter.InstanceId), hunter)) return Fail("猎人不属于当前营地");
            if (!WeaponMasteryRules.CanIncrease(hunter, family.Id)) return Fail("熟练度已达到上限");
            if (!WeaponTrainingRules.CanTrain(hunter.IsAvailable && !hunter.IsDead, settlement.IsInventionUnlocked(requiredInventionId), settlement.GetResource(costResourceId), ResourceCost, family.Id, Experience, out string reason)) return Fail(reason);

            int oldResourceAmount = settlement.GetResource(costResourceId);
            if (!settlement.SpendResource(costResourceId, ResourceCost)) return Fail("训练资源不足");
            if (!WeaponMasteryRules.TryGain(hunter, family, Experience, out WeaponMasteryGainOutcome masteryOutcome))
            {
                settlement.AddResource(costResourceId, ResourceCost);
                return Fail("熟练度已达到上限");
            }

            Result = new WeaponTrainingCommandResult(true, string.Empty, masteryOutcome);
            eventOutbox.Stage(new ResourceChangedEvent
            {
                ResourceName = costResourceId,
                OldAmount = oldResourceAmount,
                NewAmount = settlement.GetResource(costResourceId)
            });
            eventOutbox.Stage(new WeaponMasteryChangedEvent
            {
                HunterId = hunter.InstanceId,
                HunterName = hunter.Name,
                WeaponName = family.DisplayName,
                MasteryId = masteryOutcome.MasteryId,
                MasteryName = masteryOutcome.MasteryName,
                OldValue = masteryOutcome.OldValue,
                NewValue = masteryOutcome.NewValue,
                ReachedMilestoneNames = new List<string>(masteryOutcome.ReachedMilestoneNames).ToArray(),
                Source = WeaponMasteryGainSource.Training
            });
            eventOutbox.Stage(new SettlementTransactionCommittedEvent
            {
                TransactionId = $"weapon-training:{hunter.InstanceId}:{family.Id}",
                Kind = SettlementTransactionKind.WeaponTraining
            });
            return UniTask.FromResult(ActionOutcome.Success());
        }

        private UniTask<ActionOutcome> Fail(string reason)
        {
            Result = WeaponTrainingCommandResult.Failed(reason);
            return UniTask.FromResult(ActionOutcome.Failure(reason));
        }
    }
}
