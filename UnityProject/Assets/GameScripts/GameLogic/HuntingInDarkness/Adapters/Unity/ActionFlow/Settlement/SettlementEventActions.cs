using System;
using System.Collections.Generic;
using CardGame.ActionQueue;
using Core;
using GameplayBase;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace HuntingInDarkness.ActionFlow.Settlement
{
    public readonly struct SettlementEventCommandResult
    {
        private SettlementEventCommandResult(bool succeeded, string reason, int resolvedCount, bool encounterRequested)
        {
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
            ResolvedCount = resolvedCount;
            EncounterRequested = encounterRequested;
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public int ResolvedCount { get; }
        public bool EncounterRequested { get; }
        public static SettlementEventCommandResult Success(int resolvedCount, bool encounterRequested) => new(true, string.Empty, resolvedCount, encounterRequested);
        public static SettlementEventCommandResult Failed(string reason, int resolvedCount) => new(false, reason, resolvedCount, false);
    }

    /// <summary>营地事件列表的唯一流程根；节点、子链与跨环境交接均在同一 Action 因果链中执行。</summary>
    public sealed class ResolveSettlementEventChainAction : CompositeGameAction, ISourceAction, ITargetAction
    {
        private readonly EventSystem eventSystem;
        private readonly IPlayableEventInput eventInput;
        private readonly Guid sessionId;
        private readonly string chainId;
        private readonly ActionEventOutbox eventOutbox;
        private readonly Queue<EventData> pendingEvents = new();
        private readonly PlayableEventChainGuard chainGuard = new();
        private readonly Func<EventData, IReactorEntity> resolveEventEntity;
        private ResolvePlayableEventNodeAction currentEntry;
        private CampaignEncounterRequest encounterRequest;
        private string failureReason;
        private int resolvedCount;

        public ResolveSettlementEventChainAction(EventSystem eventSystem, IPlayableEventInput eventInput, IReadOnlyList<EventData> events, Guid sessionId, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target, Func<EventData, IReactorEntity> resolveEventEntity)
        {
            this.eventSystem = eventSystem ?? throw new ArgumentNullException(nameof(eventSystem));
            this.eventInput = eventInput;
            this.sessionId = sessionId;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            this.resolveEventEntity = resolveEventEntity ?? throw new ArgumentNullException(nameof(resolveEventEntity));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            chainId = Guid.NewGuid().ToString("N");
            if (events == null) return;
            foreach (EventData gameEvent in events)
                TryEnqueue(gameEvent);
        }

        public SettlementEventCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        public void Append(EventData gameEvent)
        {
            TryEnqueue(gameEvent);
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
                if (currentEntry.EncounterIds.Count > 0)
                {
                    string encounterId = string.IsNullOrWhiteSpace(currentEntry.EncounterIds[0]) ? PlayableEncounterRuntime.DefaultEncounterId : currentEntry.EncounterIds[0];
                    encounterRequest = new CampaignEncounterRequest(sessionId, encounterId, CampaignEncounterSourceKind.SettlementEvent, GamePhase.Settlement, Vector2Int.zero, currentEntry.EventId, "settlement");
                    currentEntry = null;
                    return null;
                }

                foreach (EventData chainedEvent in currentEntry.ChainedEvents)
                    TryEnqueue(chainedEvent);
                currentEntry = null;
            }

            if (pendingEvents.Count == 0) return null;
            EventData nextEvent = pendingEvents.Dequeue();
            int sequence = resolvedCount;
            currentEntry = new ResolvePlayableEventNodeAction(eventSystem, eventInput, nextEvent, null, eventSystem.Settlement.GetAvailableHunters(), eventOutbox, checkpoint => StageCommitCheckpoint(checkpoint, sequence), Source, resolveEventEntity(nextEvent));
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
            Result = SettlementEventCommandResult.Success(resolvedCount, encounterRequested);
            if (encounterRequested)
                eventOutbox.StageAfterCommit(new CampaignEncounterRequestedEvent { Request = encounterRequest });
            return ActionOutcome.Success();
        }

        private void StageCommitCheckpoint(PlayableEventCommitCheckpoint checkpoint, int sequence)
        {
            eventOutbox.Stage(new SettlementTransactionCommittedEvent
            {
                TransactionId = checkpoint.Kind == PlayableEventCommitKind.Reroll ? $"settlement-event:{chainId}:{sequence}:{checkpoint.EventId}:reroll" : $"settlement-event:{chainId}:{sequence}:{checkpoint.EventId}",
                Kind = checkpoint.Kind == PlayableEventCommitKind.Reroll ? SettlementTransactionKind.EventReroll : SettlementTransactionKind.EventResolution
            });
        }

        private void TryEnqueue(EventData gameEvent)
        {
            if (gameEvent == null) return;
            if (chainGuard.TrySchedule(gameEvent))
            {
                pendingEvents.Enqueue(gameEvent);
                return;
            }
            eventOutbox.Stage(new PlayableEventDuplicatePreventedEvent { EventId = gameEvent.name });
        }
    }
}
