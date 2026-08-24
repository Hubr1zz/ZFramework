using System.Collections.Generic;
using System.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableEventChainOccurrenceQueueTests
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
        public void Commit_LeavesRepeatedSiblingOccurrencesDistinctAndConsumesOne()
        {
            var queue = new PlayableEventChainOccurrenceQueue(64);

            PlayableEventChainCommitResult result = queue.Commit(-1, new[] { "same", "same" }, 2, 7);

            Assert.That(result.AppendedOccurrences, Has.Count.EqualTo(2));
            Assert.That(result.AppendedOccurrences[0].Sequence, Is.Not.EqualTo(result.AppendedOccurrences[1].Sequence));
            queue.Commit(result.AppendedOccurrences[0].Sequence, System.Array.Empty<string>(), 2, 7);
            Assert.That(queue.PendingOccurrences, Has.Count.EqualTo(1));
            Assert.That(queue.PendingOccurrences[0].Sequence, Is.EqualTo(result.AppendedOccurrences[1].Sequence));
        }

        [Test]
        public void Commit_RejectsExhaustedSequenceWithoutCreatingInvalidOccurrence()
        {
            var queue = new PlayableEventChainOccurrenceQueue(64, int.MaxValue);

            PlayableEventChainCommitResult result = queue.Commit(-1, new[] { "child" }, 2, 7);

            Assert.That(result.AppendedOccurrences, Is.Empty);
            Assert.That(result.Diagnostic, Does.Contain("序号"));
            Assert.That(queue.PendingOccurrences, Is.Empty);
        }

        [Test]
        public void Constructor_PreservesExhaustedSequenceFromMaximumPendingOccurrence()
        {
            var pending = new[] { new PlayableEventChainOccurrence(int.MaxValue, "existing", "existing", 1, 1) };
            var queue = new PlayableEventChainOccurrenceQueue(64, pendingOccurrences: pending);

            PlayableEventChainCommitResult result = queue.Commit(-1, new[] { "child" }, 2, 7);

            Assert.That(result.AppendedOccurrences, Is.Empty);
            Assert.That(result.Diagnostic, Does.Contain("序号"));
            Assert.That(queue.PendingOccurrences, Has.Count.EqualTo(1));
        }

        [Test]
        public void Constructor_RestoresNextRootSequenceWithoutReusingCommittedRoots()
        {
            var queue = new PlayableEventChainOccurrenceQueue(64, committedSequences: new[] { -1, -2 }, nextRootSequence: -3);

            Assert.That(queue.TryScheduleRoot("root", "root", 2, 7, out PlayableEventChainOccurrence occurrence), Is.True);
            Assert.That(occurrence.Sequence, Is.EqualTo(-3));
            Assert.That(queue.NextRootSequence, Is.EqualTo(-4));
        }

        [Test]
        public void SettlementAdapter_RoundTripsSchemaOneCheckpointThroughJsonUtility()
        {
            var settlement = new SettlementInstance();
            var adapter = new SettlementEventChainCheckpointAdapter(settlement);
            adapter.Commit("schema-chain", -1, new[] { "child" }, 3, 11);

            SettlementInstance restored = JsonUtility.FromJson<SettlementInstance>(JsonUtility.ToJson(settlement));
            SettlementEventChainCheckpoint checkpoint = restored.PendingEventChains[0];
            var restoredAdapter = new SettlementEventChainCheckpointAdapter(restored);
            IReadOnlyList<PlayableEventChainOccurrence> pending = restoredAdapter.GetPending("schema-chain");

            Assert.That(checkpoint.SchemaVersion, Is.EqualTo(PlayableEventChainOccurrenceQueue.CurrentSchemaVersion));
            Assert.That(pending, Has.Count.EqualTo(1));
            Assert.That(pending[0].EventId, Is.EqualTo("child"));
            Assert.That(pending[0].Sequence, Is.EqualTo(1));
        }

        [Test]
        public async Task SettlementRunner_UsesExplicitContentIdToStopAncestorCycle()
        {
            var settlement = new SettlementInstance();
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            EventData root = CreateNarrativeEvent("root-asset", "root-id");
            EventData child = CreateNarrativeEvent("child-asset", "child-id");
            EventData rootAlias = CreateNarrativeEvent("root-alias-asset", "root-id");
            root.chainedEvents.Add(child);
            child.chainedEvents.Add(rootAlias);

            using var session = new PlayableSettlementActionSession(settlement, new EmptyWeaponTrainingContent(), eventSystem);
            SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { root });

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(result.ResolvedCount, Is.EqualTo(2));
            Assert.That(settlement.HasPendingEventChainOccurrences, Is.False);
        }

        private EventData CreateNarrativeEvent(string name, string contentId)
        {
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = name;
            gameEvent.ConfigureContentId(contentId);
            createdEvents.Add(gameEvent);
            return gameEvent;
        }

        private sealed class EmptyWeaponTrainingContent : IWeaponTrainingContent
        {
            public string RequiredInventionId => string.Empty;
            public string CostResourceId => string.Empty;
            public int ResourceCost => 0;
            public int Experience => 0;

            public bool TryGetFamily(string masteryId, out WeaponMasteryFamilyDefinition family)
            {
                family = default;
                return false;
            }
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
