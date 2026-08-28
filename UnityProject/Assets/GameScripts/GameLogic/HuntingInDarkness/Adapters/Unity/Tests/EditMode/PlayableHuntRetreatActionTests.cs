using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Core;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunt;
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
                received.CollectedItems[0].ItemId = "被订阅者修改";
                receivedCount++;
            };
            EventBus.Subscribe(handler);
            try
            {
                HuntRetreatCommandResult result = await rig.Session.PrepareRetreatAsync(rig.Settlement.CurrentYear);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.Record.Year, Is.EqualTo(3));
                Assert.That(result.Record.RecordId, Is.Not.Empty);
                Assert.That(result.Record.ReturnSchemaVersion, Is.EqualTo(HuntRecord.CurrentReturnSchemaVersion));
                Assert.That(result.Record.ParticipantHunterIds, Is.EqualTo(new[] { 7101, 7102 }));
                Assert.That(result.Record.HuntersDeployed, Is.EqualTo(2));
                Assert.That(result.Record.HuntersLost, Is.EqualTo(1));
                Assert.That(result.Record.CollectedResources, Is.Empty);
                Assert.That(result.Record.CollectedItems.Select(item => (item.ItemId, item.Count)), Is.EqualTo(new[] { ("暗石", 1) }));
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
        public async Task PrepareRetreatAsync_RemoteWithMaterialsRequiresAbandonment()
        {
            using var rig = new RetreatRig();
            rig.MoveAwayFromCamp();

            HuntRetreatCommandResult result = await rig.Session.PrepareRetreatAsync(rig.Settlement.CurrentYear);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Is.EqualTo("远离营地且携带物品时，必须选择放弃一份物品。"));
            Assert.That(rig.Survivor.Collectibles, Has.Count.EqualTo(1));
            Assert.That(rig.Survivor.Collectibles[0].Count, Is.EqualTo(1));
        }

        [Test]
        public async Task PrepareRetreatAsync_RemoteSelectionOnlyChangesRecordSnapshot()
        {
            using var rig = new RetreatRig();
            rig.MoveAwayFromCamp();

            HuntRetreatCommandResult result = await rig.Session.PrepareRetreatAsync(rig.Settlement.CurrentYear, new HuntRetreatDecision(rig.Resource.ContentId));

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(result.Record.CollectedItems, Is.Empty);
            Assert.That(rig.Survivor.Collectibles, Has.Count.EqualTo(1));
            Assert.That(rig.Survivor.Collectibles[0].Data.ContentId, Is.EqualTo(rig.Resource.ContentId));
            Assert.That(rig.Survivor.Collectibles[0].Count, Is.EqualTo(1));
        }

        [Test]
        public async Task PrepareRetreatAsync_RemoteRejectsForgedAbandonmentId()
        {
            using var rig = new RetreatRig();
            rig.MoveAwayFromCamp();

            HuntRetreatCommandResult result = await rig.Session.PrepareRetreatAsync(rig.Settlement.CurrentYear, new HuntRetreatDecision("forged.resource"));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Is.EqualTo("选择的放弃物品已不在当前小队携带物中。"));
            Assert.That(rig.Survivor.Collectibles[0].Count, Is.EqualTo(1));
        }

        [Test]
        public async Task PrepareRetreatAsync_RemoteEmptyHandsAllowReturnWithoutSelection()
        {
            using var rig = new RetreatRig();
            rig.Survivor.Collectibles.Clear();
            rig.MoveAwayFromCamp();

            HuntRetreatCommandResult result = await rig.Session.PrepareRetreatAsync(rig.Settlement.CurrentYear);

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(result.Record.CollectedItems, Is.Empty);
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

            public void MoveAwayFromCamp()
            {
                KeyValuePair<Vector2Int, HexTileInstance> target = Manager.Map.First(pair => pair.Key != Manager.CampPosition && pair.Value.State == TileState.Interactable);
                Assert.That(Manager.TryCommitTileInteraction(target.Key, HuntTileInteractionKind.Reveal, out _), Is.True);
                Assert.That(Manager.TryCommitTileInteraction(target.Key, HuntTileInteractionKind.Move, out _), Is.True);
                Assert.That(Manager.IsSquadAtCamp, Is.False);
            }

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
