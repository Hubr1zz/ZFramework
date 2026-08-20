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
    public readonly struct SettlementInventionCommandResult
    {
        public SettlementInventionCommandResult(bool succeeded, string reason, string inventionId, string displayName)
        {
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
            InventionId = inventionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public string InventionId { get; }
        public string DisplayName { get; }

        public static SettlementInventionCommandResult Failed(string reason) => new(false, reason, string.Empty, string.Empty);
    }

    public struct SettlementInventionUnlockedEvent
    {
        public string InventionId;
        public string DisplayName;
    }

    public struct SettlementInventionEffectAppliedEvent
    {
        public string InventionId;
        public int HunterId;
        public InventionEffectKind Kind;
        public int PreviousValue;
        public int CurrentValue;
    }

    /// <summary>发明解锁 Root：先提交解锁，再把每个猎人的结构化效果展开成独立 Reactor 边界。</summary>
    public sealed class UnlockSettlementInventionAction : CompositeGameAction, ISourceAction, ITargetAction
    {
        private readonly SettlementInstance settlement;
        private readonly InventionSystem inventionSystem;
        private readonly InventionData invention;
        private readonly ActionEventOutbox eventOutbox;
        private readonly Func<HunterInstance, IReactorEntity> resolveHunterEntity;
        private readonly Queue<GameAction> pendingEffects = new();
        private CommitSettlementInventionAction commitAction;
        private bool effectsPrepared;

        public UnlockSettlementInventionAction(SettlementInstance settlement, InventionSystem inventionSystem, InventionData invention, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target, Func<HunterInstance, IReactorEntity> resolveHunterEntity = null)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.inventionSystem = inventionSystem ?? throw new ArgumentNullException(nameof(inventionSystem));
            this.invention = invention;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            this.resolveHunterEntity = resolveHunterEntity ?? (_ => Target);
        }

        public SettlementInventionCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override GameAction GetNextChild(CompositeExecutionContext context)
        {
            if (commitAction == null)
            {
                commitAction = new CommitSettlementInventionAction(settlement, inventionSystem, invention, eventOutbox, Source, Target);
                return commitAction;
            }
            if (!commitAction.Result.Succeeded) return null;
            if (!effectsPrepared)
            {
                PrepareEffects();
                effectsPrepared = true;
            }
            return pendingEffects.Count > 0 ? pendingEffects.Dequeue() : null;
        }

        protected override ActionOutcome Resolve(CompositeExecutionContext context)
        {
            if (commitAction == null || !commitAction.Result.Succeeded)
            {
                string reason = commitAction?.Result.Reason;
                if (string.IsNullOrWhiteSpace(reason)) reason = context.LastOutcome.Reason;
                Result = SettlementInventionCommandResult.Failed(reason);
                return ActionOutcome.Failure(Result.Reason);
            }
            Result = commitAction.Result;
            eventOutbox.Stage(new SettlementTransactionCommittedEvent { TransactionId = $"invention:{invention.ContentId}", Kind = SettlementTransactionKind.Invention });
            return ActionOutcome.Success();
        }

        private void PrepareEffects()
        {
            if (invention.unlockEffects == null) return;
            var hunters = new List<HunterInstance>(settlement.Hunters);
            hunters.RemoveAll(hunter => hunter == null);
            hunters.Sort((left, right) => left.InstanceId.CompareTo(right.InstanceId));
            foreach (InventionPassiveEffect effect in invention.unlockEffects)
            {
                if (effect == null) continue;
                foreach (HunterInstance hunter in hunters)
                {
                    if (!InventionEffectRules.IsEligible(hunter, effect.target)) continue;
                    pendingEffects.Enqueue(new ApplySettlementInventionEffectAction(invention.ContentId, hunter, effect.kind, effect.target, effect.value, eventOutbox, Target, resolveHunterEntity(hunter)));
                }
            }
        }
    }

    public sealed class CommitSettlementInventionAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly SettlementInstance settlement;
        private readonly InventionSystem inventionSystem;
        private readonly InventionData invention;
        private readonly ActionEventOutbox eventOutbox;

        public CommitSettlementInventionAction(SettlementInstance settlement, InventionSystem inventionSystem, InventionData invention, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.inventionSystem = inventionSystem ?? throw new ArgumentNullException(nameof(inventionSystem));
            this.invention = invention;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public SettlementInventionCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (invention == null || !inventionSystem.AllInventions.Contains(invention)) return Fail("发明不属于当前营地。");
            if (!inventionSystem.CanUnlock(invention, out string reason)) return Fail(reason);

            Dictionary<string, int> previousResources = CaptureCostAmounts();
            foreach (InventionCost cost in invention.costs)
            {
                if (cost?.resource == null || cost.count <= 0) continue;
                string resourceId = cost.resource.ContentId;
                if (settlement.GetResource(resourceId) < GetTotalCost(resourceId)) return Fail($"缺少 {resourceId}。");
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!inventionSystem.TryCommitUnlock(invention)) return Fail("发明提交失败，请重试。");

            foreach (KeyValuePair<string, int> previous in previousResources)
                eventOutbox.Stage(new ResourceChangedEvent { ResourceName = previous.Key, OldAmount = previous.Value, NewAmount = settlement.GetResource(previous.Key) });
            Result = new SettlementInventionCommandResult(true, string.Empty, invention.ContentId, invention.inventionName);
            eventOutbox.Stage(new SettlementInventionUnlockedEvent { InventionId = invention.ContentId, DisplayName = invention.inventionName });
            return UniTask.FromResult(ActionOutcome.Success());
        }

        private Dictionary<string, int> CaptureCostAmounts()
        {
            var amounts = new Dictionary<string, int>();
            foreach (InventionCost cost in invention.costs)
                if (cost?.resource != null && cost.count > 0 && !amounts.ContainsKey(cost.resource.ContentId))
                    amounts.Add(cost.resource.ContentId, settlement.GetResource(cost.resource));
            return amounts;
        }

        private int GetTotalCost(string resourceId)
        {
            int total = 0;
            foreach (InventionCost cost in invention.costs)
                if (cost?.resource != null && cost.resource.ContentId == resourceId && cost.count > 0)
                    total += cost.count;
            return total;
        }

        private UniTask<ActionOutcome> Fail(string reason)
        {
            Result = SettlementInventionCommandResult.Failed(reason);
            return UniTask.FromResult(ActionOutcome.Failure(reason));
        }
    }

    public sealed class ApplySettlementInventionEffectAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly string inventionId;
        private readonly ActionEventOutbox eventOutbox;

        public ApplySettlementInventionEffectAction(string inventionId, HunterInstance hunter, InventionEffectKind kind, InventionEffectTarget effectTarget, int value, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.inventionId = inventionId ?? string.Empty;
            Hunter = hunter ?? throw new ArgumentNullException(nameof(hunter));
            Kind = kind;
            EffectTarget = effectTarget;
            Value = value;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public HunterInstance Hunter { get; }
        public InventionEffectKind Kind { get; }
        public InventionEffectTarget EffectTarget { get; }
        public int Value { get; private set; }
        public bool Applied { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        public void SetValue(int value) => Value = value;

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!InventionEffectRules.IsEligible(Hunter, EffectTarget)) return UniTask.FromResult(ActionOutcome.Success());
            if (!InventionEffectRules.TryApply(Hunter, Kind, Value, out int previousValue, out int currentValue)) return UniTask.FromResult(ActionOutcome.Failure("发明效果配置无效。"));
            Applied = true;
            eventOutbox.Stage(new SettlementInventionEffectAppliedEvent { InventionId = inventionId, HunterId = Hunter.InstanceId, Kind = Kind, PreviousValue = previousValue, CurrentValue = currentValue });
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }
}
