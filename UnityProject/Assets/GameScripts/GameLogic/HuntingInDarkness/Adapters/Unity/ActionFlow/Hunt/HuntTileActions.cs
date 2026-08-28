using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.Data;
using UnityEngine;

namespace HuntingInDarkness.ActionFlow.Hunt
{
    public readonly struct HuntTileCommandResult
    {
        private HuntTileCommandResult(bool succeeded, string reason, HuntTileInteractionCommit commit, PlayableHuntNoiseResolution noiseResolution, PlayableEventEffectBatchResult effectResults, bool encounterRequested = false)
        {
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
            Commit = commit;
            NoiseResolution = noiseResolution;
            EffectResults = effectResults;
            EncounterRequested = encounterRequested;
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public HuntTileInteractionCommit Commit { get; }
        public PlayableHuntNoiseResolution NoiseResolution { get; }
        public PlayableEventEffectBatchResult EffectResults { get; }
        public bool EncounterRequested { get; }
        public int FailedEffectCount => EffectResults.FailedCount;
        public static HuntTileCommandResult Success(HuntTileInteractionCommit commit) => Success(commit, default, PlayableEventEffectBatchResult.Empty);
        public static HuntTileCommandResult Success(HuntTileInteractionCommit commit, PlayableHuntNoiseResolution noiseResolution, PlayableEventEffectBatchResult effectResults, bool encounterRequested = false) => new(true, string.Empty, commit, noiseResolution, effectResults, encounterRequested);
        public static HuntTileCommandResult Failed(string reason) => new(false, reason, default, default, PlayableEventEffectBatchResult.Empty);
        public static HuntTileCommandResult Failed(string reason, HuntTileInteractionCommit commit, PlayableHuntNoiseResolution noiseResolution, PlayableEventEffectBatchResult effectResults) => new(false, reason, commit, noiseResolution, effectResults);
    }

    public struct HuntTileInteractionCommittedEvent
    {
        public Vector2Int Coordinate;
        public HuntTileInteractionKind Kind;
        public int ResourcePointCount;
        public bool BossEncounter;
    }

    public struct HuntEventNodeCommittedEvent
    {
        public Vector2Int Coordinate;
        public string EventId;
        public int ActorId;
        public PlayableEventCommitKind Kind;
    }

    public struct HuntEventChainTruncatedEvent
    {
        public string SourceEventId;
        public int PreventedChildCount;
        public string Reason;
    }

    /// <summary>一次地图点击的完整因果链：权威状态提交后，再进入可覆盖的地块事件窗口。</summary>
    public sealed class InteractHuntTileAction : CompositeGameAction, ISourceAction, ITargetAction
    {
        private readonly HuntManager manager;
        private readonly Vector2Int coordinate;
        private readonly HuntTileInteractionKind intendedKind;
        private readonly ActionEventOutbox eventOutbox;
        private readonly HuntEncounterAccumulator encounterAccumulator;
        private readonly Func<HuntingInDarkness.Data.EventData, IReactorEntity> resolveEventEntity;
        private readonly ITabletopRandomInteractionPresenter randomInteractionPresenter;
        private readonly IHuntTileInteractionPresenter tileInteractionPresenter;
        private readonly PlayableHuntEventOccurrenceStore occurrenceStore;
        private readonly Action lockEncounterHandoff;
        private readonly IPlayableEventFatalInjuryCommand fatalInjuryCommand;
        private readonly Guid huntSessionId;
        private readonly string destinationId;
        private PrepareHuntNoiseAction noiseAction;
        private CommitHuntTileInteractionAction commitAction;
        private ResolveHuntTileEventAction eventAction;
        private bool eventScheduled;
        private bool finalizeScheduled;
        private bool noiseScheduled;
        private bool eventFailed;
        private string eventFailureReason;

        public InteractHuntTileAction(HuntManager manager, Vector2Int coordinate, HuntTileInteractionKind intendedKind, Guid huntSessionId, string defaultEncounterId, string destinationId, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target, Func<HuntingInDarkness.Data.EventData, IReactorEntity> resolveEventEntity, ITabletopRandomInteractionPresenter randomInteractionPresenter = null, IHuntTileInteractionPresenter tileInteractionPresenter = null, PlayableHuntEventOccurrenceStore occurrenceStore = null, Action lockEncounterHandoff = null, IPlayableEventFatalInjuryCommand fatalInjuryCommand = null)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.coordinate = coordinate;
            this.intendedKind = intendedKind;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            this.resolveEventEntity = resolveEventEntity ?? throw new ArgumentNullException(nameof(resolveEventEntity));
            this.randomInteractionPresenter = randomInteractionPresenter;
            this.tileInteractionPresenter = tileInteractionPresenter;
            this.occurrenceStore = occurrenceStore ?? new PlayableHuntEventOccurrenceStore();
            this.lockEncounterHandoff = lockEncounterHandoff;
            this.fatalInjuryCommand = fatalInjuryCommand;
            this.huntSessionId = huntSessionId;
            this.destinationId = destinationId ?? string.Empty;
            encounterAccumulator = new HuntEncounterAccumulator(huntSessionId, defaultEncounterId, destinationId);
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public HuntTileCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override GameAction GetNextChild(CompositeExecutionContext context)
        {
            if (!noiseScheduled)
            {
                noiseScheduled = true;
                noiseAction = new PrepareHuntNoiseAction(manager, coordinate, intendedKind, huntSessionId, destinationId, randomInteractionPresenter, Source, Target);
                return noiseAction;
            }
            if (commitAction == null)
            {
                if (!context.LastOutcome.IsSuccess) return null;
                commitAction = new CommitHuntTileInteractionAction(manager, coordinate, intendedKind, noiseAction.Resolution, eventOutbox, encounterAccumulator, tileInteractionPresenter, Source, Target);
                return commitAction;
            }
            if (!commitAction.IsCommitted) return null;
            if (!eventScheduled)
            {
                eventScheduled = true;
                eventAction = new ResolveHuntTileEventAction(manager, commitAction.Commit, noiseAction.Resolution, eventOutbox, encounterAccumulator, Source, Target, resolveEventEntity, randomInteractionPresenter, occurrenceStore, lockEncounterHandoff: lockEncounterHandoff, fatalInjuryCommand: fatalInjuryCommand);
                return eventAction;
            }
            if (!finalizeScheduled && eventAction != null && !context.LastOutcome.IsSuccess)
            {
                eventFailed = true;
                eventFailureReason = context.LastOutcome.Reason;
            }
            if (!finalizeScheduled)
            {
                finalizeScheduled = true;
                return new FinalizeHuntTileInteractionAction(manager, commitAction.Commit, Source, Target);
            }
            return null;
        }

        protected override ActionOutcome Resolve(CompositeExecutionContext context)
        {
            if (commitAction == null || !commitAction.IsCommitted)
            {
                string reason = string.IsNullOrWhiteSpace(context.LastOutcome.Reason) ? "地块状态已变化，操作未执行" : context.LastOutcome.Reason;
                Result = HuntTileCommandResult.Failed(reason);
                return ActionOutcome.Failure(reason);
            }
            if (eventFailed)
            {
                string reason = string.IsNullOrWhiteSpace(eventFailureReason) ? "地块事件未完成" : eventFailureReason;
                Result = HuntTileCommandResult.Failed(reason, commitAction.Commit, noiseAction?.Resolution ?? default, eventAction?.EffectResults ?? PlayableEventEffectBatchResult.Empty);
                return ActionOutcome.Failure(reason);
            }
            Result = HuntTileCommandResult.Success(commitAction.Commit, noiseAction?.Resolution ?? default, eventAction?.EffectResults ?? PlayableEventEffectBatchResult.Empty, eventAction?.EncounterRequested == true || encounterAccumulator.HasRequest);
            if (encounterAccumulator.HasRequest)
            {
                lockEncounterHandoff?.Invoke();
                eventOutbox.StageAfterCommit(new CampaignEncounterRequestedEvent { Request = encounterAccumulator.Request });
            }
            return ActionOutcome.Success();
        }
    }

    public sealed class CommitHuntTileInteractionAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly HuntManager manager;
        private readonly Vector2Int coordinate;
        private readonly HuntTileInteractionKind intendedKind;
        private readonly PlayableHuntNoiseResolution noiseResolution;
        private readonly ActionEventOutbox eventOutbox;
        private readonly HuntEncounterAccumulator encounterAccumulator;
        private readonly IHuntTileInteractionPresenter tileInteractionPresenter;

        internal CommitHuntTileInteractionAction(HuntManager manager, Vector2Int coordinate, HuntTileInteractionKind intendedKind, PlayableHuntNoiseResolution noiseResolution, ActionEventOutbox eventOutbox, HuntEncounterAccumulator encounterAccumulator, IHuntTileInteractionPresenter tileInteractionPresenter, IReactorEntity source, IReactorEntity target)
        {
            this.manager = manager;
            this.coordinate = coordinate;
            this.intendedKind = intendedKind;
            this.noiseResolution = noiseResolution;
            this.eventOutbox = eventOutbox;
            this.encounterAccumulator = encounterAccumulator;
            this.tileInteractionPresenter = tileInteractionPresenter;
            Source = source;
            Target = target;
        }

        public bool IsCommitted => Commit.IsCommitted;
        public HuntTileInteractionCommit Commit { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }
        public override ReactionPhases OpenReactionPhases => ReactionPhases.BeforeExecution | ReactionPhases.AfterResolved;

        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            if (intendedKind == HuntTileInteractionKind.None) return ActionOutcome.Failure("该地块当前不可交互");
            if (!manager.TryCommitTileInteraction(coordinate, intendedKind, out HuntTileInteractionCommit commit)) return ActionOutcome.Failure("地块状态已变化，操作未执行");
            Commit = commit;
            if (commit.BossEncounter)
                encounterAccumulator.TryAdd(commit.Tile.Config?.bossEncounterId, CampaignEncounterSourceKind.HuntBossTile, commit.Coordinate, commit.Tile.ConfigName);
            eventOutbox.Stage(new HuntTileInteractionCommittedEvent
            {
                Coordinate = commit.Coordinate,
                Kind = commit.Kind,
                ResourcePointCount = commit.Tile.ResourcePoints.Count,
                BossEncounter = commit.BossEncounter
            });
            if (noiseResolution.IsResolved)
            {
                manager.CommitNoiseResolution(noiseResolution);
                eventOutbox.Stage(new HuntNoiseResolvedEvent
                {
                    InteractionId = noiseResolution.InteractionId,
                    DestinationId = noiseResolution.DestinationId,
                    Coordinate = coordinate,
                    NoiseScore = noiseResolution.Plan.NoiseScore,
                    DangerCardCount = noiseResolution.Plan.DangerCardCount,
                    DeckSize = noiseResolution.Plan.DeckSize,
                    IsDanger = noiseResolution.IsDanger,
                    EventId = noiseResolution.EventId
                });
            }
            if (commit.Kind == HuntTileInteractionKind.Reveal)
                eventOutbox.Stage(new GameEventTriggeredEvent { EventId = $"tile_reveal:{coordinate.x},{coordinate.y}" });
            eventOutbox.PublishCheckpoint();
            if (tileInteractionPresenter == null) return ActionOutcome.Success();
            try
            {
                var request = new HuntTileInteractionPresentationRequest(commit.Coordinate, commit.Kind);
                await context.AwaitPresentationAsync(tileInteractionPresenter.PresentAsync(request, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            return ActionOutcome.Success();
        }
    }

    /// <summary>地块内容结算边界；后续可由事件表 Action 工厂替换或由 Reactor 注入前后效果。</summary>
    public sealed class ResolveHuntTileEventAction : CompositeGameAction, ISourceAction, ITargetAction
    {
        private readonly HuntManager manager;
        private readonly HuntTileInteractionCommit commit;
        private readonly PlayableHuntNoiseResolution noiseResolution;
        private readonly ActionEventOutbox eventOutbox;
        private readonly HuntEncounterAccumulator encounterAccumulator;
        private readonly Func<HuntingInDarkness.Data.EventData, IReactorEntity> resolveEventEntity;
        private readonly ITabletopRandomInteractionPresenter randomInteractionPresenter;
        private readonly PlayableHuntEventOccurrenceStore occurrenceStore;
        private readonly IPlayableEventWorldCommand worldCommand;
        private readonly Queue<PlayableHuntEventOccurrence> pendingOccurrences = new();
        private SelectHuntTileEventAction selectAction;
        private ResolvePlayableEventNodeAction currentEntry;
        private PlayableHuntEventOccurrence currentOccurrence;
        private bool selectionCollected;
        private bool selectionScheduled;
        private bool failure;
        private string failureReason;
        public bool HasCommittedCheckpoint { get; private set; }
        private readonly bool stageEncounterAfterCommit;
        private readonly Action lockEncounterHandoff;
        private readonly IPlayableEventFatalInjuryCommand fatalInjuryCommand;
        private readonly List<PlayableEventEffectResult> effectResults = new();

        internal ResolveHuntTileEventAction(HuntManager manager, HuntTileInteractionCommit commit, PlayableHuntNoiseResolution noiseResolution, ActionEventOutbox eventOutbox, HuntEncounterAccumulator encounterAccumulator, IReactorEntity source, IReactorEntity target, Func<HuntingInDarkness.Data.EventData, IReactorEntity> resolveEventEntity, ITabletopRandomInteractionPresenter randomInteractionPresenter = null, PlayableHuntEventOccurrenceStore occurrenceStore = null, PlayableHuntEventOccurrence initialOccurrence = null, bool stageEncounterAfterCommit = false, Action lockEncounterHandoff = null, IPlayableEventFatalInjuryCommand fatalInjuryCommand = null)
        {
            this.manager = manager;
            this.commit = commit;
            this.noiseResolution = noiseResolution;
            this.eventOutbox = eventOutbox;
            this.encounterAccumulator = encounterAccumulator;
            this.resolveEventEntity = resolveEventEntity;
            this.randomInteractionPresenter = randomInteractionPresenter;
            this.occurrenceStore = occurrenceStore ?? new PlayableHuntEventOccurrenceStore();
            worldCommand = new HuntTileEventWorldCommand(manager, commit);
            this.stageEncounterAfterCommit = stageEncounterAfterCommit;
            this.lockEncounterHandoff = lockEncounterHandoff;
            this.fatalInjuryCommand = fatalInjuryCommand;
            Source = source;
            Target = target;
            if (initialOccurrence != null)
            {
                selectionScheduled = true;
                pendingOccurrences.Enqueue(initialOccurrence);
            }
        }

        public HuntTileInteractionCommit Commit => commit;
        public PlayableEventEffectBatchResult EffectResults => new(effectResults);
        public bool EncounterRequested => encounterAccumulator.HasRequest;
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }
        protected override GameAction GetNextChild(CompositeExecutionContext context)
        {
            if (!selectionScheduled && pendingOccurrences.Count == 0)
            {
                selectionScheduled = true;
                selectAction = new SelectHuntTileEventAction(manager, commit, noiseResolution, Source, Target);
                return selectAction;
            }
            if (selectAction != null && !selectionCollected)
            {
                selectionCollected = true;
                if (!occurrenceStore.TryScheduleRoot(selectAction.SelectedEvent, commit.Coordinate, manager.CurrentYear, manager.EnsureSelectedHunterAvailable()?.InstanceId ?? 0, out PlayableHuntEventOccurrence rootOccurrence))
                {
                    if (selectAction.SelectedEvent != null)
                        eventOutbox.Stage(new PlayableEventDuplicatePreventedEvent { EventId = selectAction.SelectedEvent.ContentId });
                }
                else
                    pendingOccurrences.Enqueue(rootOccurrence);
            }
            else if (currentEntry != null)
            {
                if (!context.LastOutcome.IsSuccess)
                {
                    failure = true;
                    failureReason = context.LastOutcome.Reason;
                    currentEntry = null;
                    return null;
                }
                effectResults.AddRange(currentEntry.EffectResults.Effects);
                foreach (string encounterId in currentEntry.EncounterIds)
                    encounterAccumulator.TryAdd(encounterId, CampaignEncounterSourceKind.HuntEvent, commit.Coordinate, currentEntry.EventId);
                if (encounterAccumulator.HasRequest)
                {
                    pendingOccurrences.Clear();
                    currentEntry = null;
                    return null;
                }
                currentEntry = null;
            }
            if (failure || pendingOccurrences.Count == 0) return null;
            currentOccurrence = pendingOccurrences.Dequeue();
            HuntingInDarkness.Data.EventData nextEvent = currentOccurrence.Event;
            HunterInstance occurrenceActor = ResolveOccurrenceActor(currentOccurrence.Occurrence.ActorId);
            if (currentOccurrence.Occurrence.ActorId > 0 && occurrenceActor == null)
            {
                failure = true;
                failureReason = "狩猎事件执行猎人已经失效，待处理事件保留。";
                return null;
            }
            currentEntry = new ResolvePlayableEventNodeAction(manager.EventSystem, manager.EventInput, nextEvent, occurrenceActor, manager.ActiveHunters, eventOutbox, StageCommitCheckpoint, Source, resolveEventEntity(nextEvent), randomInteractionPresenter, manager.EventResourceCommand, worldCommand, itemCommand: manager.EventItemCommand, populationCommand: manager.EventPopulationCommand, rerollCheckpoint: currentOccurrence.Occurrence.RerollCheckpoint, fatalInjuryCommand: fatalInjuryCommand);
            return currentEntry;
        }

        protected override ActionOutcome Resolve(CompositeExecutionContext context)
        {
            if (!failure)
            {
                if (stageEncounterAfterCommit && encounterAccumulator.HasRequest)
                {
                    lockEncounterHandoff?.Invoke();
                    eventOutbox.StageAfterCommit(new CampaignEncounterRequestedEvent { Request = encounterAccumulator.Request });
                }
                return ActionOutcome.Success();
            }
            string reason = string.IsNullOrWhiteSpace(failureReason) ? "狩猎事件未完成" : failureReason;
            return ActionOutcome.Failure(reason);
        }

        private void StageCommitCheckpoint(PlayableEventCommitCheckpoint checkpoint)
        {
            HasCommittedCheckpoint = true;
            manager.EnsureSelectedHunterAvailable();
            if (checkpoint.Kind == PlayableEventCommitKind.Reroll && !occurrenceStore.TrySetRerollCheckpoint(currentOccurrence, checkpoint.RerollCheckpoint, out failureReason))
            {
                failure = true;
                return;
            }
            if (checkpoint.Kind == PlayableEventCommitKind.Resolution)
            {
                PlayableHuntEventOccurrenceCommitResult commitResult = occurrenceStore.Commit(currentOccurrence, checkpoint.ChainedEvents, manager.CurrentYear, checkpoint.ActorId);
                foreach (string preventedEventId in commitResult.PreventedEventIds)
                    eventOutbox.Stage(new PlayableEventDuplicatePreventedEvent { EventId = preventedEventId });
                if (!commitResult.Succeeded)
                {
                    failure = true;
                    failureReason = commitResult.Diagnostic;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(commitResult.Diagnostic))
                        eventOutbox.Stage(new HuntEventChainTruncatedEvent { SourceEventId = checkpoint.EventId, PreventedChildCount = commitResult.TruncatedChildCount, Reason = commitResult.Diagnostic });
                    if (!encounterAccumulator.HasRequest)
                        foreach (PlayableHuntEventOccurrence child in commitResult.AppendedOccurrences)
                            pendingOccurrences.Enqueue(child);
                }
            }
            if (failure) return;
            eventOutbox.Stage(new HuntEventNodeCommittedEvent
            {
                Coordinate = commit.Coordinate,
                EventId = checkpoint.EventId,
                ActorId = checkpoint.ActorId,
                Kind = checkpoint.Kind
            });
        }

        private HunterInstance ResolveOccurrenceActor(int actorId)
        {
            if (actorId > 0)
            {
                foreach (HunterInstance hunter in manager.ActiveHunters)
                    if (hunter != null && hunter.InstanceId == actorId && hunter.IsAlive)
                        return hunter;
                return null;
            }
            return manager.EnsureSelectedHunterAvailable();
        }
    }

    public sealed class SelectHuntTileEventAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly HuntManager manager;
        private readonly HuntTileInteractionCommit commit;
        private readonly PlayableHuntNoiseResolution noiseResolution;

        internal SelectHuntTileEventAction(HuntManager manager, HuntTileInteractionCommit commit, PlayableHuntNoiseResolution noiseResolution, IReactorEntity source, IReactorEntity target)
        {
            this.manager = manager;
            this.commit = commit;
            this.noiseResolution = noiseResolution;
            Source = source;
            Target = target;
        }

        public HuntingInDarkness.Data.EventData SelectedEvent { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            SelectedEvent = manager.SelectTileInteractionEvent(commit, noiseResolution);
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }

    public sealed class FinalizeHuntTileInteractionAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly HuntManager manager;
        private readonly HuntTileInteractionCommit commit;

        internal FinalizeHuntTileInteractionAction(HuntManager manager, HuntTileInteractionCommit commit, IReactorEntity source, IReactorEntity target)
        {
            this.manager = manager;
            this.commit = commit;
            Source = source;
            Target = target;
        }

        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            manager.FinalizeTileInteraction(commit);
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }
}
