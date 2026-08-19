using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace HuntingInDarkness.ActionFlow.Hunt
{
    public readonly struct HuntTileCommandResult
    {
        private HuntTileCommandResult(bool succeeded, string reason, HuntTileInteractionCommit commit)
        {
            Succeeded = succeeded;
            Reason = reason;
            Commit = commit;
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public HuntTileInteractionCommit Commit { get; }
        public static HuntTileCommandResult Success(HuntTileInteractionCommit commit) => new(true, string.Empty, commit);
        public static HuntTileCommandResult Failed(string reason) => new(false, reason, default);
    }

    public struct HuntTileInteractionCommittedEvent
    {
        public Vector2Int Coordinate;
        public HuntTileInteractionKind Kind;
        public int ResourcePointCount;
        public bool BossEncounter;
    }

    /// <summary>一次地图点击的完整因果链：权威状态提交后，再进入可覆盖的地块事件窗口。</summary>
    public sealed class InteractHuntTileAction : CompositeGameAction, ISourceAction, ITargetAction
    {
        private readonly HuntManager manager;
        private readonly Vector2Int coordinate;
        private readonly HuntTileInteractionKind intendedKind;
        private readonly ActionEventOutbox eventOutbox;
        private CommitHuntTileInteractionAction commitAction;
        private bool eventScheduled;
        private bool finalizeScheduled;

        public InteractHuntTileAction(HuntManager manager, Vector2Int coordinate, HuntTileInteractionKind intendedKind, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.coordinate = coordinate;
            this.intendedKind = intendedKind;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
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
                commitAction = new CommitHuntTileInteractionAction(manager, coordinate, intendedKind, eventOutbox, Source, Target);
                return commitAction;
            }
            if (!commitAction.IsCommitted) return null;
            if (!eventScheduled)
            {
                eventScheduled = true;
                return new ResolveHuntTileEventAction(manager, commitAction.Commit, eventOutbox, Source, Target);
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
            Result = HuntTileCommandResult.Success(commitAction.Commit);
            return ActionOutcome.Success();
        }
    }

    public sealed class CommitHuntTileInteractionAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly HuntManager manager;
        private readonly Vector2Int coordinate;
        private readonly HuntTileInteractionKind intendedKind;
        private readonly ActionEventOutbox eventOutbox;

        internal CommitHuntTileInteractionAction(HuntManager manager, Vector2Int coordinate, HuntTileInteractionKind intendedKind, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.manager = manager;
            this.coordinate = coordinate;
            this.intendedKind = intendedKind;
            this.eventOutbox = eventOutbox;
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
        private readonly Queue<HuntingInDarkness.Data.EventData> pendingEvents = new();
        private SelectHuntTileEventAction selectAction;
        private ResolveHuntEventEntryAction currentEntry;
        private bool selectionCollected;

        internal ResolveHuntTileEventAction(HuntManager manager, HuntTileInteractionCommit commit, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.manager = manager;
            this.commit = commit;
            this.eventOutbox = eventOutbox;
            Source = source;
            Target = target;
        }

        public HuntTileInteractionCommit Commit => commit;
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
                if (selectAction.SelectedEvent != null)
                    pendingEvents.Enqueue(selectAction.SelectedEvent);
            }
            else if (currentEntry != null)
            {
                foreach (HuntingInDarkness.Data.EventData chained in currentEntry.ChainedEvents)
                    if (chained != null)
                        pendingEvents.Enqueue(chained);
                currentEntry = null;
            }
            if (pendingEvents.Count == 0) return null;
            currentEntry = new ResolveHuntEventEntryAction(manager, pendingEvents.Dequeue(), manager.SelectedHunter, eventOutbox, Source, Target);
            return currentEntry;
        }

        protected override ActionOutcome Resolve(CompositeExecutionContext context) => ActionOutcome.Success();
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

    public sealed class ResolveHuntEventEntryAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly HuntManager manager;
        private readonly HuntingInDarkness.Data.EventData gameEvent;
        private readonly HuntingInDarkness.Data.HunterInstance defaultActor;
        private readonly ActionEventOutbox eventOutbox;

        internal ResolveHuntEventEntryAction(HuntManager manager, HuntingInDarkness.Data.EventData gameEvent, HuntingInDarkness.Data.HunterInstance defaultActor, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.manager = manager;
            this.gameEvent = gameEvent;
            this.defaultActor = defaultActor;
            this.eventOutbox = eventOutbox;
            Source = source;
            Target = target;
        }

        public IReadOnlyList<HuntingInDarkness.Data.EventData> ChainedEvents { get; private set; } = Array.Empty<HuntingInDarkness.Data.EventData>();
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            if (gameEvent == null) return ActionOutcome.Success();
            eventOutbox.Stage(new GameEventTriggeredEvent { EventId = gameEvent.name });
            eventOutbox.PublishCheckpoint();
            if (gameEvent.eventType != HuntingInDarkness.Data.GameEventType.Choice || gameEvent.options == null || gameEvent.options.Count == 0)
            {
                if (manager.EventInput != null)
                    await manager.EventInput.ConfirmNarrativeAsync(gameEvent, defaultActor, cancellationToken);
                ChainedEvents = manager.EventSystem.ResolveNarrativeStandalone(gameEvent, defaultActor);
                return ActionOutcome.Success();
            }

            HuntEventChoiceSelection selection = manager.EventInput != null
                ? await manager.EventInput.SelectChoiceAsync(gameEvent, defaultActor, manager.ActiveHunters, cancellationToken)
                : FindAutomaticSelection();
            PlayableEventChoiceTransaction transaction = selection.IsValid ? manager.EventSystem.PrepareChoice(gameEvent, selection.OptionIndex, selection.Actor) : null;
            if (transaction == null)
            {
                selection = FindAutomaticSelection();
                transaction = selection.IsValid ? manager.EventSystem.PrepareChoice(gameEvent, selection.OptionIndex, selection.Actor) : null;
            }
            if (transaction == null)
            {
                ChainedEvents = manager.EventSystem.ResolveNarrativeStandalone(gameEvent, defaultActor);
                return ActionOutcome.Success();
            }

            while (transaction.RequiresCheck && manager.EventInput != null)
            {
                HuntEventCheckDecision decision = await manager.EventInput.PresentCheckAsync(transaction, cancellationToken);
                if (decision != HuntEventCheckDecision.Reroll || !transaction.TryReroll()) break;
            }
            PlayableEventCommitResult result = transaction.CommitStandalone();
            ChainedEvents = result.ChainedEvents;
            if (manager.EventInput != null)
                await manager.EventInput.ConfirmResultAsync(gameEvent, result.Result, cancellationToken);
            return ActionOutcome.Success();
        }

        private HuntEventChoiceSelection FindAutomaticSelection()
        {
            for (int optionIndex = 0; optionIndex < gameEvent.options.Count; optionIndex++)
            {
                HuntingInDarkness.Data.EventOption option = gameEvent.options[optionIndex];
                if (HuntingInDarkness.Settlement.PlayableEventOptionAvailability.CanUse(option, defaultActor, manager.EventSystem.Settlement, out _))
                    return new HuntEventChoiceSelection(optionIndex, defaultActor);
                foreach (HuntingInDarkness.Data.HunterInstance hunter in manager.ActiveHunters)
                    if (HuntingInDarkness.Settlement.PlayableEventOptionAvailability.CanUse(option, hunter, manager.EventSystem.Settlement, out _))
                        return new HuntEventChoiceSelection(optionIndex, hunter);
            }
            return new HuntEventChoiceSelection(-1, null);
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
