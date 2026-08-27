using System;
using System.Threading;
using CardGame.ActionQueue;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;

namespace HuntingInDarkness.ActionFlow.Hunt
{
    public readonly struct HuntActorSelectionResult
    {
        public HuntActorSelectionResult(bool succeeded, bool changed, int previousHunterId, int selectedHunterId, string reason)
        {
            Succeeded = succeeded;
            Changed = changed;
            PreviousHunterId = previousHunterId;
            SelectedHunterId = selectedHunterId;
            Reason = reason ?? string.Empty;
        }

        public bool Succeeded { get; }
        public bool Changed { get; }
        public int PreviousHunterId { get; }
        public int SelectedHunterId { get; }
        public string Reason { get; }

        public static HuntActorSelectionResult Failed(string reason) => new(false, false, 0, 0, reason);
    }

    public struct HuntActorSelectionCommittedEvent
    {
        public Guid SessionId;
        public int PreviousHunterId;
        public int SelectedHunterId;
    }

    public sealed class SelectHuntActorAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly HuntManager manager;
        private readonly int hunterId;
        private readonly Guid sessionId;
        private readonly ActionEventOutbox eventOutbox;

        public SelectHuntActorAction(HuntManager manager, int hunterId, Guid sessionId, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.hunterId = hunterId;
            this.sessionId = sessionId;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public HuntActorSelectionResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!manager.TryCommitSelectedHunter(hunterId, out HunterInstance previous, out HunterInstance selected, out string reason))
                return Fail(reason);
            bool changed = !ReferenceEquals(previous, selected);
            Result = new HuntActorSelectionResult(true, changed, previous?.InstanceId ?? 0, selected.InstanceId, string.Empty);
            if (changed)
                eventOutbox.Stage(new HuntActorSelectionCommittedEvent { SessionId = sessionId, PreviousHunterId = previous?.InstanceId ?? 0, SelectedHunterId = selected.InstanceId });
            return UniTask.FromResult(ActionOutcome.Success());
        }

        private UniTask<ActionOutcome> Fail(string reason)
        {
            Result = HuntActorSelectionResult.Failed(reason);
            return UniTask.FromResult(ActionOutcome.Failure(reason));
        }
    }
}
