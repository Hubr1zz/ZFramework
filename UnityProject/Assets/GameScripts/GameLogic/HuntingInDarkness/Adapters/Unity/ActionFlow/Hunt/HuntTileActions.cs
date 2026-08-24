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
using UnityEngine;

namespace HuntingInDarkness.ActionFlow.Hunt
{
    public readonly struct HuntTileCommandResult
    {
        private HuntTileCommandResult(bool succeeded, string reason, HuntTileInteractionCommit commit, PlayableEventEffectBatchResult effectResults)
        {
            Succeeded = succeeded;
            Reason = reason;
            Commit = commit;
            EffectResults = effectResults;
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public HuntTileInteractionCommit Commit { get; }
        public PlayableEventEffectBatchResult EffectResults { get; }
        public int FailedEffectCount => EffectResults.FailedCount;
        public static HuntTileCommandResult Success(HuntTileInteractionCommit commit) => Success(commit, PlayableEventEffectBatchResult.Empty);
        public static HuntTileCommandResult Success(HuntTileInteractionCommit commit, PlayableEventEffectBatchResult effectResults) => new(true, string.Empty, commit, effectResults);
        public static HuntTileCommandResult Failed(string reason) => new(false, reason, default, PlayableEventEffectBatchResult.Empty);
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
        private CommitHuntTileInteractionAction commitAction;
        private ResolveHuntTileEventAction eventAction;
        private bool presentationScheduled;
        private bool eventScheduled;
        private bool finalizeScheduled;

        public InteractHuntTileAction(HuntManager manager, Vector2Int coordinate, HuntTileInteractionKind intendedKind, Guid huntSessionId, string defaultEncounterId, string destinationId, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target, Func<HuntingInDarkness.Data.EventData, IReactorEntity> resolveEventEntity, ITabletopRandomInteractionPresenter randomInteractionPresenter = null, IHuntTileInteractionPresenter tileInteractionPresenter = null)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.coordinate = coordinate;
            this.intendedKind = intendedKind;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            this.resolveEventEntity = resolveEventEntity ?? throw new ArgumentNullException(nameof(resolveEventEntity));
            this.randomInteractionPresenter = randomInteractionPresenter;
            this.tileInteractionPresenter = tileInteractionPresenter;
            encounterAccumulator = new HuntEncounterAccumulator(huntSessionId, defaultEncounterId, destinationId);
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public HuntTileCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override GameAction GetNextChild(CompositeExecutionContext context)
        {
            if (context.CompletedCount == 0)
            {
                commitAction = new CommitHuntTileInteractionAction(manager, coordinate, intendedKind, eventOutbox, encounterAccumulator, Source, Target);
                return commitAction;
            }
            if (!commitAction.IsCommitted) return null;
            if (!presentationScheduled)
            {
                presentationScheduled = true;
                return new PresentHuntTileInteractionAction(tileInteractionPresenter, new HuntTileInteractionPresentationRequest(commitAction.Commit.Coordinate, commitAction.Commit.Kind), Source, Target);
            }
            if (!eventScheduled)
            {
                eventScheduled = true;
                eventAction = new ResolveHuntTileEventAction(manager, commitAction.Commit, eventOutbox, encounterAccumulator, Source, Target, resolveEventEntity, randomInteractionPresenter);
                return eventAction;
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
            Result = HuntTileCommandResult.Success(commitAction.Commit, eventAction?.EffectResults ?? PlayableEventEffectBatchResult.Empty);
            if (encounterAccumulator.HasRequest)
                eventOutbox.StageAfterCommit(new CampaignEncounterRequestedEvent { Request = encounterAccumulator.Request });
            return ActionOutcome.Success();
        }
    }

    public sealed class CommitHuntTileInteractionAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly HuntManager manager;
        private readonly Vector2Int coordinate;
        private readonly HuntTileInteractionKind intendedKind;
        private readonly ActionEventOutbox eventOutbox;
        private readonly HuntEncounterAccumulator encounterAccumulator;

        internal CommitHuntTileInteractionAction(HuntManager manager, Vector2Int coordinate, HuntTileInteractionKind intendedKind, ActionEventOutbox eventOutbox, HuntEncounterAccumulator encounterAccumulator, IReactorEntity source, IReactorEntity target)
        {
            this.manager = manager;
            this.coordinate = coordinate;
            this.intendedKind = intendedKind;
            this.eventOutbox = eventOutbox;
            this.encounterAccumulator = encounterAccumulator;
            Source = source;
            Target = target;
        }

        public bool IsCommitted => Commit.IsCommitted;
        public HuntTileInteractionCommit Commit { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }
        public override ReactionPhases OpenReactionPhases => ReactionPhases.BeforeExecution | ReactionPhases.AfterResolved;

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            if (intendedKind == HuntTileInteractionKind.None) return UniTask.FromResult(ActionOutcome.Failure("该地块当前不可交互"));
            if (!manager.TryCommitTileInteraction(coordinate, intendedKind, out HuntTileInteractionCommit commit)) return UniTask.FromResult(ActionOutcome.Failure("地块状态已变化，操作未执行"));
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
            if (commit.Kind == HuntTileInteractionKind.Reveal)
                eventOutbox.Stage(new GameEventTriggeredEvent { EventId = $"tile_reveal:{coordinate.x},{coordinate.y}" });
            eventOutbox.PublishCheckpoint();
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }

    /// <summary>地块内容结算边界；后续可由事件表 Action 工厂替换或由 Reactor 注入前后效果。</summary>
    public sealed class ResolveHuntTileEventAction : CompositeGameAction, ISourceAction, ITargetAction
    {
        private readonly HuntManager manager;
        private readonly HuntTileInteractionCommit commit;
        private readonly ActionEventOutbox eventOutbox;
        private readonly HuntEncounterAccumulator encounterAccumulator;
        private readonly Func<HuntingInDarkness.Data.EventData, IReactorEntity> resolveEventEntity;
        private readonly ITabletopRandomInteractionPresenter randomInteractionPresenter;
        private readonly Queue<HuntingInDarkness.Data.EventData> pendingEvents = new();
        private readonly PlayableEventChainGuard chainGuard = new();
        private SelectHuntTileEventAction selectAction;
        private ResolvePlayableEventNodeAction currentEntry;
        private bool selectionCollected;
        private readonly List<PlayableEventEffectResult> effectResults = new();

        internal ResolveHuntTileEventAction(HuntManager manager, HuntTileInteractionCommit commit, ActionEventOutbox eventOutbox, HuntEncounterAccumulator encounterAccumulator, IReactorEntity source, IReactorEntity target, Func<HuntingInDarkness.Data.EventData, IReactorEntity> resolveEventEntity, ITabletopRandomInteractionPresenter randomInteractionPresenter = null)
        {
            this.manager = manager;
            this.commit = commit;
            this.eventOutbox = eventOutbox;
            this.encounterAccumulator = encounterAccumulator;
            this.resolveEventEntity = resolveEventEntity;
            this.randomInteractionPresenter = randomInteractionPresenter;
            Source = source;
            Target = target;
        }

        public HuntTileInteractionCommit Commit => commit;
        public PlayableEventEffectBatchResult EffectResults => new(effectResults);
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }
        protected override GameAction GetNextChild(CompositeExecutionContext context)
        {
            if (context.CompletedCount == 0)
            {
                selectAction = new SelectHuntTileEventAction(manager, commit, Source, Target);
                return selectAction;
            }
            if (!selectionCollected)
            {
                selectionCollected = true;
                TryEnqueue(selectAction.SelectedEvent);
            }
            else if (currentEntry != null)
            {
                effectResults.AddRange(currentEntry.EffectResults.Effects);
                foreach (string encounterId in currentEntry.EncounterIds)
                    encounterAccumulator.TryAdd(encounterId, CampaignEncounterSourceKind.HuntEvent, commit.Coordinate, currentEntry.EventId);
                if (encounterAccumulator.HasRequest)
                {
                    currentEntry = null;
                    return null;
                }
                foreach (HuntingInDarkness.Data.EventData chained in currentEntry.ChainedEvents)
                    TryEnqueue(chained);
                currentEntry = null;
            }
            if (pendingEvents.Count == 0) return null;
            HuntingInDarkness.Data.EventData nextEvent = pendingEvents.Dequeue();
            currentEntry = new ResolvePlayableEventNodeAction(manager.EventSystem, manager.EventInput, nextEvent, manager.SelectedHunter, manager.ActiveHunters, eventOutbox, StageCommitCheckpoint, Source, resolveEventEntity(nextEvent), randomInteractionPresenter, manager.EventResourceCommand);
            return currentEntry;
        }

        protected override ActionOutcome Resolve(CompositeExecutionContext context) => ActionOutcome.Success();

        private void StageCommitCheckpoint(PlayableEventCommitCheckpoint checkpoint)
        {
            manager.EnsureSelectedHunterAvailable();
            eventOutbox.Stage(new HuntEventNodeCommittedEvent
            {
                Coordinate = commit.Coordinate,
                EventId = checkpoint.EventId,
                ActorId = checkpoint.ActorId,
                Kind = checkpoint.Kind
            });
        }

        private void TryEnqueue(HuntingInDarkness.Data.EventData gameEvent)
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

    public sealed class SelectHuntTileEventAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly HuntManager manager;
        private readonly HuntTileInteractionCommit commit;

        internal SelectHuntTileEventAction(HuntManager manager, HuntTileInteractionCommit commit, IReactorEntity source, IReactorEntity target)
        {
            this.manager = manager;
            this.commit = commit;
            Source = source;
            Target = target;
        }

        public HuntingInDarkness.Data.EventData SelectedEvent { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            SelectedEvent = manager.SelectTileInteractionEvent(commit);
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
