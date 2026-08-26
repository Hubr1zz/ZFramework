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
    public readonly struct SettlementHuntReturnCommandResult
    {
        public SettlementHuntReturnCommandResult(bool succeeded, bool applied, string reason, IReadOnlyList<EventData> events, bool seasonAdvanced = false, bool yearAdvanced = false, int currentYear = 0, int currentSeasonIndex = 0)
        {
            Succeeded = succeeded;
            Applied = applied;
            Reason = reason ?? string.Empty;
            Events = events ?? Array.Empty<EventData>();
            SeasonAdvanced = seasonAdvanced;
            YearAdvanced = yearAdvanced;
            CurrentYear = currentYear;
            CurrentSeasonIndex = currentSeasonIndex;
        }

        public bool Succeeded { get; }
        public bool Applied { get; }
        public string Reason { get; }
        public IReadOnlyList<EventData> Events { get; }
        public bool SeasonAdvanced { get; }
        public bool YearAdvanced { get; }
        public int CurrentYear { get; }
        public int CurrentSeasonIndex { get; }

        public static SettlementHuntReturnCommandResult Failed(string reason) => new(false, false, reason, Array.Empty<EventData>());
    }

    /// <summary>在 Settlement Runner 的单个 root 内提交远征记录、资源、猎人成长、年份和年度 Timeline。</summary>
    public sealed class ApplySettlementHuntReturnAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly TimelineSystem timeline;
        private readonly HuntRecord huntRecord;
        private readonly ActionEventOutbox eventOutbox;
        private readonly SettlementInstance settlement;
        private readonly HunterManagementSystem hunterManagement;
        private readonly IPlayableCampaignPersistentEffectProjection persistentEffectProjection;

        public ApplySettlementHuntReturnAction(TimelineSystem timeline, HuntRecord huntRecord, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
            : this(timeline, huntRecord, eventOutbox, null, null, null, source, target)
        {
        }

        public ApplySettlementHuntReturnAction(TimelineSystem timeline, HuntRecord huntRecord, ActionEventOutbox eventOutbox, SettlementInstance settlement, HunterManagementSystem hunterManagement, IPlayableCampaignPersistentEffectProjection persistentEffectProjection, IReactorEntity source, IReactorEntity target)
        {
            this.timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
            this.huntRecord = huntRecord ?? throw new ArgumentNullException(nameof(huntRecord));
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            this.settlement = settlement;
            this.hunterManagement = hunterManagement;
            this.persistentEffectProjection = persistentEffectProjection;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public SettlementHuntReturnCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool alreadyApplied = timeline.HasAppliedHuntRecord(huntRecord);
            if (!HuntReturnRules.TryCreatePlan(CreateInput(), timeline.CurrentYear, BuildParticipantStates(), BuildResourceStates(), alreadyApplied, out HuntReturnPlan plan, out string reason))
                return Fail(reason);
            if (!plan.IsAlreadyApplied && !plan.IsLegacyCompatibility && (settlement == null || hunterManagement == null))
                return Fail("当前远征归来缺少 Settlement 提交环境。");
            if (!plan.IsAlreadyApplied && !timeline.TryCreateCalendarAdvancePlan(huntRecord, out _, out reason))
                return Fail(reason);
            if (settlement != null && persistentEffectProjection != null && !persistentEffectProjection.TryClear(settlement, out reason))
                return Fail(reason);
            if (plan.IsAlreadyApplied)
                {
                Result = new SettlementHuntReturnCommandResult(true, false, string.Empty, Array.Empty<EventData>());
                return UniTask.FromResult(ActionOutcome.Success());
            }
            if (!plan.IsLegacyCompatibility)
            {
                ApplyResourceGrants(plan.ResourceGrants);
                ClearCollectibles();
                PlayableHunterAdvancementAdapter.ApplyAfterHunt(ResolveParticipants(plan), hunterManagement, eventOutbox);
            }

            IReadOnlyList<EventData> events = timeline.AdvanceCalendar(huntRecord, out CampaignCalendarAdvancePlan committedPlan, out reason);
            if (!string.IsNullOrEmpty(reason)) return Fail(reason);
            SeasonDefinition completedSeason = null;
            SeasonDefinition advancedSeason = null;
            if (timeline.Calendar != null)
            {
                timeline.Calendar.TryGetSeason(committedPlan.CurrentSeasonIndex, out completedSeason);
                timeline.Calendar.TryGetSeason(committedPlan.NextSeasonIndex, out advancedSeason);
            }
            Result = new SettlementHuntReturnCommandResult(true, true, string.Empty, events, committedPlan.SeasonAdvanced, committedPlan.YearAdvanced, timeline.CurrentYear, timeline.CurrentSeasonIndex);
            eventOutbox.StageAfterCommit(new SeasonAdvancedEvent
            {
                CalendarId = timeline.Calendar.CalendarId,
                PreviousYear = committedPlan.CurrentYear,
                PreviousSeasonIndex = committedPlan.CurrentSeasonIndex,
                NewYear = committedPlan.NextYear,
                NewSeasonIndex = committedPlan.NextSeasonIndex
            });
            eventOutbox.StageAfterCommit(new HuntCompletedEvent
            {
                CompletedYear = huntRecord.Year,
                CompletedSeasonIndex = committedPlan.CurrentSeasonIndex,
                CompletedSeasonId = completedSeason?.Id ?? string.Empty,
                CompletedSeasonDisplayName = completedSeason?.DisplayName ?? string.Empty,
                TotalHunts = timeline.TotalHunts,
                HuntersDeployed = huntRecord.HuntersDeployed,
                HuntersLost = huntRecord.HuntersLost,
                CollectedResourceCount = plan.CollectedResourceCount,
                BossDefeated = huntRecord.BossDefeated,
                AdvancedToYear = timeline.CurrentYear,
                AdvancedToSeasonIndex = timeline.CurrentSeasonIndex,
                AdvancedToSeasonId = advancedSeason?.Id ?? string.Empty,
                AdvancedToSeasonDisplayName = advancedSeason?.DisplayName ?? string.Empty,
                CalendarId = timeline.Calendar.CalendarId
            });
            if (committedPlan.YearAdvanced)
            {
                eventOutbox.StageAfterCommit(new YearAdvancedEvent { NewYear = timeline.CurrentYear, NewSeasonIndex = timeline.CurrentSeasonIndex, CalendarId = timeline.Calendar.CalendarId });
            }
            return UniTask.FromResult(ActionOutcome.Success());
        }

        private HuntReturnInput CreateInput()
        {
            var resourceIds = new List<string>();
            if (huntRecord.CollectedResources != null)
                foreach (string resourceId in huntRecord.CollectedResources)
                    resourceIds.Add(PlayableSettlementItemRegistry.ResolveContentId(resourceId));
            return new HuntReturnInput(huntRecord.RecordId, huntRecord.ReturnSchemaVersion, huntRecord.Year, huntRecord.HuntersDeployed, huntRecord.HuntersLost, huntRecord.ParticipantHunterIds, resourceIds);
        }

        private List<HuntReturnParticipantState> BuildParticipantStates()
        {
            if (settlement == null || huntRecord.ParticipantHunterIds == null) return new List<HuntReturnParticipantState>();
            var states = new List<HuntReturnParticipantState>();
            foreach (int hunterId in huntRecord.ParticipantHunterIds)
            {
                HunterInstance hunter = settlement.GetHunter(hunterId);
                if (hunter != null)
                    states.Add(new HuntReturnParticipantState(hunter.InstanceId, !hunter.IsDead, hunter.Availability, hunter.Age));
            }
            return states;
        }

        private List<HuntReturnResourceState> BuildResourceStates()
        {
            var states = new List<HuntReturnResourceState>();
            if (settlement == null || huntRecord.CollectedResources == null || huntRecord.CollectedResources.Count == 0) return states;
            var collectedCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string rawId in huntRecord.CollectedResources)
            {
                string resourceId = PlayableSettlementItemRegistry.ResolveContentId(rawId);
                if (!PlayableSettlementItemRegistry.TryGet(resourceId, out ItemData item) || item == null || item.itemType != ItemType.Resource)
                    continue;
                collectedCounts[resourceId] = collectedCounts.TryGetValue(resourceId, out int count) ? count + 1 : 1;
            }
            foreach (KeyValuePair<string, int> collected in collectedCounts)
            {
                string resourceId = collected.Key;
                if (!PlayableSettlementItemRegistry.TryGet(resourceId, out ItemData item) || item == null) continue;
                bool alreadyAdded = false;
                foreach (HuntReturnResourceState state in states)
                    if (state.ResourceId == resourceId) { alreadyAdded = true; break; }
                if (!alreadyAdded)
                    states.Add(new HuntReturnResourceState(resourceId, settlement.GetResource(resourceId)));
            }
            return states;
        }

        private List<HunterInstance> ResolveParticipants(HuntReturnPlan plan)
        {
            var hunters = new List<HunterInstance>();
            foreach (HuntReturnParticipantPlan participant in plan.ParticipantPlans)
            {
                if (!participant.ShouldAdvance)
                    continue;
                HunterInstance hunter = settlement.GetHunter(participant.HunterId);
                if (hunter != null)
                    hunters.Add(hunter);
            }
            return hunters;
        }

        private void ApplyResourceGrants(IReadOnlyList<HuntReturnResourceGrant> grants)
        {
            foreach (HuntReturnResourceGrant grant in grants)
            {
                int oldAmount = settlement.GetResource(grant.ResourceId);
                settlement.AddResource(grant.ResourceId, grant.Amount);
                settlement.DiscoverMaterial(grant.ResourceId);
                eventOutbox.Stage(new ResourceChangedEvent { ResourceName = grant.ResourceId, OldAmount = oldAmount, NewAmount = settlement.GetResource(grant.ResourceId) });
            }
        }

        private void ClearCollectibles()
        {
            if (huntRecord.ParticipantHunterIds == null) return;
            foreach (int hunterId in huntRecord.ParticipantHunterIds)
                settlement.GetHunter(hunterId)?.Collectibles?.Clear();
        }

        private UniTask<ActionOutcome> Fail(string reason)
        {
            Result = SettlementHuntReturnCommandResult.Failed(reason);
            return UniTask.FromResult(ActionOutcome.Failure(reason));
        }
    }
}
