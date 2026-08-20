using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.ActionFlow.Settlement
{
    public readonly struct RecruitHunterCommandResult
    {
        public RecruitHunterCommandResult(bool succeeded, string reason, HunterInstance hunter)
        {
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
            Hunter = hunter;
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public HunterInstance Hunter { get; }

        public static RecruitHunterCommandResult Failed(string reason) => new(false, reason, null);
    }

    public readonly struct RecoverHunterCommandResult
    {
        public RecoverHunterCommandResult(bool succeeded, string reason, HunterRecoveryResult recovery)
        {
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
            Recovery = recovery;
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public HunterRecoveryResult Recovery { get; }

        public static RecoverHunterCommandResult Failed(string reason) => new(false, reason, default);
    }

    public struct HunterRecruitedEvent
    {
        public int HunterId;
        public string HunterName;
        public string TemplateId;
    }

    public struct HunterRecoveredEvent
    {
        public int HunterId;
        public HunterBodyPart BodyPart;
        public int PreviousHealth;
        public int CurrentHealth;
        public int MaximumHealth;
    }

    /// <summary>一次招募的权威提交点；Before Reactor 可覆盖费用与营地容量。</summary>
    public sealed class RecruitHunterAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly SettlementInstance settlement;
        private readonly HunterData template;
        private readonly string requestedName;
        private readonly IReadOnlyList<HunterData> allowedTemplates;
        private readonly string costResourceId;
        private readonly ActionEventOutbox eventOutbox;

        public RecruitHunterAction(SettlementInstance settlement, HunterData template, string requestedName, IReadOnlyList<HunterData> allowedTemplates, string costResourceId, int resourceCost, int maximumLivingHunters, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.template = template;
            this.requestedName = requestedName;
            this.allowedTemplates = allowedTemplates ?? Array.Empty<HunterData>();
            this.costResourceId = costResourceId ?? string.Empty;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            SetResourceCost(resourceCost);
            SetMaximumLivingHunters(maximumLivingHunters);
        }

        public int ResourceCost { get; private set; }
        public int MaximumLivingHunters { get; private set; }
        public RecruitHunterCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        public void SetResourceCost(int value) => ResourceCost = Math.Max(0, value);
        public void SetMaximumLivingHunters(int value) => MaximumLivingHunters = Math.Max(1, value);

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (template == null || !ContainsTemplate(template)) return Fail("请选择一名愿意靠近营火的陌生人。");

            int livingCount = settlement.GetAliveHunters().Count;
            if (ResourceCost > 0 && string.IsNullOrWhiteSpace(costResourceId)) return Fail("招募成本尚未配置。");
            int availableResource = string.IsNullOrWhiteSpace(costResourceId) ? 0 : settlement.GetResource(costResourceId);
            if (!RecruitmentRules.CanRecruit(settlement.CurrentYear, settlement.LastRecruitmentYear, livingCount, MaximumLivingHunters, availableResource, ResourceCost, out string reason)) return Fail(reason);

            var existingNames = new List<string>();
            foreach (HunterInstance existingHunter in settlement.Hunters)
                if (existingHunter != null)
                    existingNames.Add(existingHunter.Name);
            if (!RecruitmentRules.TryNormalizeName(requestedName, existingNames, out string normalizedName, out reason)) return Fail(reason);

            var hunter = new HunterInstance(template, HunterIdentityRules.NextAvailableId(settlement.Hunters)) { Name = normalizedName };
            PlayableSymptomRuntime.SynchronizeHunter(hunter);
            var annal = new AnnalEntry
            {
                Year = settlement.CurrentYear,
                EventId = $"recruit:{hunter.InstanceId}",
                EventName = $"{hunter.Name} 加入营地",
                IsCompleted = true,
                EntryType = TimelineEntryType.PlayerAdded
            };

            cancellationToken.ThrowIfCancellationRequested();
            int oldResourceAmount = availableResource;
            if (ResourceCost > 0 && !settlement.SpendResource(costResourceId, ResourceCost)) return Fail("招募所需物资已经发生变化。");
            settlement.Hunters.Add(hunter);
            settlement.LastRecruitmentYear = settlement.CurrentYear;
            settlement.Timeline ??= new List<AnnalEntry>();
            settlement.Timeline.Add(annal);

            Result = new RecruitHunterCommandResult(true, string.Empty, hunter);
            if (ResourceCost > 0)
                eventOutbox.Stage(new ResourceChangedEvent { ResourceName = costResourceId, OldAmount = oldResourceAmount, NewAmount = settlement.GetResource(costResourceId) });
            eventOutbox.Stage(new HunterRecruitedEvent { HunterId = hunter.InstanceId, HunterName = hunter.Name, TemplateId = template.name });
            eventOutbox.Stage(new HunterRosterChangedEvent());
            eventOutbox.Stage(new SettlementTransactionCommittedEvent { TransactionId = $"recruit:{hunter.InstanceId}", Kind = SettlementTransactionKind.Recruitment });
            return UniTask.FromResult(ActionOutcome.Success());
        }

        private bool ContainsTemplate(HunterData candidate)
        {
            foreach (HunterData allowedTemplate in allowedTemplates)
                if (ReferenceEquals(allowedTemplate, candidate))
                    return true;
            return false;
        }

        private UniTask<ActionOutcome> Fail(string reason)
        {
            Result = RecruitHunterCommandResult.Failed(reason);
            return UniTask.FromResult(ActionOutcome.Failure(reason));
        }
    }

    /// <summary>一次分部位休养的权威提交点；Before Reactor 可覆盖费用与恢复量。</summary>
    public sealed class RecoverHunterAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly SettlementInstance settlement;
        private readonly HunterInstance hunter;
        private readonly HunterBodyPart bodyPart;
        private readonly string costResourceId;
        private readonly ActionEventOutbox eventOutbox;

        public RecoverHunterAction(SettlementInstance settlement, HunterInstance hunter, HunterBodyPart bodyPart, string costResourceId, int resourceCost, int recoveryAmount, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.hunter = hunter ?? throw new ArgumentNullException(nameof(hunter));
            this.bodyPart = bodyPart;
            this.costResourceId = costResourceId ?? string.Empty;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            SetResourceCost(resourceCost);
            SetRecoveryAmount(recoveryAmount);
        }

        public int ResourceCost { get; private set; }
        public int RecoveryAmount { get; private set; }
        public RecoverHunterCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        public void SetResourceCost(int value) => ResourceCost = Math.Max(0, value);
        public void SetRecoveryAmount(int value) => RecoveryAmount = Math.Max(1, value);

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(settlement.GetHunter(hunter.InstanceId), hunter)) return Fail("猎人不属于当前营地。");
            if (!HunterRecoveryRules.CanRecover(hunter, bodyPart, out string reason)) return Fail(reason);
            if (ResourceCost > 0 && string.IsNullOrWhiteSpace(costResourceId)) return Fail("休养成本尚未配置。");

            int oldResourceAmount = string.IsNullOrWhiteSpace(costResourceId) ? 0 : settlement.GetResource(costResourceId);
            if (oldResourceAmount < ResourceCost) return Fail($"缺少 {costResourceId}。");
            cancellationToken.ThrowIfCancellationRequested();
            if (ResourceCost > 0 && !settlement.SpendResource(costResourceId, ResourceCost)) return Fail("休养所需物资已经发生变化。");
            if (!HunterRecoveryRules.TryRecover(hunter, bodyPart, RecoveryAmount, out HunterRecoveryResult recovery, out reason))
            {
                if (ResourceCost > 0)
                    settlement.AddResource(costResourceId, ResourceCost);
                return Fail(reason);
            }

            Result = new RecoverHunterCommandResult(true, string.Empty, recovery);
            if (ResourceCost > 0)
                eventOutbox.Stage(new ResourceChangedEvent { ResourceName = costResourceId, OldAmount = oldResourceAmount, NewAmount = settlement.GetResource(costResourceId) });
            eventOutbox.Stage(new HunterRecoveredEvent
            {
                HunterId = hunter.InstanceId,
                BodyPart = recovery.BodyPart,
                PreviousHealth = recovery.PreviousHealth,
                CurrentHealth = recovery.CurrentHealth,
                MaximumHealth = recovery.MaximumHealth
            });
            eventOutbox.Stage(new SettlementTransactionCommittedEvent { TransactionId = $"recovery:{hunter.InstanceId}:{bodyPart}", Kind = SettlementTransactionKind.Recovery });
            return UniTask.FromResult(ActionOutcome.Success());
        }

        private UniTask<ActionOutcome> Fail(string reason)
        {
            Result = RecoverHunterCommandResult.Failed(reason);
            return UniTask.FromResult(ActionOutcome.Failure(reason));
        }
    }
}
