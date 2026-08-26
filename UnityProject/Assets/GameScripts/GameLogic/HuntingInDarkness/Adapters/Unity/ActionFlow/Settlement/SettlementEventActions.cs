using System;
using System.Collections.Generic;
using CardGame.ActionQueue;
using Core;
using GameplayBase;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace HuntingInDarkness.ActionFlow.Settlement
{
    public readonly struct SettlementEventCommandResult
    {
        private SettlementEventCommandResult(bool succeeded, string reason, int resolvedCount, bool encounterRequested, PlayableEventEffectBatchResult effectResults)
        {
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
            ResolvedCount = resolvedCount;
            EncounterRequested = encounterRequested;
            EffectResults = effectResults;
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public int ResolvedCount { get; }
        public bool EncounterRequested { get; }
        public PlayableEventEffectBatchResult EffectResults { get; }
        public int FailedEffectCount => EffectResults.FailedCount;
        public static SettlementEventCommandResult Success(int resolvedCount, bool encounterRequested) => Success(resolvedCount, encounterRequested, PlayableEventEffectBatchResult.Empty);
        public static SettlementEventCommandResult Success(int resolvedCount, bool encounterRequested, PlayableEventEffectBatchResult effectResults) => new(true, string.Empty, resolvedCount, encounterRequested, effectResults);
        public static SettlementEventCommandResult Failed(string reason, int resolvedCount) => new(false, reason, resolvedCount, false, PlayableEventEffectBatchResult.Empty);
    }

    /// <summary>营地事件列表的唯一流程根；节点、子链与跨环境交接均在同一 Action 因果链中执行。</summary>
    public sealed class ResolveSettlementEventChainAction : CompositeGameAction, ISourceAction, ITargetAction
    {
        private readonly EventSystem eventSystem;
        private readonly IPlayableEventInput eventInput;
        private readonly Guid sessionId;
        private readonly string chainId;
        private readonly ActionEventOutbox eventOutbox;
        private readonly Queue<PendingEventWork> pendingEvents = new();
        private readonly PlayableEventChainGuard chainGuard = new();
        private readonly Func<EventData, IReactorEntity> resolveEventEntity;
        private readonly ITabletopRandomInteractionPresenter randomInteractionPresenter;
        private readonly IPlayableEventSettlementCommand settlementCommand;
        private ResolvePlayableEventNodeAction currentEntry;
        private PendingEventWork currentWork;
        private IReadOnlyList<PlayableEventChainOccurrence> lastCommittedChildren = Array.Empty<PlayableEventChainOccurrence>();
        private string lastCommitDiagnostic = string.Empty;
        private int nextRootSequence = -1;
        private CampaignEncounterRequest encounterRequest;
        private string failureReason;
        private int resolvedCount;
        private readonly List<PlayableEventEffectResult> effectResults = new();
        private readonly SettlementEventChainCheckpointAdapter checkpointAdapter;

        public ResolveSettlementEventChainAction(EventSystem eventSystem, IPlayableEventInput eventInput, IReadOnlyList<EventData> events, Guid sessionId, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target, Func<EventData, IReactorEntity> resolveEventEntity, ITabletopRandomInteractionPresenter randomInteractionPresenter = null, string restoredChainId = null, IReadOnlyList<SettlementEventChainOccurrence> restoredOccurrences = null, IPlayableEventSettlementCommand settlementCommand = null)
            : this(eventSystem, eventInput, ToWorkItems(events, restoredOccurrences), sessionId, eventOutbox, source, target, resolveEventEntity, randomInteractionPresenter, restoredChainId, settlementCommand)
        {
        }

        public ResolveSettlementEventChainAction(EventSystem eventSystem, IPlayableEventInput eventInput, IReadOnlyList<SettlementEventWork> events, Guid sessionId, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target, Func<EventData, IReactorEntity> resolveEventEntity, ITabletopRandomInteractionPresenter randomInteractionPresenter = null, string restoredChainId = null, IPlayableEventSettlementCommand settlementCommand = null)
        {
            this.eventSystem = eventSystem ?? throw new ArgumentNullException(nameof(eventSystem));
            this.eventInput = eventInput;
            this.sessionId = sessionId;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            this.resolveEventEntity = resolveEventEntity ?? throw new ArgumentNullException(nameof(resolveEventEntity));
            this.randomInteractionPresenter = randomInteractionPresenter;
            this.settlementCommand = settlementCommand;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            checkpointAdapter = new SettlementEventChainCheckpointAdapter(eventSystem.Settlement);
            chainId = string.IsNullOrWhiteSpace(restoredChainId) ? Guid.NewGuid().ToString("N") : restoredChainId.Trim();
            if (events == null) return;
            var validWorks = new List<SettlementEventWork>(events.Count);
            foreach (SettlementEventWork work in events)
            {
                if (!ValidateWork(work, out string reason))
                {
                    failureReason = reason;
                    return;
                }
                validWorks.Add(work);
            }
            foreach (SettlementEventWork work in validWorks)
                TryEnqueue(work, work.RestoredOccurrence != null ? work.RestoredOccurrence.Sequence : nextRootSequence--, work.RestoredOccurrence?.AncestorEventIds);
        }

        public SettlementEventCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        public void Append(EventData gameEvent)
        {
            TryEnqueue(new SettlementEventWork(gameEvent), nextRootSequence--);
        }

        public void Append(SettlementEventWork work) => TryEnqueue(work, nextRootSequence--);

        protected override GameAction GetNextChild(CompositeExecutionContext context)
        {
            if (currentEntry != null)
            {
                if (!context.LastOutcome.IsSuccess)
                {
                    if (currentEntry.ResolutionCheckpointPublished && string.IsNullOrWhiteSpace(lastCommitDiagnostic))
                    {
                        resolvedCount++;
                        effectResults.AddRange(currentEntry.EffectResults.Effects);
                        EnqueuePersistedChildren(currentEntry.ChainedEvents, AppendAncestor(currentWork.AncestorEventIds, currentEntry.EventId));
                        if (currentEntry.EncounterIds.Count > 0)
                        {
                            string encounterId = string.IsNullOrWhiteSpace(currentEntry.EncounterIds[0]) ? PlayableEncounterRuntime.DefaultEncounterId : currentEntry.EncounterIds[0];
                            encounterRequest = new CampaignEncounterRequest(sessionId, encounterId, CampaignEncounterSourceKind.SettlementEvent, GamePhase.Settlement, Vector2Int.zero, currentEntry.EventId, "settlement");
                        }
                        currentEntry = null;
                        if (pendingEvents.Count > 0) return GetNextChild(context);
                        return null;
                    }
                    failureReason = context.LastOutcome.Reason;
                    currentEntry = null;
                    return null;
                }

                resolvedCount++;
                effectResults.AddRange(currentEntry.EffectResults.Effects);
                if (!string.IsNullOrWhiteSpace(lastCommitDiagnostic))
                {
                    failureReason = lastCommitDiagnostic;
                    currentEntry = null;
                    return null;
                }
                if (currentEntry.EncounterIds.Count > 0)
                {
                    string encounterId = string.IsNullOrWhiteSpace(currentEntry.EncounterIds[0]) ? PlayableEncounterRuntime.DefaultEncounterId : currentEntry.EncounterIds[0];
                    encounterRequest = new CampaignEncounterRequest(sessionId, encounterId, CampaignEncounterSourceKind.SettlementEvent, GamePhase.Settlement, Vector2Int.zero, currentEntry.EventId, "settlement");
                    currentEntry = null;
                    return null;
                }

                IReadOnlyCollection<string> childAncestors = AppendAncestor(currentWork.AncestorEventIds, currentEntry.EventId);
                EnqueuePersistedChildren(currentEntry.ChainedEvents, childAncestors);
                currentEntry = null;
            }

            if (pendingEvents.Count == 0) return null;
            PendingEventWork nextWork = pendingEvents.Dequeue();
            EventData nextEvent = nextWork.Event;
            currentWork = nextWork;
            currentEntry = new ResolvePlayableEventNodeAction(eventSystem, eventInput, nextEvent, null, eventSystem.Settlement.GetAvailableHunters(), eventOutbox, checkpoint => StageCommitCheckpoint(checkpoint, nextWork), Source, resolveEventEntity(nextEvent), randomInteractionPresenter, settlementCommand: settlementCommand);
            return currentEntry;
        }

        protected override ActionOutcome Resolve(CompositeExecutionContext context)
        {
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                Result = SettlementEventCommandResult.Failed(failureReason, resolvedCount);
                return ActionOutcome.Failure(failureReason);
            }

            bool encounterRequested = !string.IsNullOrWhiteSpace(encounterRequest.EncounterId);
            Result = SettlementEventCommandResult.Success(resolvedCount, encounterRequested, new PlayableEventEffectBatchResult(effectResults));
            if (encounterRequested)
                eventOutbox.StageAfterCommit(new CampaignEncounterRequestedEvent { Request = encounterRequest });
            return ActionOutcome.Success();
        }

        private void StageCommitCheckpoint(PlayableEventCommitCheckpoint checkpoint, PendingEventWork work)
        {
            lastCommittedChildren = Array.Empty<PlayableEventChainOccurrence>();
            lastCommitDiagnostic = string.Empty;
            if (checkpoint.Kind == PlayableEventCommitKind.Resolution)
            {
                SettlementEventMemory memory = CreateEventMemory(checkpoint, work);
                string memoryReason = string.Empty;
                if (memory == null || !eventSystem.Settlement.CanRecordEventMemory(memory, out memoryReason))
                {
                    lastCommitDiagnostic = string.IsNullOrWhiteSpace(memoryReason) ? "营地事件结果记忆无效。" : memoryReason;
                    return;
                }
                if (work.Work.TimelineEntry != null && work.Work.TimelineEntry.IsCompleted && !string.Equals(work.Work.TimelineEntry.ResolutionMemoryId, memory.MemoryId, StringComparison.Ordinal))
                {
                    lastCommitDiagnostic = "营地事件对应的年鉴 occurrence 已完成且结果记忆不一致。";
                    return;
                }
                if (work.Work.TimelineEntry != null && !work.Work.TimelineEntry.IsCompleted && !eventSystem.TryMarkTimelineEntryCompleted(work.Work.TimelineEntry, checkpoint.EventId))
                {
                    lastCommitDiagnostic = "营地事件对应的年鉴 occurrence 已失效。";
                    return;
                }
                if (!eventSystem.Settlement.TryRecordEventMemory(memory, out memoryReason))
                {
                    lastCommitDiagnostic = memoryReason;
                    return;
                }
                if (work.Work.TimelineEntry != null) work.Work.TimelineEntry.ResolutionMemoryId = memory.MemoryId;
                var chainedEventIds = new List<string>();
                foreach (string chainedEventId in checkpoint.ChainedEventIds)
                {
                    bool cycle = string.Equals(chainedEventId, checkpoint.EventId, StringComparison.Ordinal);
                    if (!cycle)
                        foreach (string ancestorEventId in work.AncestorEventIds)
                            if (string.Equals(ancestorEventId, chainedEventId, StringComparison.Ordinal))
                            {
                                cycle = true;
                                break;
                            }
                    if (cycle)
                    {
                        continue;
                    }
                    chainedEventIds.Add(chainedEventId);
                }
                lastCommittedChildren = checkpointAdapter.Commit(chainId, work.PersistenceSequence, chainedEventIds, eventSystem.Settlement.CurrentYear, checkpoint.ActorId, AppendAncestor(work.AncestorEventIds, checkpoint.EventId));
                lastCommitDiagnostic = checkpointAdapter.GetDiagnostic(chainId);
            }
            eventOutbox.Stage(new SettlementTransactionCommittedEvent
            {
                TransactionId = checkpoint.Kind == PlayableEventCommitKind.Reroll ? $"settlement-event:{chainId}:{(work.PersistenceSequence >= 0 ? work.PersistenceSequence : resolvedCount)}:{checkpoint.EventId}:reroll" : $"settlement-event:{chainId}:{(work.PersistenceSequence >= 0 ? work.PersistenceSequence : resolvedCount)}:{checkpoint.EventId}",
                Kind = checkpoint.Kind == PlayableEventCommitKind.Reroll ? SettlementTransactionKind.EventReroll : SettlementTransactionKind.EventResolution
            });
        }

        private SettlementEventMemory CreateEventMemory(PlayableEventCommitCheckpoint checkpoint, PendingEventWork work)
        {
            if (!checkpoint.ResolutionFact.IsValid) return null;
            var memory = new SettlementEventMemory
            {
                MemoryId = $"settlement-event-memory:{chainId}:{work.PersistenceSequence}:{checkpoint.EventId}",
                EventId = checkpoint.ResolutionFact.EventId,
                EventName = checkpoint.ResolutionFact.EventName,
                ResolutionMode = checkpoint.ResolutionFact.ResolutionMode,
                SelectionMode = checkpoint.ResolutionFact.SelectionMode,
                OptionId = checkpoint.ResolutionFact.OptionId,
                OptionText = checkpoint.ResolutionFact.OptionText,
                Year = checkpoint.ResolutionFact.Year,
                ActorId = checkpoint.ResolutionFact.ActorId,
                CheckType = checkpoint.ResolutionFact.CheckType,
                HasCheck = checkpoint.ResolutionFact.HasCheck,
                Success = checkpoint.ResolutionFact.Success,
                RollValue = checkpoint.ResolutionFact.RollValue,
                Bonus = checkpoint.ResolutionFact.Bonus,
                Total = checkpoint.ResolutionFact.Total,
                Target = checkpoint.ResolutionFact.Target,
                WasRerolled = checkpoint.ResolutionFact.WasRerolled,
                ResultText = checkpoint.ResolutionFact.ResultText
            };
            foreach (PlayableEventEffectResult effect in checkpoint.ResolutionFact.EffectResults)
                memory.Effects.Add(new SettlementEventMemoryEffect
                {
                    EffectIndex = effect.EffectIndex,
                    EffectType = effect.EffectType?.ToString() ?? string.Empty,
                    TargetName = effect.TargetName,
                    ResolvedTargetId = effect.ResolvedTargetId,
                    Applied = effect.Succeeded,
                    Reason = effect.Reason,
                    TargetActorId = effect.TargetActorId,
                    StateChanged = effect.StateChanged,
                    PreviousValue = effect.PreviousValue,
                    CurrentValue = effect.CurrentValue
                });
            return memory;
        }

        private void EnqueuePersistedChildren(IReadOnlyList<EventData> chainedEvents, IReadOnlyCollection<string> ancestorEventIds)
        {
            if (chainedEvents == null) return;
            int occurrenceIndex = 0;
            foreach (EventData chainedEvent in chainedEvents)
            {
                if (chainedEvent == null) continue;
                if (ContainsAncestor(ancestorEventIds, chainedEvent))
                {
                    eventOutbox.Stage(new PlayableEventDuplicatePreventedEvent { EventId = chainedEvent.ContentId });
                    continue;
                }
                if (occurrenceIndex >= lastCommittedChildren.Count) break;
                PlayableEventChainOccurrence occurrence = lastCommittedChildren[occurrenceIndex];
                string eventId = chainedEvent.ContentId;
                if (!string.Equals(eventId, occurrence.EventId, StringComparison.Ordinal)) continue;
                TryEnqueue(new SettlementEventWork(chainedEvent, null, null), occurrence.Sequence, occurrence.AncestorEventIds);
                occurrenceIndex++;
            }
            lastCommittedChildren = Array.Empty<PlayableEventChainOccurrence>();
        }

        private void TryEnqueue(EventData gameEvent, int persistenceSequence = -1, IReadOnlyCollection<string> ancestorEventIds = null)
        {
            TryEnqueue(new SettlementEventWork(gameEvent), persistenceSequence, ancestorEventIds);
        }

        private void TryEnqueue(SettlementEventWork work, int persistenceSequence = -1, IReadOnlyCollection<string> ancestorEventIds = null)
        {
            EventData gameEvent = work.Event;
            if (gameEvent == null) return;
            if (ancestorEventIds != null && ContainsAncestor(ancestorEventIds, gameEvent))
            {
                eventOutbox.Stage(new PlayableEventDuplicatePreventedEvent { EventId = gameEvent.ContentId });
                return;
            }

            string occurrenceKey = work.TimelineEntry != null
                ? $"{chainId}:timeline:{persistenceSequence}"
                : null;
            bool scheduled = !string.IsNullOrWhiteSpace(occurrenceKey)
                ? chainGuard.TrySchedule(gameEvent, occurrenceKey)
                : persistenceSequence > 0
                ? chainGuard.TrySchedule(gameEvent, $"{chainId}:{persistenceSequence}")
                : chainGuard.TrySchedule(gameEvent);
            if (scheduled)
            {
                pendingEvents.Enqueue(new PendingEventWork(work, persistenceSequence, ancestorEventIds));
                return;
            }
            eventOutbox.Stage(new PlayableEventDuplicatePreventedEvent { EventId = gameEvent.ContentId });
        }

        private static bool ContainsAncestor(IReadOnlyCollection<string> ancestorEventIds, EventData gameEvent)
        {
            if (gameEvent == null) return false;
            string eventId = gameEvent.ContentId;
            foreach (string ancestorEventId in ancestorEventIds)
                if (string.Equals(ancestorEventId, eventId, StringComparison.Ordinal)) return true;
            return false;
        }

        private static IReadOnlyCollection<string> AppendAncestor(IReadOnlyCollection<string> ancestorEventIds, string eventId)
        {
            var result = new List<string>();
            if (ancestorEventIds != null)
                foreach (string ancestorEventId in ancestorEventIds)
                    if (!string.IsNullOrWhiteSpace(ancestorEventId) && !result.Contains(ancestorEventId)) result.Add(ancestorEventId);
            if (!string.IsNullOrWhiteSpace(eventId) && !result.Contains(eventId)) result.Add(eventId);
            return result;
        }

        private bool ValidateWork(SettlementEventWork work, out string reason)
        {
            reason = string.Empty;
            if (work.Event == null)
            {
                reason = "营地事件工作项缺少事件内容。";
                return false;
            }
            if (work.TimelineEntry != null)
            {
                bool belongsToSettlement = eventSystem.Settlement?.Timeline != null && eventSystem.Settlement.Timeline.Contains(work.TimelineEntry);
                if (!belongsToSettlement || work.TimelineEntry.IsCompleted || !PlayableSettlementEventRegistry.IsTimelineEventEntry(work.TimelineEntry) || !string.Equals(work.TimelineEntry.EventId, work.Event.ContentId, StringComparison.Ordinal))
                {
                    reason = "营地事件工作项包含无效或不匹配的年鉴 occurrence。";
                    return false;
                }
            }
            if (work.RestoredOccurrence != null && !string.Equals(work.RestoredOccurrence.EventId, work.Event.ContentId, StringComparison.Ordinal))
            {
                reason = "营地事件恢复 occurrence 与事件内容不匹配。";
                return false;
            }
            if (work.TimelineEntry != null && work.RestoredOccurrence != null)
            {
                reason = "营地事件工作项不能同时绑定年鉴与子链 occurrence。";
                return false;
            }
            return true;
        }

        private readonly struct PendingEventWork
        {
            public PendingEventWork(SettlementEventWork work, int persistenceSequence, IReadOnlyCollection<string> ancestorEventIds = null)
            {
                Work = work;
                PersistenceSequence = persistenceSequence;
                AncestorEventIds = ancestorEventIds == null ? Array.Empty<string>() : new List<string>(ancestorEventIds);
            }

            public SettlementEventWork Work { get; }
            public EventData Event => Work.Event;
            public int PersistenceSequence { get; }
            public IReadOnlyCollection<string> AncestorEventIds { get; }
        }

        private static IReadOnlyList<SettlementEventWork> ToWorkItems(IReadOnlyList<EventData> events, IReadOnlyList<SettlementEventChainOccurrence> restoredOccurrences)
        {
            if (events == null) return null;
            var works = new List<SettlementEventWork>(events.Count);
            bool hasOccurrences = restoredOccurrences != null && restoredOccurrences.Count == events.Count;
            for (int index = 0; index < events.Count; index++)
            {
                SettlementEventChainOccurrence occurrence = hasOccurrences ? restoredOccurrences[index] : null;
                works.Add(new SettlementEventWork(events[index], null, occurrence));
            }
            return works;
        }
    }
}
