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
        private ResolvePlayableEventNodeAction currentEntry;
        private PendingEventWork currentWork;
        private IReadOnlyList<SettlementEventChainOccurrence> lastCommittedChildren = Array.Empty<SettlementEventChainOccurrence>();
        private string lastCommitDiagnostic = string.Empty;
        private int nextRootSequence = -1;
        private CampaignEncounterRequest encounterRequest;
        private string failureReason;
        private int resolvedCount;
        private readonly List<PlayableEventEffectResult> effectResults = new();

        public ResolveSettlementEventChainAction(EventSystem eventSystem, IPlayableEventInput eventInput, IReadOnlyList<EventData> events, Guid sessionId, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target, Func<EventData, IReactorEntity> resolveEventEntity, ITabletopRandomInteractionPresenter randomInteractionPresenter = null, string restoredChainId = null, IReadOnlyList<SettlementEventChainOccurrence> restoredOccurrences = null)
        {
            this.eventSystem = eventSystem ?? throw new ArgumentNullException(nameof(eventSystem));
            this.eventInput = eventInput;
            this.sessionId = sessionId;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            this.resolveEventEntity = resolveEventEntity ?? throw new ArgumentNullException(nameof(resolveEventEntity));
            this.randomInteractionPresenter = randomInteractionPresenter;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            chainId = string.IsNullOrWhiteSpace(restoredChainId) ? Guid.NewGuid().ToString("N") : restoredChainId.Trim();
            if (events == null) return;
            if (restoredOccurrences != null && restoredOccurrences.Count == events.Count)
                for (int index = 0; index < events.Count; index++)
                {
                    SettlementEventChainOccurrence occurrence = restoredOccurrences[index];
                    TryEnqueue(events[index], occurrence.Sequence);
                }
            else
                foreach (EventData gameEvent in events)
                    TryEnqueue(gameEvent, nextRootSequence--);
        }

        public SettlementEventCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        public void Append(EventData gameEvent)
        {
            TryEnqueue(gameEvent, nextRootSequence--);
        }

        protected override GameAction GetNextChild(CompositeExecutionContext context)
        {
            if (currentEntry != null)
            {
                if (!context.LastOutcome.IsSuccess)
                {
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

                var childAncestors = new HashSet<EventData>(currentWork.Ancestors) { currentEntry.GameEvent };
                EnqueuePersistedChildren(currentEntry.ChainedEvents, childAncestors);
                currentEntry = null;
            }

            if (pendingEvents.Count == 0) return null;
            PendingEventWork nextWork = pendingEvents.Dequeue();
            EventData nextEvent = nextWork.Event;
            currentWork = nextWork;
            currentEntry = new ResolvePlayableEventNodeAction(eventSystem, eventInput, nextEvent, null, eventSystem.Settlement.GetAvailableHunters(), eventOutbox, checkpoint => StageCommitCheckpoint(checkpoint, nextWork), Source, resolveEventEntity(nextEvent), randomInteractionPresenter);
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
            lastCommittedChildren = Array.Empty<SettlementEventChainOccurrence>();
            lastCommitDiagnostic = string.Empty;
            if (checkpoint.Kind == PlayableEventCommitKind.Resolution)
            {
                var chainedEventIds = new List<string>();
                foreach (string chainedEventId in checkpoint.ChainedEventIds)
                {
                    bool cycle = string.Equals(chainedEventId, checkpoint.EventId, StringComparison.Ordinal);
                    if (!cycle)
                        foreach (EventData ancestor in work.Ancestors)
                            if (ancestor != null && string.Equals(ancestor.name, chainedEventId, StringComparison.Ordinal))
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
                lastCommittedChildren = eventSystem.Settlement.CommitEventChainOccurrence(chainId, work.PersistenceSequence, chainedEventIds, eventSystem.Settlement.CurrentYear, checkpoint.ActorId);
                lastCommitDiagnostic = eventSystem.Settlement.GetEventChainDiagnostic(chainId);
            }
            eventOutbox.Stage(new SettlementTransactionCommittedEvent
            {
                TransactionId = checkpoint.Kind == PlayableEventCommitKind.Reroll ? $"settlement-event:{chainId}:{(work.PersistenceSequence >= 0 ? work.PersistenceSequence : resolvedCount)}:{checkpoint.EventId}:reroll" : $"settlement-event:{chainId}:{(work.PersistenceSequence >= 0 ? work.PersistenceSequence : resolvedCount)}:{checkpoint.EventId}",
                Kind = checkpoint.Kind == PlayableEventCommitKind.Reroll ? SettlementTransactionKind.EventReroll : SettlementTransactionKind.EventResolution
            });
        }

        private void EnqueuePersistedChildren(IReadOnlyList<EventData> chainedEvents, IReadOnlyCollection<EventData> ancestors)
        {
            if (chainedEvents == null) return;
            int occurrenceIndex = 0;
            foreach (EventData chainedEvent in chainedEvents)
            {
                if (chainedEvent == null) continue;
                if (ContainsAncestor(ancestors, chainedEvent))
                {
                    eventOutbox.Stage(new PlayableEventDuplicatePreventedEvent { EventId = chainedEvent.name });
                    continue;
                }
                if (occurrenceIndex >= lastCommittedChildren.Count) break;
                SettlementEventChainOccurrence occurrence = lastCommittedChildren[occurrenceIndex];
                string eventId = chainedEvent.name?.Trim() ?? string.Empty;
                if (!string.Equals(eventId, occurrence.EventId, StringComparison.Ordinal)) continue;
                TryEnqueue(chainedEvent, occurrence.Sequence, ancestors);
                occurrenceIndex++;
            }
            lastCommittedChildren = Array.Empty<SettlementEventChainOccurrence>();
        }

        private void TryEnqueue(EventData gameEvent, int persistenceSequence = -1, IReadOnlyCollection<EventData> ancestors = null)
        {
            if (gameEvent == null) return;
            if (ancestors != null && ContainsAncestor(ancestors, gameEvent))
            {
                eventOutbox.Stage(new PlayableEventDuplicatePreventedEvent { EventId = gameEvent.name });
                return;
            }

            bool scheduled = persistenceSequence > 0
                ? chainGuard.TrySchedule(gameEvent, $"{chainId}:{persistenceSequence}")
                : chainGuard.TrySchedule(gameEvent);
            if (scheduled)
            {
                pendingEvents.Enqueue(new PendingEventWork(gameEvent, persistenceSequence, ancestors));
                return;
            }
            eventOutbox.Stage(new PlayableEventDuplicatePreventedEvent { EventId = gameEvent.name });
        }

        private static bool ContainsAncestor(IReadOnlyCollection<EventData> ancestors, EventData gameEvent)
        {
            foreach (EventData ancestor in ancestors)
                if (ReferenceEquals(ancestor, gameEvent)) return true;
            return false;
        }

        private readonly struct PendingEventWork
        {
            public PendingEventWork(EventData gameEvent, int persistenceSequence, IReadOnlyCollection<EventData> ancestors = null)
            {
                Event = gameEvent;
                PersistenceSequence = persistenceSequence;
                Ancestors = ancestors == null ? new HashSet<EventData>() : new HashSet<EventData>(ancestors);
            }

            public EventData Event { get; }
            public int PersistenceSequence { get; }
            public IReadOnlyCollection<EventData> Ancestors { get; }
        }
    }
}
