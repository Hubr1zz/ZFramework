using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Core;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableHuntActorSelectionTests
    {
        [Test]
        public async Task SelectActorAsync_CommitsFactAndCheckpointOnlyWhenSelectionChanges()
        {
            using var rig = new SelectionRig();
            int factCount = 0;
            HuntActorSelectionCommittedEvent received = default;
            Action<HuntActorSelectionCommittedEvent> handler = evt =>
            {
                factCount++;
                received = evt;
            };
            EventBus.Subscribe(handler);
            try
            {
                HuntActorSelectionResult changed = await rig.Session.SelectActorAsync(rig.Second.InstanceId);
                HuntActorSelectionResult repeated = await rig.Session.SelectActorAsync(rig.Second.InstanceId);

                Assert.That(changed.Succeeded, Is.True, changed.Reason);
                Assert.That(changed.Changed, Is.True);
                Assert.That(changed.PreviousHunterId, Is.EqualTo(rig.First.InstanceId));
                Assert.That(changed.SelectedHunterId, Is.EqualTo(rig.Second.InstanceId));
                Assert.That(repeated.Succeeded, Is.True, repeated.Reason);
                Assert.That(repeated.Changed, Is.False);
                Assert.That(rig.Manager.SelectedHunter, Is.SameAs(rig.Second));
                Assert.That(factCount, Is.EqualTo(1));
                Assert.That(received.SessionId, Is.EqualTo(rig.Session.SessionId));
                Assert.That(rig.CheckpointCount, Is.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task SelectActorAsync_RejectsInvalidDeadPreventedAndReturnLockedWithoutMutation()
        {
            using var rig = new SelectionRig();
            rig.Second.IsAlive = false;
            HuntActorSelectionResult dead = await rig.Session.SelectActorAsync(rig.Second.InstanceId);
            HuntActorSelectionResult unknown = await rig.Session.SelectActorAsync(999999);
            rig.Second.IsAlive = true;
            using IDisposable prevention = rig.Session.Reactors.RegisterGlobal(new PreventSelectionReactor());
            HuntActorSelectionResult prevented = await rig.Session.SelectActorAsync(rig.Second.InstanceId);
            prevention.Dispose();
            rig.Session.SetReturnCheckpointLock(true);
            HuntActorSelectionResult locked = await rig.Session.SelectActorAsync(rig.Second.InstanceId);

            Assert.That(dead.Succeeded, Is.False);
            Assert.That(unknown.Succeeded, Is.False);
            Assert.That(prevented.Succeeded, Is.False);
            Assert.That(prevented.Reason, Is.EqualTo("测试阻止行动猎人选择"));
            Assert.That(locked.Succeeded, Is.False);
            Assert.That(rig.Manager.SelectedHunter, Is.SameAs(rig.First));
            Assert.That(rig.CheckpointCount, Is.Zero);
        }

        [Test]
        public async Task SelectActorAsync_CancelledBeforeExecutionLeavesSelectionUntouched()
        {
            using var rig = new SelectionRig();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            HuntActorSelectionResult result = await rig.Session.SelectActorAsync(rig.Second.InstanceId, cancellation.Token);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(rig.Manager.SelectedHunter, Is.SameAs(rig.First));
            Assert.That(rig.CheckpointCount, Is.Zero);
        }

        [Test]
        public async Task ExplorationPort_RejectsSelectionAfterSessionLeaseExpires()
        {
            using var rig = new SelectionRig();
            var runtime = new HuntExplorationRuntime(rig.Manager, rig.Session);
            IHuntExplorationPort stalePort = runtime.Port;
            rig.Session.Dispose();

            HuntActorSelectionResult result = await stalePort.SubmitActorSelectionAsync(rig.Second.InstanceId);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Does.Contain("没有可用"));
            Assert.That(rig.Manager.SelectedHunter, Is.SameAs(rig.First));
        }

        [Test]
        public async Task ExplorationPort_RejectsSelectionWhenOwningRuntimeGenerationIsDetached()
        {
            using var rig = new SelectionRig();
            bool isCurrent = true;
            var runtime = new HuntExplorationRuntime(rig.Manager, rig.Session, () => isCurrent);
            IHuntExplorationPort stalePort = runtime.Port;
            isCurrent = false;

            HuntActorSelectionResult result = await stalePort.SubmitActorSelectionAsync(rig.Second.InstanceId);

            Assert.That(rig.Session.IsActive, Is.True);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Does.Contain("没有可用"));
            Assert.That(rig.Manager.SelectedHunter, Is.SameAs(rig.First));
        }

        private sealed class SelectionRig : IDisposable
        {
            private readonly HexTileData startingTile;
            private readonly HexTileData plainTile;

            public SelectionRig()
            {
                startingTile = ScriptableObject.CreateInstance<HexTileData>();
                startingTile.name = "actor-selection-start";
                startingTile.tileType = TileType.Starting;
                startingTile.tileName = "起点";
                plainTile = ScriptableObject.CreateInstance<HexTileData>();
                plainTile.name = "actor-selection-plain";
                plainTile.tileType = TileType.Plains;
                plainTile.tileName = "荒地";
                First = new HunterInstance(null, 7101) { Name = "先行者" };
                Second = new HunterInstance(null, 7102) { Name = "守望者" };
                var settlement = new SettlementInstance();
                settlement.Hunters.Add(First);
                settlement.Hunters.Add(Second);
                Manager = new HuntManager(new EventSystem(settlement, new FirstRandom()), seed: 31)
                {
                    StartingTileConfig = startingTile,
                    TilePool = new List<HexTileData> { plainTile }
                };
                Manager.OnEnter(new List<HunterInstance> { First, Second });
                Session = new PlayableHuntActionSession(Manager, checkpointCommitted: () => CheckpointCount++);
            }

            public HunterInstance First { get; }
            public HunterInstance Second { get; }
            public HuntManager Manager { get; }
            public PlayableHuntActionSession Session { get; }
            public int CheckpointCount { get; private set; }

            public void Dispose()
            {
                Session.Dispose();
                UnityEngine.Object.DestroyImmediate(plainTile);
                UnityEngine.Object.DestroyImmediate(startingTile);
            }
        }

        private sealed class PreventSelectionReactor : GameActionReactor<SelectHuntActorAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(SelectHuntActorAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试阻止行动猎人选择");
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
