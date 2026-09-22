using System;
using System.Collections.Generic;
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
    public sealed class PlayableHuntHarvestActionSessionTests
    {
        [Test]
        public async Task HarvestFlow_PublishesPreparedRevealAndCommitFactsInOrder()
        {
            using var rig = new HuntRig(drawCount: 2);
            var received = new List<string>();
            Action<HarvestPreparedEvent> preparedHandler = evt => received.Add($"prepared:{evt.CardCount}");
            Action<HarvestCardRevealedEvent> revealHandler = evt => received.Add($"reveal:{evt.CardIndex}");
            Action<HarvestCommittedEvent> commitHandler = evt => received.Add($"commit:{evt.ObtainedCount}");
            EventBus.Subscribe(preparedHandler);
            EventBus.Subscribe(revealHandler);
            EventBus.Subscribe(commitHandler);
            try
            {
                rig.Session.Reactors.RegisterGlobal(new HarvestTermsReactor(drawCount: 2, hitChance: 1f));

                PlayableHarvestTransaction transaction = await rig.Session.PrepareHarvestAsync(rig.Point);
                PlayableHarvestStepResult first = await rig.Session.AdvanceHarvestAsync(transaction);
                PlayableHarvestStepResult second = await rig.Session.AdvanceHarvestAsync(transaction);

                Assert.That(first.Succeeded, Is.True);
                Assert.That(first.IsCompleted, Is.False);
                Assert.That(second.Succeeded, Is.True);
                Assert.That(second.IsCompleted, Is.True);
                Assert.That(second.Obtained, Has.Count.EqualTo(2));
                Assert.That(rig.Point.IsExhausted, Is.True);
                Assert.That(rig.Hunter.Collectibles, Has.Count.EqualTo(2));
                Assert.That(received, Is.EqualTo(new[] { "prepared:2", "reveal:0", "reveal:1", "commit:2" }));
            }
            finally
            {
                EventBus.Unsubscribe(preparedHandler);
                EventBus.Unsubscribe(revealHandler);
                EventBus.Unsubscribe(commitHandler);
            }
        }

        [Test]
        public async Task BeginReactor_PreventionDoesNotReserveResourcePoint()
        {
            using var rig = new HuntRig(drawCount: 1);
            IDisposable prevention = rig.Session.Reactors.RegisterGlobal(new PreventBeginHarvestReactor());

            PlayableHarvestTransaction blocked = await rig.Session.PrepareHarvestAsync(rig.Point);
            prevention.Dispose();
            PlayableHarvestTransaction retry = await rig.Session.PrepareHarvestAsync(rig.Point);

            Assert.That(blocked, Is.Null);
            Assert.That(retry, Is.Not.Null);
            Assert.That(rig.Point.IsExhausted, Is.False);
        }

        [Test]
        public async Task CommitReactor_PreventionKeepsLastRevealAndAllowsCommitRetry()
        {
            using var rig = new HuntRig(drawCount: 1);
            rig.Session.Reactors.RegisterGlobal(new HarvestTermsReactor(drawCount: 1, hitChance: 1f));
            IDisposable prevention = rig.Session.Reactors.RegisterGlobal(new PreventCommitHarvestReactor());
            PlayableHarvestTransaction transaction = await rig.Session.PrepareHarvestAsync(rig.Point);

            PlayableHarvestStepResult blocked = await rig.Session.AdvanceHarvestAsync(transaction);
            prevention.Dispose();
            PlayableHarvestStepResult retry = await rig.Session.AdvanceHarvestAsync(transaction);

            Assert.That(blocked.Succeeded, Is.False);
            Assert.That(blocked.HasRevealedCard, Is.True);
            Assert.That(transaction.RevealedCount, Is.EqualTo(1));
            Assert.That(rig.Point.IsExhausted, Is.True);
            Assert.That(retry.Succeeded, Is.True);
            Assert.That(retry.IsCompleted, Is.True);
            Assert.That(retry.HasRevealedCard, Is.False);
            Assert.That(rig.Hunter.Collectibles, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task SessionDispose_AbandonsRevealedReservationForDiscardedHunt()
        {
            var rig = new HuntRig(drawCount: 2);
            PlayableHarvestTransaction transaction = await rig.Session.PrepareHarvestAsync(rig.Point);
            await rig.Session.AdvanceHarvestAsync(transaction);

            rig.Session.Dispose();
            PlayableHarvestTransaction nextHunt = rig.Manager.Resources.PrepareHarvest(rig.Point, rig.Hunter);

            Assert.That(nextHunt, Is.Not.Null);
            Assert.That(rig.Point.IsExhausted, Is.False);
            nextHunt.Cancel();
            rig.Dispose();
        }

        [Test]
        public async Task ZeroCardPlan_CommitsEmptyHarvestWithoutReveal()
        {
            using var rig = new HuntRig(drawCount: 0);
            PlayableHarvestTransaction transaction = await rig.Session.PrepareHarvestAsync(rig.Point);

            PlayableHarvestStepResult result = await rig.Session.AdvanceHarvestAsync(transaction);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.IsCompleted, Is.True);
            Assert.That(result.HasRevealedCard, Is.False);
            Assert.That(result.Obtained, Is.Empty);
            Assert.That(rig.Point.IsExhausted, Is.True);
        }

        [Test]
        public async Task PrepareHarvestAsync_RejectsPointOutsideCurrentRevealedMap()
        {
            using var rig = new HuntRig(drawCount: 1);
            var foreignPoint = new ResourcePointInstance
            {
                ResourceName = rig.Point.ResourceName,
                Resource = rig.Point.Resource,
                DrawCount = 1
            };

            PlayableHarvestTransaction result = await rig.Session.PrepareHarvestAsync(foreignPoint);

            Assert.That(result, Is.Null);
            Assert.That(foreignPoint.IsExhausted, Is.False);
        }

        [Test]
        public async Task PrepareHarvestAsync_RejectsLostSelectedHunterWithoutReservingPoint()
        {
            using var rig = new HuntRig(drawCount: 1);
            rig.Hunter.IsAlive = false;

            PlayableHarvestTransaction result = await rig.Session.PrepareHarvestAsync(rig.Point);

            Assert.That(result, Is.Null);
            Assert.That(rig.Point.IsExhausted, Is.False);
            Assert.That(rig.Manager.SelectedHunter, Is.Null);
        }

        [Test]
        public async Task AdvanceHarvestAsync_HunterLostMidHarvestReleasesReservationAndEndsTransaction()
        {
            using var rig = new HuntRig(drawCount: 2);
            PlayableHarvestTransaction transaction = await rig.Session.PrepareHarvestAsync(rig.Point);
            PlayableHarvestStepResult first = await rig.Session.AdvanceHarvestAsync(transaction);
            rig.Hunter.IsAlive = false;

            PlayableHarvestStepResult result = await rig.Session.AdvanceHarvestAsync(transaction);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Does.Contain("失去行动能力"));
            Assert.That(transaction.IsCancelled, Is.True);
            Assert.That(rig.Session.HasActiveHarvest, Is.False);
            Assert.That(rig.Point.IsExhausted, Is.False);
            Assert.That(rig.Hunter.Collectibles, Is.Empty);
        }

        [Test]
        public async Task PrepareHarvestAsync_RequiresSquadPresenceOnResourceTile()
        {
            using var rig = new HuntRig(drawCount: 1);
            HexTileInstance resourceTile = null;
            foreach (HexTileInstance tile in rig.Manager.Map.Values)
                if (tile.State == TileState.Interactable)
                {
                    resourceTile = tile;
                    break;
                }
            Assert.That(resourceTile, Is.Not.Null);
            rig.Manager.Map[Vector2Int.zero].ResourcePoints.Remove(rig.Point);
            resourceTile.State = TileState.Revealed;
            resourceTile.ResourcePoints.Add(rig.Point);
            int selectionCount = 0;
            rig.Manager.OnResourcePointClicked += (_, _) => selectionCount++;

            rig.Manager.OnResourcePointSelected(resourceTile.AxialCoord, 0);
            PlayableHarvestTransaction remote = await rig.Session.PrepareHarvestAsync(rig.Point);
            HuntTileCommandResult movement = await rig.Session.InteractTileAsync(resourceTile.AxialCoord);
            rig.Manager.OnResourcePointSelected(resourceTile.AxialCoord, 0);
            PlayableHarvestTransaction arrived = await rig.Session.PrepareHarvestAsync(rig.Point);

            Assert.That(remote, Is.Null);
            Assert.That(movement.Succeeded, Is.True);
            Assert.That(rig.Manager.SquadPosition, Is.EqualTo(resourceTile.AxialCoord));
            Assert.That(selectionCount, Is.EqualTo(1));
            Assert.That(arrived, Is.Not.Null);
        }

        [Test]
        public async Task InteractTileAsync_RejectsMapInteractionWhileHarvestIsActive()
        {
            using var rig = new HuntRig(drawCount: 1);
            HexTileInstance target = null;
            foreach (HexTileInstance tile in rig.Manager.Map.Values)
                if (tile.State == TileState.Interactable)
                {
                    target = tile;
                    break;
                }
            Assert.That(target, Is.Not.Null);

            PlayableHarvestTransaction transaction = await rig.Session.PrepareHarvestAsync(rig.Point);
            HuntTileCommandResult interaction = await rig.Session.InteractTileAsync(target.AxialCoord);

            Assert.That(transaction, Is.Not.Null);
            Assert.That(interaction.Succeeded, Is.False);
            Assert.That(target.State, Is.EqualTo(TileState.Interactable));
            Assert.That(rig.Manager.SquadPosition, Is.EqualTo(Vector2Int.zero));
        }

        private sealed class HuntRig : IDisposable
        {
            private readonly ItemData resource;

            public HuntRig(int drawCount)
            {
                resource = ScriptableObject.CreateInstance<ItemData>();
                resource.itemName = "队列素材";
                Hunter = new HunterInstance(null, 41) { Name = "采集者" };
                var eventSystem = new EventSystem(new SettlementInstance(), new FirstRandom());
                Manager = new HuntManager(eventSystem, seed: 23);
                Manager.OnEnter(new List<HunterInstance> { Hunter });
                Point = new ResourcePointInstance
                {
                    ResourceName = resource.itemName,
                    Resource = resource,
                    DrawCount = drawCount
                };
                Manager.Map[Vector2Int.zero].ResourcePoints.Add(Point);
                Session = new PlayableHuntActionSession(Manager);
            }

            public HunterInstance Hunter { get; }
            public HuntManager Manager { get; }
            public ResourcePointInstance Point { get; }
            public PlayableHuntActionSession Session { get; }

            public void Dispose()
            {
                Session.Dispose();
                UnityEngine.Object.DestroyImmediate(resource);
            }
        }

        private sealed class HarvestTermsReactor : GameActionReactor<BeginHarvestAction>
        {
            private readonly int drawCount;
            private readonly float hitChance;

            public HarvestTermsReactor(int drawCount, float hitChance)
            {
                this.drawCount = drawCount;
                this.hitChance = hitChance;
            }

            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;

            protected override void React(BeginHarvestAction action, ReactionContext context, ReactionResponse response)
            {
                action.SetDrawCount(drawCount);
                action.SetHitChance(hitChance);
            }
        }

        private sealed class PreventBeginHarvestReactor : GameActionReactor<BeginHarvestAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(BeginHarvestAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试阻止准备采集");
        }

        private sealed class PreventCommitHarvestReactor : GameActionReactor<CommitHarvestAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(CommitHarvestAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试阻止采集提交");
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
