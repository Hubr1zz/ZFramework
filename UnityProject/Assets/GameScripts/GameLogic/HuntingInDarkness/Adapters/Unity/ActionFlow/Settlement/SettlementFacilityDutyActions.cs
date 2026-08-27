using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;

namespace HuntingInDarkness.ActionFlow.Settlement
{
    public readonly struct SettlementFacilityDutyCommandResult
    {
        public SettlementFacilityDutyCommandResult(bool succeeded, string reason, string dutyId, int populationGain, int roll)
        {
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
            DutyId = dutyId ?? string.Empty;
            PopulationGain = populationGain;
            Roll = roll;
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public string DutyId { get; }
        public int PopulationGain { get; }
        public int Roll { get; }
        public static SettlementFacilityDutyCommandResult Failed(string reason) => new(false, reason, string.Empty, 0, 0);
    }

    public struct SettlementFacilityDutyAssignedEvent
    {
        public string DutyId;
        public string FacilityId;
        public int HunterId;
        public int DueYear;
        public int DueSeasonIndex;
    }

    public struct SettlementFacilityDutyCancelledEvent
    {
        public string DutyId;
        public int HunterId;
    }

    public struct SettlementFacilityDutyResolvedEvent
    {
        public string DutyId;
        public string FacilityId;
        public int HunterId;
        public int Roll;
        public int PopulationGain;
        public int PreviousPopulation;
        public int CurrentPopulation;
    }

    internal static class SettlementFacilityDutyActionHelpers
    {
        public static SettlementFacilityDutyDefinition Find(IReadOnlyList<SettlementFacilityDutyDefinition> definitions, string dutyId)
        {
            foreach (SettlementFacilityDutyDefinition definition in definitions ?? Array.Empty<SettlementFacilityDutyDefinition>())
                if (definition != null && string.Equals(definition.DutyId, dutyId?.Trim(), StringComparison.Ordinal)) return definition;
            return null;
        }

        public static UniTask<ActionOutcome> Fail(string reason)
        {
            return UniTask.FromResult(ActionOutcome.Failure(reason));
        }
    }

    public sealed class AssignSettlementFacilityDutyAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly SettlementInstance settlement;
        private readonly IReadOnlyList<SettlementFacilityDutyDefinition> definitions;
        private readonly string dutyId;
        private readonly string facilityId;
        private readonly int hunterId;
        private readonly int seasonsPerYear;
        private readonly string calendarId;
        private readonly ActionEventOutbox eventOutbox;

        public AssignSettlementFacilityDutyAction(SettlementInstance settlement, IReadOnlyList<SettlementFacilityDutyDefinition> definitions, string dutyId, string facilityId, int hunterId, int seasonsPerYear, string calendarId, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.definitions = definitions ?? Array.Empty<SettlementFacilityDutyDefinition>();
            this.dutyId = dutyId;
            this.facilityId = facilityId;
            this.hunterId = hunterId;
            this.seasonsPerYear = seasonsPerYear;
            this.calendarId = calendarId ?? string.Empty;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public SettlementFacilityDutyCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SettlementFacilityDutyDefinition definition = SettlementFacilityDutyActionHelpers.Find(definitions, dutyId);
            HunterInstance hunter = settlement.GetHunter(hunterId);
            bool requirementMet = definition != null && (string.IsNullOrWhiteSpace(definition.RequiredInventionId) ? settlement.IsWorkshopBuilt(facilityId) : settlement.IsInventionUnlocked(definition.RequiredInventionId));
            if (!requirementMet || hunter == null || !hunter.IsAvailable || settlement.GetDepartureEligibleHunters(settlement.CurrentYear, settlement.CurrentSeasonIndex).Count <= 1)
                return Fail("设施、发明或猎人资格不满足值守条件。");
            if (settlement.HasDueFacilityDuty(settlement.CurrentYear, settlement.CurrentSeasonIndex) || settlement.HasActiveFacilityDuty(definition.DutyId) || settlement.HasAssignedFacilityDuty(hunterId))
                return Fail("当前值守岗位或猎人已经被占用。");
            if (!SettlementFacilityDutyRules.TryCreateState(definition, facilityId, hunterId, settlement.CurrentYear, settlement.CurrentSeasonIndex, seasonsPerYear, out SettlementFacilityDutyState state, out string reason)) return Fail(reason);
            state.CalendarId = calendarId;
            if (!settlement.TryAddFacilityDuty(state, out reason)) return Fail(reason);
            Result = new SettlementFacilityDutyCommandResult(true, string.Empty, state.DutyId, 0, 0);
            eventOutbox.StageAfterCommit(new SettlementFacilityDutyAssignedEvent { DutyId = state.DutyId, FacilityId = state.FacilityId, HunterId = state.AssignedHunterId, DueYear = state.DueYear, DueSeasonIndex = state.DueSeasonIndex });
            eventOutbox.StageAfterCommit(new SettlementTransactionCommittedEvent { TransactionId = $"facility-duty:assign:{state.AssignmentId}", Kind = SettlementTransactionKind.FacilityDuty });
            return UniTask.FromResult(ActionOutcome.Success());
        }

        private UniTask<ActionOutcome> Fail(string reason)
        {
            Result = SettlementFacilityDutyCommandResult.Failed(reason);
            return SettlementFacilityDutyActionHelpers.Fail(reason);
        }
    }

    public sealed class CancelSettlementFacilityDutyAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly SettlementInstance settlement;
        private readonly string dutyId;
        private readonly ActionEventOutbox eventOutbox;
        public CancelSettlementFacilityDutyAction(SettlementInstance settlement, string dutyId, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.dutyId = dutyId;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }
        public SettlementFacilityDutyCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }
        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!settlement.TryGetFacilityDuty(dutyId, out SettlementFacilityDutyState state)) return Fail("值守岗位不存在。");
            if (SettlementFacilityDutyRules.IsDue(state, settlement.CurrentYear, settlement.CurrentSeasonIndex)) return Fail("已到期的值守岗位必须先结算。");
            string assignmentId = state.AssignmentId;
            if (!settlement.TryRemoveFacilityDuty(assignmentId)) return Fail("值守岗位状态无法清理。");
            Result = new SettlementFacilityDutyCommandResult(true, string.Empty, state.DutyId, 0, 0);
            eventOutbox.StageAfterCommit(new SettlementFacilityDutyCancelledEvent { DutyId = state.DutyId, HunterId = state.AssignedHunterId });
            eventOutbox.StageAfterCommit(new SettlementTransactionCommittedEvent { TransactionId = $"facility-duty:cancel:{state.AssignmentId}", Kind = SettlementTransactionKind.FacilityDuty });
            return UniTask.FromResult(ActionOutcome.Success());
        }
        private UniTask<ActionOutcome> Fail(string reason)
        {
            Result = SettlementFacilityDutyCommandResult.Failed(reason);
            return SettlementFacilityDutyActionHelpers.Fail(reason);
        }
    }

    public sealed class ResolveSettlementFacilityDutyAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly SettlementInstance settlement;
        private readonly IReadOnlyList<SettlementFacilityDutyDefinition> definitions;
        private readonly string dutyId;
        private readonly ITabletopRandomInteractionPresenter presenter;
        private readonly ActionEventOutbox eventOutbox;
        public ResolveSettlementFacilityDutyAction(SettlementInstance settlement, IReadOnlyList<SettlementFacilityDutyDefinition> definitions, string dutyId, ITabletopRandomInteractionPresenter presenter, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.definitions = definitions ?? Array.Empty<SettlementFacilityDutyDefinition>();
            this.dutyId = dutyId;
            this.presenter = presenter;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }
        public SettlementFacilityDutyCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }
        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!settlement.TryGetFacilityDuty(dutyId, out SettlementFacilityDutyState state) || !SettlementFacilityDutyRules.IsDue(state, settlement.CurrentYear, settlement.CurrentSeasonIndex)) return await FailAsync("值守岗位尚未到期或内容未配置。");
            SettlementFacilityDutyDefinition definition = SettlementFacilityDutyActionHelpers.Find(definitions, state.DutyId);
            if (definition == null) return await FailAsync("值守岗位尚未到期或内容未配置。");
            HunterInstance hunter = settlement.GetHunter(state.AssignedHunterId);
            if (hunter == null || !hunter.IsAlive || !hunter.IsAvailable)
            {
                string assignmentId = state.AssignmentId;
                Result = new SettlementFacilityDutyCommandResult(true, string.Empty, state.DutyId, 0, 0);
                eventOutbox.StageAfterCommit(new SettlementFacilityDutyResolvedEvent { DutyId = state.DutyId, FacilityId = state.FacilityId, HunterId = state.AssignedHunterId });
                eventOutbox.StageAfterCommit(new SettlementTransactionCommittedEvent { TransactionId = $"facility-duty:resolve:{state.AssignmentId}", Kind = SettlementTransactionKind.FacilityDuty });
                settlement.TryRemoveFacilityDuty(assignmentId);
                return ActionOutcome.Success();
            }
            if (presenter == null) return await FailAsync("桌面骰子尚未配置。");
            var request = new TabletopRandomInteractionRequest($"facility-duty:{state.DutyId}:{state.StartYear}:{state.StartSeasonIndex}:{state.AssignedHunterId}", TabletopRandomInteractionKind.PhysicalDice, state.AssignedHunterId.ToString(), state.FacilityId, definition.DiceCount, definition.DiceSides, instruction: "为庇护所值守掷骰");
            TabletopRandomInteractionResult randomResult = await presenter.PresentAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!settlement.TryGetFacilityDuty(state.AssignmentId, out SettlementFacilityDutyState currentState) || !string.Equals(currentState.AssignmentId, state.AssignmentId, StringComparison.Ordinal) || !SettlementFacilityDutyRules.IsDue(currentState, settlement.CurrentYear, settlement.CurrentSeasonIndex)) return await FailAsync("值守岗位在掷骰期间已发生变化。");
            if (!TabletopRandomInteractionResultValidator.TryGetDiceTotal(request, randomResult, out int roll)) return await FailAsync("值守骰子结果无效或已取消。");
            int previousPopulation = Math.Max(0, settlement.Population);
            int gain = 0;
            if (hunter != null && hunter.IsAlive && hunter.IsAvailable && SettlementFacilityDutyRules.TryResolve(definition, roll, out SettlementFacilityDutyResolution resolution)) gain = resolution.PopulationGain;
            settlement.Population = SettlementFacilityDutyRules.SaturatePopulation(previousPopulation, gain);
            string resolvedAssignmentId = state.AssignmentId;
            Result = new SettlementFacilityDutyCommandResult(true, string.Empty, state.DutyId, settlement.Population - previousPopulation, roll);
            eventOutbox.StageAfterCommit(new SettlementFacilityDutyResolvedEvent { DutyId = state.DutyId, FacilityId = state.FacilityId, HunterId = state.AssignedHunterId, Roll = roll, PopulationGain = settlement.Population - previousPopulation, PreviousPopulation = previousPopulation, CurrentPopulation = settlement.Population });
            eventOutbox.StageAfterCommit(new SettlementTransactionCommittedEvent { TransactionId = $"facility-duty:resolve:{state.AssignmentId}", Kind = SettlementTransactionKind.FacilityDuty });
            settlement.TryRemoveFacilityDuty(resolvedAssignmentId);
            return ActionOutcome.Success();
        }
        private async UniTask<ActionOutcome> FailAsync(string reason)
        {
            Result = SettlementFacilityDutyCommandResult.Failed(reason);
            return await UniTask.FromResult(ActionOutcome.Failure(reason));
        }
    }
}
