using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunt;
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

    public struct HuntPopulationReturnedEvent
    {
        public string RecordId;
        public int RescuedPopulation;
        public int PreviousPopulation;
        public int CurrentPopulation;
    }

    /// <summary>在 Settlement Runner 的单个 root 内提交远征记录、资源、猎人成长、日历和回营事件 Timeline。</summary>
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
            if (!EventResolutionMemoryRules.TryValidateHuntRecord(huntRecord, out string memoryReason))
                return Fail(memoryReason);
            bool alreadyApplied = timeline.HasAppliedHuntRecord(huntRecord);
            int currentPopulation = settlement?.Population ?? 0;
            if (!HuntReturnRules.TryCreateItemPlan(CreateInput(), timeline.CurrentYear, BuildParticipantStates(), BuildItemStates(), currentPopulation, alreadyApplied, out HuntReturnPlan plan, out string reason))
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
                ApplyItemGrants(plan.ItemGrants);
                settlement.Population = plan.NewPopulation;
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
                CollectedItemCount = plan.CollectedItemCount,
                BossDefeated = huntRecord.BossDefeated,
                AdvancedToYear = timeline.CurrentYear,
                AdvancedToSeasonIndex = timeline.CurrentSeasonIndex,
                AdvancedToSeasonId = advancedSeason?.Id ?? string.Empty,
                AdvancedToSeasonDisplayName = advancedSeason?.DisplayName ?? string.Empty,
                CalendarId = timeline.Calendar.CalendarId
            });
            if (plan.RescuedPopulation > 0)
                eventOutbox.StageAfterCommit(new HuntPopulationReturnedEvent { RecordId = plan.RecordId, RescuedPopulation = plan.RescuedPopulation, PreviousPopulation = plan.PreviousPopulation, CurrentPopulation = plan.NewPopulation });
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
            var items = new List<HuntLootStack>();
            if (huntRecord.CollectedItems != null)
                foreach (HuntLootStack stack in huntRecord.CollectedItems)
                    items.Add(stack == null ? null : new HuntLootStack(PlayableSettlementItemRegistry.ResolveContentId(stack.ItemId), stack.Count));
            return new HuntReturnInput(huntRecord.RecordId, huntRecord.ReturnSchemaVersion, huntRecord.Year, huntRecord.HuntersDeployed, huntRecord.HuntersLost, huntRecord.ParticipantHunterIds, resourceIds, items, huntRecord.RescuedPopulation);
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

        private List<HuntReturnItemState> BuildItemStates()
        {
            var states = new List<HuntReturnItemState>();
            if (settlement == null) return states;
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            if (huntRecord.ReturnSchemaVersion == HuntReturnRules.ResourceOnlySchemaVersion)
            {
                foreach (string rawId in huntRecord.CollectedResources ?? new List<string>())
                    itemIds.Add(PlayableSettlementItemRegistry.ResolveContentId(rawId));
            }
            else
            {
                foreach (HuntLootStack stack in huntRecord.CollectedItems ?? new List<HuntLootStack>())
                    if (stack != null)
                        itemIds.Add(PlayableSettlementItemRegistry.ResolveContentId(stack.ItemId));
            }
            foreach (string itemId in itemIds)
            {
                if (!PlayableSettlementItemRegistry.TryGet(itemId, out ItemData item) || item == null) continue;
                HuntReturnItemKind kind = item.itemType == ItemType.Resource ? HuntReturnItemKind.Resource : HuntReturnItemKind.StoredItem;
                int currentAmount = kind == HuntReturnItemKind.Resource ? settlement.GetResource(itemId) : settlement.GetStoredItem(itemId);
                states.Add(new HuntReturnItemState(itemId, kind, currentAmount));
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

        private void ApplyItemGrants(IReadOnlyList<HuntReturnItemGrant> grants)
        {
            foreach (HuntReturnItemGrant grant in grants)
            {
                if (grant.Kind == HuntReturnItemKind.StoredItem)
                {
                    settlement.AddStoredItem(grant.ItemId, grant.Amount);
                    continue;
                }
                int oldAmount = settlement.GetResource(grant.ItemId);
                settlement.AddResource(grant.ItemId, grant.Amount);
                settlement.DiscoverMaterial(grant.ItemId);
                eventOutbox.Stage(new ResourceChangedEvent { ResourceName = grant.ItemId, OldAmount = oldAmount, NewAmount = settlement.GetResource(grant.ItemId) });
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
