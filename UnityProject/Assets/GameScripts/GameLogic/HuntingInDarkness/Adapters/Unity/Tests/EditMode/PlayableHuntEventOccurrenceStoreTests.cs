using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.Data;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableHuntEventOccurrenceStoreTests
    {
        private readonly List<EventData> events = new();

        [TearDown]
        public void TearDown()
        {
            foreach (EventData gameEvent in events)
                UnityEngine.Object.DestroyImmediate(gameEvent);
            events.Clear();
        }

        [Test]
        public void Commit_PreservesRepeatedSiblingEventIdsAsIndependentOccurrences()
        {
            var store = new PlayableHuntEventOccurrenceStore();
            EventData root = CreateEvent("root");
            EventData childA = CreateEvent("same");
            EventData childB = CreateEvent("same");
            Assert.That(store.TryScheduleRoot(root, new Vector2Int(2, 3), 4, 9, out PlayableHuntEventOccurrence parent), Is.True);

            PlayableHuntEventOccurrenceCommitResult result = store.Commit(parent, new[] { childA, childB }, 4, 9);

            Assert.That(result.AppendedOccurrences, Has.Count.EqualTo(2));
            Assert.That(result.AppendedOccurrences[0].Sequence, Is.Not.EqualTo(result.AppendedOccurrences[1].Sequence));
            Assert.That(result.AppendedOccurrences[0].Coordinate, Is.EqualTo(new Vector2Int(2, 3)));
            Assert.That(store.HasPendingOccurrences, Is.True);
        }

        [Test]
        public void Commit_RejectsAncestorContentIdAndStopsAtoBtoA()
        {
            var store = new PlayableHuntEventOccurrenceStore();
            EventData first = CreateEvent("a");
            EventData second = CreateEvent("b");
            EventData cycle = CreateEvent("a");
            Assert.That(store.TryScheduleRoot(first, Vector2Int.zero, 1, 1, out PlayableHuntEventOccurrence root), Is.True);
            PlayableHuntEventOccurrenceCommitResult firstCommit = store.Commit(root, new[] { second }, 1, 1);
            PlayableHuntEventOccurrenceCommitResult secondCommit = store.Commit(firstCommit.AppendedOccurrences[0], new[] { cycle }, 1, 1);

            Assert.That(secondCommit.PreventedEventIds, Is.EqualTo(new[] { "a" }));
            Assert.That(store.HasPendingOccurrences, Is.False);
        }

        [Test]
        public void RootSchedule_UsesNegativeSequenceAndDoesNotCollideWithChildren()
        {
            var store = new PlayableHuntEventOccurrenceStore();
            EventData first = CreateEvent("first");
            EventData second = CreateEvent("second");

            Assert.That(store.TryScheduleRoot(first, Vector2Int.zero, 1, 1, out PlayableHuntEventOccurrence firstOccurrence), Is.True);
            Assert.That(store.TryScheduleRoot(second, Vector2Int.one, 1, 1, out PlayableHuntEventOccurrence secondOccurrence), Is.True);
            Assert.That(firstOccurrence.Sequence, Is.LessThan(0));
            Assert.That(secondOccurrence.Sequence, Is.LessThan(0));
            Assert.That(firstOccurrence.Sequence, Is.Not.EqualTo(secondOccurrence.Sequence));
        }

        [Test]
        public void CaptureAndRestore_PreservesPendingIdentityCoordinateAndNextRootSequence()
        {
            var source = new PlayableHuntEventOccurrenceStore();
            EventData first = CreateEvent("restore-first");
            EventData second = CreateEvent("restore-second");
            Assert.That(source.TryScheduleRoot(first, new Vector2Int(2, -1), 4, 101, out PlayableHuntEventOccurrence original), Is.True);
            PlayableHuntEventOccurrenceStoreState state = source.CaptureState();

            Assert.That(PlayableHuntEventOccurrenceStore.TryRestore(state, id => id == first.ContentId ? first : id == second.ContentId ? second : null, out PlayableHuntEventOccurrenceStore restored, out string reason), Is.True, reason);
            Assert.That(restored.TryGetNextPending(out PlayableHuntEventOccurrence pending), Is.True);
            Assert.That(pending.Sequence, Is.EqualTo(original.Sequence));
            Assert.That(pending.EventId, Is.EqualTo(first.ContentId));
            Assert.That(pending.Coordinate, Is.EqualTo(new Vector2Int(2, -1)));
            Assert.That(restored.TryScheduleRoot(second, Vector2Int.zero, 4, 101, out PlayableHuntEventOccurrence next), Is.True);
            Assert.That(next.Sequence, Is.EqualTo(-2));
        }

        [Test]
        public void TryRestore_RejectsOversizedPendingStateWithoutResolvingContent()
        {
            var records = new List<PlayableHuntEventOccurrenceRecord>();
            for (int index = 1; index <= 65; index++)
            {
                var occurrence = new PlayableEventChainOccurrence(index, $"event-{index}", $"event-{index}", 1, 1);
                records.Add(new PlayableHuntEventOccurrenceRecord(occurrence, Vector2Int.zero, Array.Empty<string>()));
            }
            var state = new PlayableHuntEventOccurrenceStoreState { PendingOccurrences = records };
            int resolveCount = 0;

            bool restored = PlayableHuntEventOccurrenceStore.TryRestore(state, _ =>
            {
                resolveCount++;
                return null;
            }, out PlayableHuntEventOccurrenceStore store, out string reason);

            Assert.That(restored, Is.False);
            Assert.That(store, Is.Null);
            Assert.That(reason, Does.Contain("上限"));
            Assert.That(resolveCount, Is.Zero);
            Assert.That(state.PendingOccurrences, Has.Count.EqualTo(65));
        }

        [Test]
        public void Commit_ReportsOverflowOnceAndAllowsAcceptedOccurrencesToDrain()
        {
            var store = new PlayableHuntEventOccurrenceStore();
            EventData root = CreateEvent("overflow-root");
            EventData child = CreateEvent("overflow-child");
            Assert.That(store.TryScheduleRoot(root, Vector2Int.zero, 1, 1, out PlayableHuntEventOccurrence parent), Is.True);

            PlayableHuntEventOccurrenceCommitResult overflow = store.Commit(parent, Enumerable.Repeat(child, 65).ToArray(), 1, 1);
            PlayableHuntEventOccurrenceCommitResult next = store.Commit(overflow.AppendedOccurrences[0], Array.Empty<EventData>(), 1, 1);

            Assert.That(overflow.Succeeded, Is.True);
            Assert.That(overflow.Diagnostic, Does.Contain("上限"));
            Assert.That(overflow.TruncatedChildCount, Is.EqualTo(1));
            Assert.That(overflow.AppendedOccurrences, Has.Count.EqualTo(64));
            Assert.That(next.Succeeded, Is.True);
            Assert.That(store.HasPendingOccurrences, Is.True);
        }

        [Test]
        public async Task ConfirmResultFailure_CommitsParentOnceAndResumesChildBeforeRetreat()
        {
            var input = new RetryInput(false);
            using var rig = new SessionRig(input);
            HuntTileCommandResult first = await rig.Session.InteractTileAsync(rig.Interactable.AxialCoord);

            Assert.That(first.Succeeded, Is.False);
            Assert.That(input.RootChoiceCount, Is.EqualTo(1));
            Assert.That(rig.Session.HasPendingEventOccurrences, Is.True);

            HuntRetreatCommandResult retreat = await rig.Session.PrepareRetreatAsync(1);

            Assert.That(retreat.Succeeded, Is.True, retreat.Reason);
            Assert.That(input.RootChoiceCount, Is.EqualTo(1));
            Assert.That(input.ChildNarrativeCount, Is.EqualTo(1));
            Assert.That(rig.Session.HasPendingEventOccurrences, Is.False);
        }

        [Test]
        public async Task PendingEventFailure_BlocksRetreatAndKeepsOccurrence()
        {
            var input = new RetryInput(true);
            using var rig = new SessionRig(input);
            HuntTileCommandResult first = await rig.Session.InteractTileAsync(rig.Interactable.AxialCoord);

            Assert.That(first.Succeeded, Is.False);
            HuntRetreatCommandResult retreat = await rig.Session.PrepareRetreatAsync(1);

            Assert.That(retreat.Succeeded, Is.False);
            Assert.That(rig.Session.HasPendingEventOccurrences, Is.True);
            Assert.That(input.RootChoiceCount, Is.EqualTo(1));
            Assert.That(input.ChildNarrativeCount, Is.EqualTo(1));
        }

        [Test]
        public async Task InputGuard_BlocksBeforePendingEventRecovery()
        {
            var input = new RetryInput(false);
            using var rig = new SessionRig(input);
            HuntTileCommandResult first = await rig.Session.InteractTileAsync(rig.Interactable.AxialCoord);
            Assert.That(first.Succeeded, Is.False);
            Assert.That(rig.Session.HasPendingEventOccurrences, Is.True);

            const int ownerId = 482071;
            PlayableHuntInputGuard.Acquire(ownerId);
            try
            {
                HuntTileCommandResult blocked = await rig.Session.InteractTileAsync(rig.Interactable.AxialCoord);

                Assert.That(blocked.Succeeded, Is.False);
                Assert.That(blocked.Reason, Does.Contain("锁定"));
                Assert.That(rig.Session.HasPendingEventOccurrences, Is.True);
                Assert.That(input.ChildNarrativeCount, Is.Zero, "输入锁生效时不得先恢复并消费 pending event。");
            }
            finally
            {
                PlayableHuntInputGuard.Release(ownerId);
            }

            HuntRetreatCommandResult resumed = await rig.Session.PrepareRetreatAsync(1);
            Assert.That(resumed.Succeeded, Is.True, resumed.Reason);
            Assert.That(input.ChildNarrativeCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ResumePendingEvents_DrainsRepeatedSiblingsBeforeRetreat()
        {
            var input = new RetryInput(false);
            using var rig = new SessionRig(input);
            rig.ConfigureRepeatedChildren();
            HuntTileCommandResult first = await rig.Session.InteractTileAsync(rig.Interactable.AxialCoord);

            Assert.That(first.Succeeded, Is.False);
            Assert.That(rig.Session.HasPendingEventOccurrences, Is.True);

            HuntRetreatCommandResult retreat = await rig.Session.PrepareRetreatAsync(1);

            Assert.That(retreat.Succeeded, Is.True, retreat.Reason);
            Assert.That(input.RootChoiceCount, Is.EqualTo(1));
            Assert.That(input.ChildNarrativeCount, Is.EqualTo(2));
            Assert.That(rig.Session.HasPendingEventOccurrences, Is.False);
        }

        [Test]
        public async Task ResumePendingEvent_UsesFrozenActorInsteadOfCurrentSelection()
        {
            var input = new RetryInput(false);
            using var rig = new SessionRig(input);
            HuntTileCommandResult first = await rig.Session.InteractTileAsync(rig.Interactable.AxialCoord);
            rig.Manager.SelectHunter(rig.SecondHunter.InstanceId);

            HuntRetreatCommandResult retreat = await rig.Session.PrepareRetreatAsync(1);

            Assert.That(first.Succeeded, Is.False);
            Assert.That(retreat.Succeeded, Is.True, retreat.Reason);
            Assert.That(input.NarrativeActorIds, Is.EqualTo(new[] { rig.Hunter.InstanceId }));
        }

        [Test]
        public async Task ResumePendingEvent_FallsBackWhenFrozenActorIsDead()
        {
            var input = new RetryInput(false);
            using var rig = new SessionRig(input);
            HuntTileCommandResult first = await rig.Session.InteractTileAsync(rig.Interactable.AxialCoord);
            rig.Hunter.IsAlive = false;

            HuntRetreatCommandResult retreat = await rig.Session.PrepareRetreatAsync(1);

            Assert.That(first.Succeeded, Is.False);
            Assert.That(retreat.Succeeded, Is.True, retreat.Reason);
            Assert.That(input.NarrativeActorIds, Is.EqualTo(new[] { rig.SecondHunter.InstanceId }));
        }

        private EventData CreateEvent(string id)
        {
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = id;
            gameEvent.ConfigureContentId(id);
            events.Add(gameEvent);
            return gameEvent;
        }

        private sealed class SessionRig : IDisposable
        {
            private readonly HunterData hunterData;
            private readonly HunterData secondHunterData;
            private readonly HexTileData startingTile;
            private readonly HexTileData plainTile;
            private readonly EventData root;
            private readonly EventData child;

            public SessionRig(RetryInput input)
            {
                hunterData = ScriptableObject.CreateInstance<HunterData>();
                hunterData.hunterName = "测试猎人";
                Hunter = new HunterInstance(hunterData);
                secondHunterData = ScriptableObject.CreateInstance<HunterData>();
                secondHunterData.hunterName = "候补猎人";
                SecondHunter = new HunterInstance(secondHunterData);
                startingTile = ScriptableObject.CreateInstance<HexTileData>();
                startingTile.name = "occurrence-starting-tile";
                startingTile.tileType = TileType.Starting;
                startingTile.tileName = "起点";
                plainTile = ScriptableObject.CreateInstance<HexTileData>();
                plainTile.name = "occurrence-plain-tile";
                plainTile.tileType = TileType.Plains;
                plainTile.tileName = "测试平原";
                root = ScriptableObject.CreateInstance<EventData>();
                root.ConfigureContentId("occurrence-root");
                root.eventType = GameEventType.Choice;
                child = ScriptableObject.CreateInstance<EventData>();
                child.ConfigureContentId("occurrence-child");
                root.options.Add(new EventOption { optionText = "继续", successChain = new List<EventData> { child } });
                plainTile.tileRevealEvent = root;
                Settlement = new SettlementInstance();
                Settlement.Hunters.Add(Hunter);
                Settlement.Hunters.Add(SecondHunter);
                EventSystem = new EventSystem(Settlement, new FirstRandom());
                Manager = new HuntManager(EventSystem, 17) { StartingTileConfig = startingTile, TilePool = { plainTile } };
                Manager.EventInput = input;
                Manager.OnEnter(new List<HunterInstance> { Hunter, SecondHunter });
                Session = new PlayableHuntActionSession(Manager);
                input.Hunter = Hunter;
            }

            public HunterInstance Hunter { get; }
            public HunterInstance SecondHunter { get; }
            public SettlementInstance Settlement { get; }
            public EventSystem EventSystem { get; }
            public HuntManager Manager { get; }
            public PlayableHuntActionSession Session { get; }
            public HexTileInstance Interactable => Manager.Map[Manager.Map.Keys.First(coordinate => Manager.Map[coordinate].State == TileState.Interactable && ReferenceEquals(Manager.Map[coordinate].Config, plainTile))];

            public void ConfigureRepeatedChildren() => root.options[0].successChain = new List<EventData> { child, child };

            public void Dispose()
            {
                Session.Dispose();
                UnityEngine.Object.DestroyImmediate(hunterData);
                UnityEngine.Object.DestroyImmediate(secondHunterData);
                UnityEngine.Object.DestroyImmediate(startingTile);
                UnityEngine.Object.DestroyImmediate(plainTile);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(child);
            }
        }

        private sealed class RetryInput : IPlayableEventInput
        {
            private readonly bool failChild;
            private bool failResult = true;

            public RetryInput(bool failChild) => this.failChild = failChild;
            public HunterInstance Hunter { get; set; }
            public int RootChoiceCount { get; private set; }
            public int ChildNarrativeCount { get; private set; }
            public List<int> NarrativeActorIds { get; } = new();

            public UniTask ConfirmNarrativeAsync(EventData gameEvent, HunterInstance actor, CancellationToken cancellationToken)
            {
                ChildNarrativeCount++;
                NarrativeActorIds.Add(actor?.InstanceId ?? 0);
                return failChild ? UniTask.FromException(new InvalidOperationException("测试待恢复事件失败")) : UniTask.CompletedTask;
            }

            public UniTask<PlayableEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, CancellationToken cancellationToken)
            {
                RootChoiceCount++;
                return UniTask.FromResult(new PlayableEventChoiceSelection(0, Hunter));
            }

            public UniTask<PlayableEventCheckDecision> PresentCheckAsync(PlayableEventChoiceTransaction transaction, CancellationToken cancellationToken) => UniTask.FromResult(PlayableEventCheckDecision.Accept);

            public UniTask ConfirmResultAsync(EventData gameEvent, EventResolutionResult result, CancellationToken cancellationToken)
            {
                if (failResult)
                {
                    failResult = false;
                    return UniTask.FromException(new InvalidOperationException("测试结果确认失败"));
                }
                return UniTask.CompletedTask;
            }
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
