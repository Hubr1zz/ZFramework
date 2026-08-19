using System;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Hunt;
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
            if (!commitAction.IsCommitted || eventScheduled) return null;
            eventScheduled = true;
            return new ResolveHuntTileEventAction(manager, commitAction.Commit, Source, Target);
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
    public sealed class ResolveHuntTileEventAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly HuntManager manager;
        private readonly HuntTileInteractionCommit commit;

        internal ResolveHuntTileEventAction(HuntManager manager, HuntTileInteractionCommit commit, IReactorEntity source, IReactorEntity target)
        {
            this.manager = manager;
            this.commit = commit;
            Source = source;
            Target = target;
        }

        public HuntTileInteractionCommit Commit => commit;
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }
        public override ReactionPhases OpenReactionPhases => ReactionPhases.BeforeExecution | ReactionPhases.AfterResolved;

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            try
            {
                manager.ResolveTileInteractionEvent(commit);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }
}
