using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class SettlementEventRestoreProjectionTests
    {
        private readonly List<EventData> createdEvents = new();

        [TearDown]
        public void TearDown()
        {
            foreach (EventData gameEvent in createdEvents)
                if (gameEvent != null)
                    Object.DestroyImmediate(gameEvent);
            createdEvents.Clear();
        }

        [Test]
        public void Prepare_OnlyProjectsUncompletedEntriesWithoutChangingTimeline()
        {
            EventData pendingEvent = CreateEvent("pending_event");
            EventData completedEvent = CreateEvent("completed_event");
            var settlement = new SettlementInstance();
            settlement.Timeline.Add(new AnnalEntry { Year = 2, EventId = pendingEvent.name, IsCompleted = false });
            settlement.Timeline.Add(new AnnalEntry { Year = 2, EventId = completedEvent.name, IsCompleted = true });
            int timelineCount = settlement.Timeline.Count;

            var projection = new SettlementEventRestoreProjection(settlement, id => id == pendingEvent.name ? pendingEvent : completedEvent);
            SettlementEventRestorePlan plan = projection.Prepare();

            Assert.That(plan.Succeeded, Is.True);
            Assert.That(plan.Events, Has.Count.EqualTo(1));
            Assert.That(settlement.Timeline, Has.Count.EqualTo(timelineCount));
            Assert.That(settlement.Timeline[0].IsCompleted, Is.False);
            Assert.That(settlement.Timeline[1].IsCompleted, Is.True);
        }

        [Test]
        public void Prepare_EmptyOrCompletedTimeline_DoesNotResolveAnything()
        {
            var settlement = new SettlementInstance();
            settlement.Timeline.Add(new AnnalEntry { EventId = "already_done", IsCompleted = true });
            int resolveCount = 0;
            var projection = new SettlementEventRestoreProjection(settlement, _ =>
            {
                resolveCount++;
                return null;
            });

            SettlementEventRestorePlan plan = projection.Prepare();

            Assert.That(plan.Succeeded, Is.True);
            Assert.That(plan.Events, Is.Empty);
            Assert.That(resolveCount, Is.Zero);
            Assert.That(projection.IsReady, Is.True);
        }

        [Test]
        public void Prepare_UnknownEventFailsClosedWithDiagnostic()
        {
            var settlement = new SettlementInstance();
            settlement.Timeline.Add(new AnnalEntry { EventId = "missing_event", IsCompleted = false });
            var projection = new SettlementEventRestoreProjection(settlement, _ => null);

            SettlementEventRestorePlan plan = projection.Prepare();

            Assert.That(plan.Succeeded, Is.False);
            Assert.That(plan.FailureReason, Does.Contain("missing_event"));
            Assert.That(projection.IsReady, Is.False);
            Assert.That(projection.FailureReason, Does.Contain("missing_event"));
        }

        [Test]
        public void Prepare_DuplicateEventIdsRemainSeparateAndRepeatedPrepareDoesNotAppend()
        {
            EventData gameEvent = CreateEvent("repeatable_event");
            var settlement = new SettlementInstance();
            var first = new AnnalEntry { Year = 2, EventId = gameEvent.name };
            var second = new AnnalEntry { Year = 3, EventId = gameEvent.name };
            settlement.Timeline.Add(first);
            settlement.Timeline.Add(second);
            var projection = new SettlementEventRestoreProjection(settlement, _ => gameEvent);

            SettlementEventRestorePlan firstPlan = projection.Prepare();
            SettlementEventRestorePlan repeatedPlan = projection.Prepare();

            Assert.That(firstPlan.Events, Has.Count.EqualTo(2));
            Assert.That(repeatedPlan.AlreadyInProgress, Is.True);
            Assert.That(repeatedPlan.Events, Is.Empty);

            first.IsCompleted = true;
            second.IsCompleted = true;
            Assert.That(projection.Complete(true), Is.True);
            Assert.That(projection.Prepare().Events, Is.Empty);
        }

        [Test]
        public void CompleteFailureKeepsDepartureGateClosed()
        {
            EventData gameEvent = CreateEvent("cancelled_event");
            var settlement = new SettlementInstance();
            settlement.Timeline.Add(new AnnalEntry { EventId = gameEvent.name });
            var projection = new SettlementEventRestoreProjection(settlement, _ => gameEvent);

            Assert.That(projection.Prepare().Succeeded, Is.True);
            Assert.That(projection.Complete(false), Is.False);
            Assert.That(projection.IsReady, Is.False);
            Assert.That(projection.FailureReason, Does.Contain("门禁"));
        }

        private EventData CreateEvent(string id)
        {
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = id;
            gameEvent.eventName = id;
            createdEvents.Add(gameEvent);
            return gameEvent;
        }
    }
}
