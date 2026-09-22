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

    /// <summary>发明解锁 Root：先解析持续来源，再原子提交解锁，最后展开一次性效果。</summary>
    public sealed class UnlockSettlementInventionAction : CompositeGameAction, ISourceAction, ITargetAction
    {
        private readonly SettlementInstance settlement;
        private readonly InventionSystem inventionSystem;
        private readonly InventionData invention;
        private readonly ActionEventOutbox eventOutbox;
        private readonly Func<HunterInstance, IReactorEntity> resolveHunterEntity;
        private readonly Queue<GameAction> pendingEffects = new();
        private readonly List<PrepareSettlementInventionModifierAction> modifierActions = new();
        private CommitSettlementInventionAction commitAction;
        private SettlementModifierRegistrationPlan modifierPlan;
        private int modifierActionIndex;
        private string preparationFailureReason;
        private bool modifiersPrepared;
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
                if (!modifiersPrepared)
                {
                    PrepareModifierActions();
                    modifiersPrepared = true;
                }
                if (modifierActionIndex > 0 && !modifierActions[modifierActionIndex - 1].Prepared)
                {
                    preparationFailureReason = string.IsNullOrWhiteSpace(context.LastOutcome.Reason) ? "战役持续效果已被阻止。" : context.LastOutcome.Reason;
                    return null;
                }
                if (modifierActionIndex < modifierActions.Count)
                    return modifierActions[modifierActionIndex++];

                var additions = new List<SettlementModifierState>();
                foreach (PrepareSettlementInventionModifierAction action in modifierActions)
                    additions.Add(action.Modifier);
                if (!PlayableSettlementModifierRuntime.TryCreateRegistrationPlan(settlement, additions, out modifierPlan, out preparationFailureReason)) return null;
                commitAction = new CommitSettlementInventionAction(settlement, inventionSystem, invention, modifierPlan, eventOutbox, Source, Target);
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
                string reason = preparationFailureReason;
                if (string.IsNullOrWhiteSpace(reason)) reason = commitAction?.Result.Reason;
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
                if (effect == null || effect.lifetime != InventionEffectLifetime.Unlock) continue;
                foreach (HunterInstance hunter in hunters)
                {
                    if (!InventionEffectRules.IsEligible(hunter, effect.target)) continue;
                    pendingEffects.Enqueue(new ApplySettlementInventionEffectAction(invention.ContentId, hunter, effect.kind, effect.target, effect.value, eventOutbox, Target, resolveHunterEntity(hunter)));
                }
            }
        }

        private void PrepareModifierActions()
        {
            if (invention?.unlockEffects == null) return;
            foreach (InventionPassiveEffect effect in invention.unlockEffects)
                if (effect != null && effect.lifetime == InventionEffectLifetime.Campaign)
                    modifierActions.Add(new PrepareSettlementInventionModifierAction(invention.ContentId, effect, Source, Target));
        }
    }

    public sealed class PrepareSettlementInventionModifierAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly string inventionId;
        private readonly InventionPassiveEffect effect;

        public PrepareSettlementInventionModifierAction(string inventionId, InventionPassiveEffect effect, IReactorEntity source, IReactorEntity target)
        {
            this.inventionId = inventionId ?? string.Empty;
            this.effect = effect ?? throw new ArgumentNullException(nameof(effect));
            Value = effect.value;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public int Value { get; private set; }
        public bool Prepared { get; private set; }
        public SettlementModifierState Modifier { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        public void SetValue(int value) => Value = value;

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Value == 0 || string.IsNullOrWhiteSpace(effect.modifierId) || effect.kind == InventionEffectKind.None || effect.target != InventionEffectTarget.AllLivingAndFutureHunters)
                return UniTask.FromResult(ActionOutcome.Failure("战役持续效果配置无效。"));
            Modifier = new SettlementModifierState
            {
                ModifierId = effect.modifierId.Trim(),
                SourceKind = SettlementModifierSourceKind.Invention,
                SourceId = inventionId,
                Kind = effect.kind,
                Target = effect.target,
                ConfiguredValue = effect.value,
                Value = Value,
                HasValueOverride = Value != effect.value
            };
            Prepared = true;
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }

    public sealed class CommitSettlementInventionAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly SettlementInstance settlement;
        private readonly InventionSystem inventionSystem;
        private readonly InventionData invention;
        private readonly SettlementModifierRegistrationPlan modifierPlan;
        private readonly ActionEventOutbox eventOutbox;

        internal CommitSettlementInventionAction(SettlementInstance settlement, InventionSystem inventionSystem, InventionData invention, SettlementModifierRegistrationPlan modifierPlan, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.inventionSystem = inventionSystem ?? throw new ArgumentNullException(nameof(inventionSystem));
            this.invention = invention;
            this.modifierPlan = modifierPlan ?? throw new ArgumentNullException(nameof(modifierPlan));
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
            List<PlayableSettlementModifierChange> modifierChanges = PlayableSettlementModifierRuntime.ApplyRegistrationPlan(settlement, modifierPlan);

            foreach (KeyValuePair<string, int> previous in previousResources)
                eventOutbox.Stage(new ResourceChangedEvent { ResourceName = previous.Key, OldAmount = previous.Value, NewAmount = settlement.GetResource(previous.Key) });
            Result = new SettlementInventionCommandResult(true, string.Empty, invention.ContentId, invention.inventionName);
            eventOutbox.Stage(new SettlementInventionUnlockedEvent { InventionId = invention.ContentId, DisplayName = invention.inventionName });
            foreach (PlayableSettlementModifierChange change in modifierChanges)
                eventOutbox.Stage(new SettlementInventionEffectAppliedEvent { InventionId = invention.ContentId, HunterId = change.HunterId, Kind = change.Projection.Kind, PreviousValue = change.Projection.PreviousValue, CurrentValue = change.Projection.CurrentValue });
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
