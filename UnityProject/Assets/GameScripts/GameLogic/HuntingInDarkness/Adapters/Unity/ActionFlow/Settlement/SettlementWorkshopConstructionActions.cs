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
    public readonly struct SettlementWorkshopConstructionResult
    {
        public SettlementWorkshopConstructionResult(bool succeeded, string reason, string workshopId)
        {
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
            WorkshopId = workshopId ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public string WorkshopId { get; }
        public static SettlementWorkshopConstructionResult Failed(string reason) => new(false, reason, string.Empty);
    }

    public struct SettlementWorkshopBuiltEvent
    {
        public string WorkshopId;
    }

    public sealed class BuildSettlementWorkshopAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly SettlementInstance settlement;
        private readonly PlayableWorkshopConstructionService service;
        private readonly IReadOnlyList<PlayableWorkshopDefinition> definitions;
        private readonly PlayableWorkshopDefinition definition;
        private readonly ActionEventOutbox eventOutbox;

        public BuildSettlementWorkshopAction(SettlementInstance settlement, PlayableWorkshopConstructionService service, IReadOnlyList<PlayableWorkshopDefinition> definitions, PlayableWorkshopDefinition definition, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            this.definition = definition;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public SettlementWorkshopConstructionResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (definition == null || !ContainsDefinition(definition)) return Fail("工坊蓝图不属于当前营地。");
            if (!service.CanBuild(definition, out string reason)) return Fail(reason);

            Dictionary<string, int> previousResources = CaptureResourceAmounts();
            if (!HasAggregateResources(previousResources, out reason)) return Fail(reason);
            cancellationToken.ThrowIfCancellationRequested();
            if (!service.TryBuild(definition, out reason)) return Fail(reason);

            foreach (KeyValuePair<string, int> previous in previousResources)
                eventOutbox.Stage(new ResourceChangedEvent { ResourceName = previous.Key, OldAmount = previous.Value, NewAmount = settlement.GetResource(previous.Key) });
            Result = new SettlementWorkshopConstructionResult(true, string.Empty, definition.WorkshopId);
            eventOutbox.Stage(new SettlementWorkshopBuiltEvent { WorkshopId = definition.WorkshopId });
            eventOutbox.Stage(new SettlementTransactionCommittedEvent { TransactionId = $"workshop:{definition.WorkshopId}", Kind = SettlementTransactionKind.WorkshopConstruction });
            return UniTask.FromResult(ActionOutcome.Success());
        }

        private bool ContainsDefinition(PlayableWorkshopDefinition candidate)
        {
            foreach (PlayableWorkshopDefinition item in definitions)
                if (ReferenceEquals(item, candidate)) return true;
            return false;
        }

        private Dictionary<string, int> CaptureResourceAmounts()
        {
            var amounts = new Dictionary<string, int>();
            foreach (PlayableWorkshopCost cost in definition.Costs)
                if (cost?.Item != null && !amounts.ContainsKey(cost.Item.ContentId))
                    amounts.Add(cost.Item.ContentId, settlement.GetResource(cost.Item));
            return amounts;
        }

        private bool HasAggregateResources(Dictionary<string, int> availableResources, out string reason)
        {
            var totals = new Dictionary<string, int>();
            foreach (PlayableWorkshopCost cost in definition.Costs)
            {
                if (cost?.Item == null) continue;
                totals.TryGetValue(cost.Item.ContentId, out int total);
                totals[cost.Item.ContentId] = total + cost.Amount;
            }
            foreach (KeyValuePair<string, int> total in totals)
                if (!availableResources.TryGetValue(total.Key, out int available) || available < total.Value)
                {
                    reason = $"资源不足：{total.Key}";
                    return false;
                }
            reason = string.Empty;
            return true;
        }

        private UniTask<ActionOutcome> Fail(string reason)
        {
            Result = SettlementWorkshopConstructionResult.Failed(reason);
            return UniTask.FromResult(ActionOutcome.Failure(reason));
        }
    }
}
