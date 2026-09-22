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
            var settlement = new SettlementInstance { CurrentYear = 2 };
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
        public void Prepare_SkipsFutureScheduledEntryUntilItsYearArrives()
        {
            EventData futureEvent = CreateEvent("future_scheduled_event");
            var settlement = new SettlementInstance { CurrentYear = 2 };
            var entry = new AnnalEntry { Year = 3, EventId = futureEvent.name, IsCompleted = false };
            settlement.Timeline.Add(entry);
            int resolveCount = 0;
            var projection = new SettlementEventRestoreProjection(settlement, _ =>
            {
                resolveCount++;
                return futureEvent;
            });

            SettlementEventRestorePlan beforeDue = projection.Prepare();

            Assert.That(beforeDue.Succeeded, Is.True);
            Assert.That(beforeDue.Events, Is.Empty);
            Assert.That(resolveCount, Is.Zero);
            Assert.That(entry.IsCompleted, Is.False);

            settlement.CurrentYear = 3;
            SettlementEventRestorePlan due = projection.Prepare();

            Assert.That(due.Succeeded, Is.True);
            Assert.That(due.Events, Has.Count.EqualTo(1));
            Assert.That(resolveCount, Is.EqualTo(1));
            Assert.That(entry.IsCompleted, Is.False);
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
            var settlement = new SettlementInstance { CurrentYear = 3 };
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

        [Test]
        public void CommitParentCreatesOrderedChildCheckpointAndConsumesOnlyCompletedOccurrence()
        {
            var settlement = new SettlementInstance();

            settlement.CommitEventChainOccurrence("chain", -1, new[] { "same", "same" }, 2, 7);
            IReadOnlyList<SettlementEventChainOccurrence> pending = settlement.GetPendingEventChainOccurrences("chain");

            Assert.That(pending, Has.Count.EqualTo(2));
            Assert.That(pending[0].EventId, Is.EqualTo("same"));
            Assert.That(pending[1].EventId, Is.EqualTo("same"));
            Assert.That(pending[0].Sequence, Is.Not.EqualTo(pending[1].Sequence));

            settlement.CommitEventChainOccurrence("chain", pending[0].Sequence, System.Array.Empty<string>(), 2, 7);
            pending = settlement.GetPendingEventChainOccurrences("chain");

            Assert.That(pending, Has.Count.EqualTo(1));
            Assert.That(pending[0].Sequence, Is.EqualTo(2));
        }

        [Test]
        public void RepeatedCommitIsIdempotentAndNormalCompletionClearsCheckpoint()
        {
            var settlement = new SettlementInstance();
            settlement.CommitEventChainOccurrence("chain", -1, new[] { "child" }, 2, 0);
            settlement.CommitEventChainOccurrence("chain", -1, new[] { "child" }, 2, 0);
            IReadOnlyList<SettlementEventChainOccurrence> pending = settlement.GetPendingEventChainOccurrences("chain");

            Assert.That(pending, Has.Count.EqualTo(1));
            settlement.CommitEventChainOccurrence("chain", pending[0].Sequence, System.Array.Empty<string>(), 2, 0);

            Assert.That(settlement.HasPendingEventChainOccurrences, Is.False);
            Assert.That(settlement.PendingEventChains, Is.Empty);
        }

        [Test]
        public void CheckpointRestoreUnknownIdIsolatedWithoutDeletingData()
        {
            var settlement = new SettlementInstance();
            settlement.CommitEventChainOccurrence("chain", -1, new[] { "missing" }, 2, 0);
            var projection = new SettlementEventRestoreProjection(settlement, _ => null);

            SettlementEventRestorePlan plan = projection.Prepare();

            Assert.That(plan.Succeeded, Is.False);
            Assert.That(plan.FailureReason, Does.Contain("missing"));
            Assert.That(settlement.HasPendingEventChainOccurrences, Is.True);
            Assert.That(projection.IsReady, Is.False);
        }

        [Test]
        public void Prepare_FutureCheckpointSchemaFailsClosedWithoutDeletingData()
        {
            EventData gameEvent = CreateEvent("future_event");
            var settlement = new SettlementInstance();
            settlement.CommitEventChainOccurrence("future-chain", -1, new[] { gameEvent.ContentId }, 2, 0);
            settlement.PendingEventChains[0].SchemaVersion = SettlementEventChainCheckpoint.CurrentSchemaVersion + 1;
            var projection = new SettlementEventRestoreProjection(settlement, _ => gameEvent);

            SettlementEventRestorePlan plan = projection.Prepare();

            Assert.That(plan.Succeeded, Is.False);
            Assert.That(plan.FailureReason, Does.Contain("schema"));
            Assert.That(settlement.HasPendingEventChainOccurrences, Is.True);
        }

        [Test]
        public void Prepare_LegacyCheckpointWithoutAncestorPathFailsClosedWithoutDeletingData()
        {
            EventData gameEvent = CreateEvent("legacy_event");
            var settlement = new SettlementInstance();
            settlement.CommitEventChainOccurrence("legacy-chain", -1, new[] { gameEvent.ContentId }, 2, 0);
            settlement.PendingEventChains[0].SchemaVersion = SettlementEventChainCheckpoint.CurrentSchemaVersion - 1;
            var projection = new SettlementEventRestoreProjection(settlement, _ => gameEvent);

            SettlementEventRestorePlan plan = projection.Prepare();

            Assert.That(plan.Succeeded, Is.False);
            Assert.That(plan.FailureReason, Does.Contain("schema"));
            Assert.That(settlement.HasPendingEventChainOccurrences, Is.True);
        }

        [Test]
        public void Prepare_OversizedCheckpointFailsClosedWithoutResolvingOrDeletingData()
        {
            var settlement = new SettlementInstance();
            var pending = new List<SettlementEventChainOccurrence>();
            for (int index = 1; index <= SettlementInstance.MaxPendingEventChainOccurrences + 1; index++)
                pending.Add(new SettlementEventChainOccurrence { Sequence = index, EventId = $"event-{index}" });
            settlement.PendingEventChains.Add(new SettlementEventChainCheckpoint { ChainId = "oversized", PendingOccurrences = pending });
            int resolveCount = 0;
            var projection = new SettlementEventRestoreProjection(settlement, _ =>
            {
                resolveCount++;
                return null;
            });

            SettlementEventRestorePlan plan = projection.Prepare();

            Assert.That(plan.Succeeded, Is.False);
            Assert.That(plan.FailureReason, Does.Contain("上限"));
            Assert.That(resolveCount, Is.Zero);
            Assert.That(settlement.PendingEventChains[0].PendingOccurrences, Has.Count.EqualTo(SettlementInstance.MaxPendingEventChainOccurrences + 1));
        }

        [Test]
        public void FailedRestoreReleasesBusyStateAndAllowsExplicitRetry()
        {
            EventData gameEvent = CreateEvent("retry_event");
            var settlement = new SettlementInstance();
            settlement.Timeline.Add(new AnnalEntry { EventId = gameEvent.name });
            var projection = new SettlementEventRestoreProjection(settlement, _ => gameEvent);

            Assert.That(projection.Prepare().Succeeded, Is.True);
            Assert.That(projection.Complete(false), Is.False);
            SettlementEventRestorePlan retry = projection.Prepare();

            Assert.That(retry.Succeeded, Is.True);
            Assert.That(retry.AlreadyInProgress, Is.False);
            Assert.That(retry.Events, Has.Count.EqualTo(1));
        }

        [Test]
        public void Prepare_RecoversMultiplePendingChainsSequentially()
        {
            EventData firstEvent = CreateEvent("first_chain_event");
            EventData secondEvent = CreateEvent("second_chain_event");
            var settlement = new SettlementInstance();
            settlement.CommitEventChainOccurrence("first-chain", -1, new[] { firstEvent.name }, 1, 0);
            settlement.CommitEventChainOccurrence("second-chain", -1, new[] { secondEvent.name }, 1, 0);
            var projection = new SettlementEventRestoreProjection(settlement, id => id == firstEvent.name ? firstEvent : secondEvent);

            SettlementEventRestorePlan firstPlan = projection.Prepare();
            Assert.That(firstPlan.ChainId, Is.EqualTo("first-chain"));
            settlement.CommitEventChainOccurrence("first-chain", firstPlan.Occurrences[0].Sequence, System.Array.Empty<string>(), 1, 0);
            Assert.That(projection.Complete(true), Is.False);

            SettlementEventRestorePlan secondPlan = projection.Prepare();
            Assert.That(secondPlan.Succeeded, Is.True);
            Assert.That(secondPlan.ChainId, Is.EqualTo("second-chain"));
            settlement.CommitEventChainOccurrence("second-chain", secondPlan.Occurrences[0].Sequence, System.Array.Empty<string>(), 1, 0);
            Assert.That(projection.Complete(true), Is.True);
            Assert.That(settlement.HasPendingEventChainOccurrences, Is.False);
        }

        [Test]
        public void CheckpointCapsPendingOccurrencesAndPreservesDiagnostic()
        {
            var settlement = new SettlementInstance();
            var childIds = new List<string>();
            for (int index = 0; index < SettlementInstance.MaxPendingEventChainOccurrences + 1; index++)
                childIds.Add($"event-{index}");

            settlement.CommitEventChainOccurrence("bounded", -1, childIds, 1, 0);

            Assert.That(settlement.PendingEventChains, Has.Count.EqualTo(1));
            Assert.That(settlement.PendingEventChains[0].PendingOccurrences, Has.Count.EqualTo(SettlementInstance.MaxPendingEventChainOccurrences));
            Assert.That(settlement.PendingEventChains[0].Diagnostic, Does.Contain("上限"));
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
