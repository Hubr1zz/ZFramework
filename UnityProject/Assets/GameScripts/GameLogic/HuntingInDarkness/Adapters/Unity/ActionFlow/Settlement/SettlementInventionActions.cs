using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.ActionFlow.Settlement
{
    public readonly struct SettlementInventionCommandResult
    {
        public SettlementInventionCommandResult(bool succeeded, string reason, string inventionName)
        {
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
            InventionName = inventionName ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public string InventionName { get; }

        public static SettlementInventionCommandResult Failed(string reason) => new(false, reason, string.Empty);
    }

    public struct SettlementInventionUnlockedEvent
    {
        public string InventionName;
    }

    public sealed class UnlockSettlementInventionAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly SettlementInstance settlement;
        private readonly InventionSystem inventionSystem;
        private readonly InventionData invention;
        private readonly ActionEventOutbox eventOutbox;

        public UnlockSettlementInventionAction(SettlementInstance settlement, InventionSystem inventionSystem, InventionData invention, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
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
            if (!inventionSystem.TryUnlock(invention)) return Fail("发明提交失败，请重试。");

            foreach (KeyValuePair<string, int> previous in previousResources)
                eventOutbox.Stage(new ResourceChangedEvent { ResourceName = previous.Key, OldAmount = previous.Value, NewAmount = settlement.GetResource(previous.Key) });
            Result = new SettlementInventionCommandResult(true, string.Empty, invention.inventionName);
            eventOutbox.Stage(new SettlementInventionUnlockedEvent { InventionName = invention.inventionName });
            eventOutbox.Stage(new SettlementTransactionCommittedEvent { TransactionId = $"invention:{invention.inventionName}", Kind = SettlementTransactionKind.Invention });
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
}
