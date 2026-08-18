using System;
using System.Collections.Generic;
using Core;
using UnityEngine;

namespace HuntingInDarkness.ActionFlow
{
    public enum ActionEventOutboxState
    {
        Pending,
        Committed,
        Discarded
    }

    /// <summary>暂存 TEngine 事件；只有所属 Root Action 成功后才按登记顺序发布。</summary>
    public sealed class ActionEventOutbox
    {
        private readonly List<IPendingEvent> pendingEvents = new();
        private bool claimed;

        public ActionEventOutboxState State { get; private set; } = ActionEventOutboxState.Pending;
        public int PendingCount => pendingEvents.Count;

        public void Stage<TEvent>(TEvent evt) where TEvent : struct
        {
            EnsurePending();
            pendingEvents.Add(new PendingEvent<TEvent>(evt));
        }

        /// <summary>发布已经不可回滚的增量状态事实，同时保持 Outbox 可供同一 Root 继续暂存后续事件。</summary>
        public void PublishCheckpoint()
        {
            EnsurePending();
            if (!claimed)
                throw new InvalidOperationException("An unclaimed event outbox cannot publish a checkpoint.");
            PublishPendingEvents();
        }

        internal void Claim()
        {
            EnsurePending();
            if (claimed)
                throw new InvalidOperationException("An event outbox can belong to only one root action.");
            claimed = true;
        }

        internal void Commit()
        {
            EnsurePending();
            if (!claimed)
                throw new InvalidOperationException("An unclaimed event outbox cannot be committed.");
            State = ActionEventOutboxState.Committed;
            PublishPendingEvents();
        }

        private void PublishPendingEvents()
        {
            foreach (IPendingEvent pendingEvent in pendingEvents)
            {
                try
                {
                    pendingEvent.Publish();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
            pendingEvents.Clear();
        }

        internal void Discard()
        {
            if (State != ActionEventOutboxState.Pending) return;
            State = ActionEventOutboxState.Discarded;
            pendingEvents.Clear();
        }

        private void EnsurePending()
        {
            if (State != ActionEventOutboxState.Pending)
                throw new InvalidOperationException("A committed or discarded event outbox cannot be changed.");
        }

        private interface IPendingEvent
        {
            void Publish();
        }

        private sealed class PendingEvent<TEvent> : IPendingEvent where TEvent : struct
        {
            private readonly TEvent evt;

            public PendingEvent(TEvent evt)
            {
                this.evt = evt;
            }

            public void Publish() => EventBus.Publish(evt);
        }
    }
}
