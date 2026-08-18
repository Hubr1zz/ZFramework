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
