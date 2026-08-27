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

    /// <summary>暂存 ZFramework 事件；只有所属 Root Action 成功后才按登记顺序发布。</summary>
    public sealed class ActionEventOutbox
    {
        private readonly List<IPendingEvent> pendingEvents = new();
        private readonly List<IPendingEvent> afterCommitEvents = new();
        private bool claimed;

        public ActionEventOutboxState State { get; private set; } = ActionEventOutboxState.Pending;
        public int PendingCount => pendingEvents.Count + afterCommitEvents.Count;

        public void Stage<TEvent>(TEvent evt) where TEvent : struct
        {
            EnsurePending();
            pendingEvents.Add(new PendingEvent<TEvent>(evt));
        }

        /// <summary>仅在所属 Root 完成后发布；检查点不会提前冲刷跨环境交接事实。</summary>
        public void StageAfterCommit<TEvent>(TEvent evt) where TEvent : struct
        {
            EnsurePending();
            afterCommitEvents.Add(new PendingEvent<TEvent>(evt));
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
            PublishEvents(afterCommitEvents);
        }

        private void PublishPendingEvents()
        {
            PublishEvents(pendingEvents);
        }

        private static void PublishEvents(List<IPendingEvent> events)
        {
            foreach (IPendingEvent pendingEvent in events)
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
            events.Clear();
        }

        internal void Discard()
        {
            if (State != ActionEventOutboxState.Pending) return;
            State = ActionEventOutboxState.Discarded;
            pendingEvents.Clear();
            afterCommitEvents.Clear();
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
