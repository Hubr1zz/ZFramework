using System;
using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Cards
{
    public enum ActionQueueStatus
    {
        Idle,
        Running,
        Paused,
        Completed,
        Cancelled,
        Failed
    }

    public enum ActionQueueActionResult
    {
        Completed,
        Cancelled,
        Failed
    }

    public interface IActionQueueAction
    {
        string Name { get; }
    }

    /// <summary>
    /// Deterministic queue state. Execution and asynchronous waits are owned by adapters.
    /// </summary>
    public sealed class ActionQueue
    {
        private readonly LinkedList<IActionQueueAction> _pending =
            new LinkedList<IActionQueueAction>();

        public ActionQueueStatus Status { get; private set; } = ActionQueueStatus.Idle;
        public IActionQueueAction Current { get; private set; }
        public int PendingCount => _pending.Count;

        public void EnqueueFront(IActionQueueAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            EnsureMutable();
            _pending.AddFirst(action);
        }

        public void EnqueueBack(IActionQueueAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            EnsureMutable();
            _pending.AddLast(action);
        }

        public void Start()
        {
            if (Status != ActionQueueStatus.Idle)
                throw new InvalidOperationException("The action queue has already started.");
            Status = _pending.Count == 0
                ? ActionQueueStatus.Completed
                : ActionQueueStatus.Running;
        }

        public bool TryBeginNext(out IActionQueueAction action)
        {
            action = null;
            if (Status != ActionQueueStatus.Running || Current != null)
                return false;
            if (_pending.Count == 0)
            {
                Status = ActionQueueStatus.Completed;
                return false;
            }

            Current = _pending.First.Value;
            _pending.RemoveFirst();
            action = Current;
            return true;
        }

        public void Pause()
        {
            if (Status != ActionQueueStatus.Running || Current == null)
                throw new InvalidOperationException("Only a running action can pause the queue.");
            Status = ActionQueueStatus.Paused;
        }

        public void Resume()
        {
            if (Status != ActionQueueStatus.Paused || Current == null)
                throw new InvalidOperationException("Only a paused action can resume the queue.");
            Status = ActionQueueStatus.Running;
        }

        public void CompleteCurrent(ActionQueueActionResult result)
        {
            if (Status != ActionQueueStatus.Running || Current == null)
                throw new InvalidOperationException("There is no running action to complete.");

            Current = null;
            switch (result)
            {
                case ActionQueueActionResult.Cancelled:
                    _pending.Clear();
                    Status = ActionQueueStatus.Cancelled;
                    break;
                case ActionQueueActionResult.Failed:
                    _pending.Clear();
                    Status = ActionQueueStatus.Failed;
                    break;
                default:
                    Status = _pending.Count == 0
                        ? ActionQueueStatus.Completed
                        : ActionQueueStatus.Running;
                    break;
            }
        }

        public void Cancel()
        {
            EnsureMutable();
            Current = null;
            _pending.Clear();
            Status = ActionQueueStatus.Cancelled;
        }

        private void EnsureMutable()
        {
            if (Status == ActionQueueStatus.Completed ||
                Status == ActionQueueStatus.Cancelled ||
                Status == ActionQueueStatus.Failed)
                throw new InvalidOperationException("A terminal action queue cannot be changed.");
        }
    }
}
