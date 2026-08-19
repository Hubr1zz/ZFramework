using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;
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
        private readonly IHuntEventInput eventInput;
        private readonly Guid sessionId;
        private readonly string chainId;
        private readonly ActionEventOutbox eventOutbox;
        private readonly Queue<EventData> pendingEvents = new();
        private readonly Func<EventData, IReactorEntity> resolveEventEntity;
        private ResolveSettlementEventEntryAction currentEntry;
        private CampaignEncounterRequest encounterRequest;
        private string failureReason;
        private int resolvedCount;

        public ResolveSettlementEventChainAction(EventSystem eventSystem, IHuntEventInput eventInput, IReadOnlyList<EventData> events, Guid sessionId, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target, Func<EventData, IReactorEntity> resolveEventEntity)
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
                if (gameEvent != null)
                    pendingEvents.Enqueue(gameEvent);
        }

        public SettlementEventCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        public void Append(EventData gameEvent)
        {
            if (gameEvent != null)
                pendingEvents.Enqueue(gameEvent);
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
                    if (chainedEvent != null)
                        pendingEvents.Enqueue(chainedEvent);
                currentEntry = null;
            }

            if (pendingEvents.Count == 0) return null;
            EventData nextEvent = pendingEvents.Dequeue();
            currentEntry = new ResolveSettlementEventEntryAction(eventSystem, eventInput, nextEvent, eventSystem.Settlement.GetAvailableHunters(), chainId, resolvedCount, eventOutbox, Source, resolveEventEntity(nextEvent));
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
    }

    /// <summary>一个已展示事件节点的提交边界；提交后立刻发布检查点，确保取消后已发生的节点仍会保存。</summary>
    public sealed class ResolveSettlementEventEntryAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly EventSystem eventSystem;
        private readonly IHuntEventInput eventInput;
        private readonly EventData gameEvent;
        private readonly IReadOnlyList<HunterInstance> hunters;
        private readonly string transactionId;
        private readonly ActionEventOutbox eventOutbox;

        public ResolveSettlementEventEntryAction(EventSystem eventSystem, IHuntEventInput eventInput, EventData gameEvent, IReadOnlyList<HunterInstance> hunters, string chainId, int sequence, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.eventSystem = eventSystem ?? throw new ArgumentNullException(nameof(eventSystem));
            this.eventInput = eventInput;
            this.gameEvent = gameEvent ?? throw new ArgumentNullException(nameof(gameEvent));
            this.hunters = hunters ?? Array.Empty<HunterInstance>();
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            transactionId = $"settlement-event:{chainId}:{sequence}:{gameEvent.name}";
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public IReadOnlyList<EventData> ChainedEvents { get; private set; } = Array.Empty<EventData>();
        public IReadOnlyList<string> EncounterIds { get; private set; } = Array.Empty<string>();
        public string EventId => gameEvent.name;
        public EventData GameEvent => gameEvent;
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            eventOutbox.Stage(new GameEventTriggeredEvent { EventId = gameEvent.name });
            eventOutbox.PublishCheckpoint();
            if (gameEvent.eventType != GameEventType.Choice || gameEvent.options == null || gameEvent.options.Count == 0)
            {
                if (eventInput != null)
                    await eventInput.ConfirmNarrativeAsync(gameEvent, null, cancellationToken);
                PlayableEventNodeCommitResult narrativeResult = eventSystem.ResolveNarrativeNodeStandalone(gameEvent);
                ChainedEvents = narrativeResult.ChainedEvents;
                EncounterIds = narrativeResult.EncounterIds;
                PublishCommitCheckpoint();
                return ActionOutcome.Success();
            }

            HuntEventChoiceSelection selection = eventInput != null
                ? await eventInput.SelectChoiceAsync(gameEvent, null, hunters, cancellationToken)
                : FindAutomaticSelection();
            PlayableEventChoiceTransaction transaction = selection.IsValid ? eventSystem.PrepareChoice(gameEvent, selection.OptionIndex, selection.Actor) : null;
            if (transaction == null)
            {
                selection = FindAutomaticSelection();
                transaction = selection.IsValid ? eventSystem.PrepareChoice(gameEvent, selection.OptionIndex, selection.Actor) : null;
            }
            if (transaction == null)
            {
                PlayableEventNodeCommitResult fallbackResult = eventSystem.ResolveNarrativeNodeStandalone(gameEvent);
                ChainedEvents = fallbackResult.ChainedEvents;
                EncounterIds = fallbackResult.EncounterIds;
                PublishCommitCheckpoint();
                return ActionOutcome.Success();
            }

            while (transaction.RequiresCheck && eventInput != null)
            {
                HuntEventCheckDecision decision = await eventInput.PresentCheckAsync(transaction, cancellationToken);
                if (decision != HuntEventCheckDecision.Reroll) break;
                if (!transaction.TryReroll()) break;
                eventOutbox.Stage(new SettlementTransactionCommittedEvent
                {
                    TransactionId = $"{transactionId}:reroll",
                    Kind = SettlementTransactionKind.EventReroll
                });
                eventOutbox.PublishCheckpoint();
            }
            PlayableEventCommitResult result = transaction.CommitStandalone(true);
            ChainedEvents = result.ChainedEvents;
            EncounterIds = result.EncounterIds;
            PublishCommitCheckpoint();
            if (eventInput != null)
                await eventInput.ConfirmResultAsync(gameEvent, result.Result, cancellationToken);
            return ActionOutcome.Success();
        }

        private HuntEventChoiceSelection FindAutomaticSelection()
        {
            for (int optionIndex = 0; optionIndex < gameEvent.options.Count; optionIndex++)
            {
                EventOption option = gameEvent.options[optionIndex];
                if (PlayableEventOptionAvailability.CanUse(option, null, eventSystem.Settlement, out _))
                    return new HuntEventChoiceSelection(optionIndex, null);
                foreach (HunterInstance hunter in hunters)
                    if (PlayableEventOptionAvailability.CanUse(option, hunter, eventSystem.Settlement, out _))
                        return new HuntEventChoiceSelection(optionIndex, hunter);
            }
            return new HuntEventChoiceSelection(-1, null);
        }

        private void PublishCommitCheckpoint()
        {
            eventOutbox.Stage(new SettlementTransactionCommittedEvent
            {
                TransactionId = transactionId,
                Kind = SettlementTransactionKind.EventResolution
            });
            eventOutbox.PublishCheckpoint();
        }
    }
}
