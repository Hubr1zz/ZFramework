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
    public sealed class PlayableHuntRetreatActionTests
    {
        [Test]
        public async Task PrepareRetreatAsync_PublishesSnapshotWithoutTransferringResources()
        {
            using var rig = new RetreatRig();
            HuntRetreatPreparedEvent received = default;
            int receivedCount = 0;
            Action<HuntRetreatPreparedEvent> handler = evt =>
            {
                received = evt;
                received.CollectedResources[0] = "被订阅者修改";
                receivedCount++;
            };
            EventBus.Subscribe(handler);
            try
            {
                HuntRetreatCommandResult result = await rig.Session.PrepareRetreatAsync(rig.Settlement.CurrentYear);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.Record.Year, Is.EqualTo(3));
                Assert.That(result.Record.HuntersDeployed, Is.EqualTo(2));
                Assert.That(result.Record.HuntersLost, Is.EqualTo(1));
                Assert.That(result.Record.CollectedResources, Is.EqualTo(new[] { "暗石" }));
                Assert.That(receivedCount, Is.EqualTo(1));
                Assert.That(received.HuntersDeployed, Is.EqualTo(2));
                Assert.That(rig.Settlement.GetResource("暗石"), Is.Zero);
                Assert.That(rig.Survivor.Collectibles, Has.Count.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task PrepareRetreatAsync_ReactorCanPreventPreparation()
        {
            using var rig = new RetreatRig();
            int receivedCount = 0;
            Action<HuntRetreatPreparedEvent> handler = _ => receivedCount++;
            EventBus.Subscribe(handler);
            try
            {
                rig.Session.Reactors.RegisterGlobal(new PreventRetreatReactor());

                HuntRetreatCommandResult result = await rig.Session.PrepareRetreatAsync(rig.Settlement.CurrentYear);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Reason, Is.EqualTo("风暴阻断了归途"));
                Assert.That(receivedCount, Is.Zero);
                Assert.That(rig.Survivor.Collectibles, Has.Count.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task PrepareRetreatAsync_CancelledHarvestNoLongerBlocksReturn()
        {
            using var rig = new RetreatRig();
            HexTileInstance startingTile = rig.Manager.Map[Vector2Int.zero];
            var point = new ResourcePointInstance
            {
                ResourceName = rig.Resource.itemName,
                Resource = rig.Resource,
                DrawCount = 1
            };
            startingTile.ResourcePoints.Add(point);
            PlayableHarvestTransaction transaction = await rig.Session.PrepareHarvestAsync(point);
            Assert.That(transaction, Is.Not.Null);
            Assert.That(transaction.Cancel(), Is.True);

            HuntRetreatCommandResult result = await rig.Session.PrepareRetreatAsync(rig.Settlement.CurrentYear);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(rig.Session.HasActiveHarvest, Is.False);
        }

        [Test]
        public async Task PrepareRetreatAsync_UnresolvedHarvestBlocksReturn()
        {
            using var rig = new RetreatRig();
            HexTileInstance startingTile = rig.Manager.Map[Vector2Int.zero];
            var point = new ResourcePointInstance
            {
                ResourceName = rig.Resource.itemName,
                Resource = rig.Resource,
                DrawCount = 1
            };
            startingTile.ResourcePoints.Add(point);
            PlayableHarvestTransaction transaction = await rig.Session.PrepareHarvestAsync(point);
            Assert.That(transaction, Is.Not.Null);

            HuntRetreatCommandResult result = await rig.Session.PrepareRetreatAsync(rig.Settlement.CurrentYear);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Is.EqualTo("请先完成或离开当前资源采集。"));
            Assert.That(rig.Session.HasActiveHarvest, Is.True);
        }

        private sealed class RetreatRig : IDisposable
        {
            private readonly HexTileData startingTile;
            private readonly HexTileData plainTile;

            public RetreatRig()
            {
                Resource = ScriptableObject.CreateInstance<ItemData>();
                Resource.itemName = "暗石";
                Survivor = new HunterInstance(null, 7101) { Name = "归途者", IsAlive = true };
                Survivor.Collectibles.Add(new ItemInstance(Resource));
                var lostHunter = new HunterInstance(null, 7102) { Name = "失踪者", IsAlive = false };
                Settlement = new SettlementInstance { CurrentYear = 3 };
                var eventSystem = new EventSystem(Settlement, new FirstRandom());
                startingTile = ScriptableObject.CreateInstance<HexTileData>();
                startingTile.tileType = TileType.Starting;
                startingTile.tileName = "营火起点";
                plainTile = ScriptableObject.CreateInstance<HexTileData>();
                plainTile.tileType = TileType.Plains;
                plainTile.tileName = "荒地";
                Manager = new HuntManager(eventSystem, 29)
                {
                    StartingTileConfig = startingTile,
                    TilePool = { plainTile }
                };
                Manager.OnEnter(new List<HunterInstance> { Survivor, lostHunter }, Settlement.CurrentYear);
                Session = new PlayableHuntActionSession(Manager);
            }

            public ItemData Resource { get; }
            public HunterInstance Survivor { get; }
            public SettlementInstance Settlement { get; }
            public HuntManager Manager { get; }
            public PlayableHuntActionSession Session { get; }

            public void Dispose()
            {
                Session.Dispose();
                UnityEngine.Object.DestroyImmediate(plainTile);
                UnityEngine.Object.DestroyImmediate(startingTile);
                UnityEngine.Object.DestroyImmediate(Resource);
            }
        }

        private sealed class PreventRetreatReactor : GameActionReactor<PrepareHuntRetreatAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(PrepareHuntRetreatAction action, ReactionContext context, ReactionResponse response) => response.Prevent("风暴阻断了归途");
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
