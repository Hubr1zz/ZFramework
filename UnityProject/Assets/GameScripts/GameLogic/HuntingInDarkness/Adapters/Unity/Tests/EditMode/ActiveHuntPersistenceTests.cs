using System.Collections.Generic;
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
    public sealed class ActiveHuntPersistenceTests
    {
        private readonly List<Object> createdAssets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in createdAssets)
                if (asset != null)
                    Object.DestroyImmediate(asset);
            createdAssets.Clear();
        }

        [Test]
        public void Snapshot_RoundTripsMapRosterPositionAndRandomState()
        {
            HunterData template = CreateAsset<HunterData>("hunter-template");
            var hunter = new HunterInstance(template, 101);
            var settlement = new SettlementInstance { CurrentYear = 3 };
            settlement.Hunters.Add(hunter);
            HexTileData starting = CreateTile("starting", TileType.Starting);
            HexTileData plain = CreateTile("plain", TileType.Plains);
            var source = CreateManager(settlement, starting, plain, 17);
            source.OnEnter(new List<HunterInstance> { hunter }, settlement.CurrentYear);
            using var session = new PlayableHuntActionSession(source, "encounter", "destination");

            Assert.That(ActiveHuntSnapshotAdapter.TryCapture(settlement, source, session, "expedition-1", "destination", out CampaignSnapshot captured, out string reason), Is.True, reason);
            Assert.That(SaveLoadSystem.TryCreatePayload(captured, out string payload, out reason), Is.True, reason);
            Assert.That(CampaignSaveRecovery.TryRestore(new CampaignSaveCandidates(CampaignSaveCodec.Encode(payload), null), out CampaignSnapshot saved, out _, out reason), Is.True, reason);
            var destination = CreateManager(saved.Settlement, starting, plain, 99);

            Assert.That(ActiveHuntSnapshotAdapter.TryRestore(saved, destination, out PlayableHuntRuntimeState runtime, out PlayableHuntEventOccurrenceStore occurrences, out reason), Is.True, reason);
            Assert.That(destination.TryRestore(runtime, out reason), Is.True, reason);
            Assert.That(destination.CurrentYear, Is.EqualTo(3));
            Assert.That(destination.ActiveHunters, Has.Count.EqualTo(1));
            Assert.That(destination.ActiveHunters[0].InstanceId, Is.EqualTo(101));
            Assert.That(destination.Map.Count, Is.EqualTo(source.Map.Count));
            Assert.That(destination.SquadPosition, Is.EqualTo(source.SquadPosition));
            Assert.That(destination.CaptureRandomState().Value, Is.EqualTo(source.CaptureRandomState().Value));
            Assert.That(occurrences.HasPendingOccurrences, Is.False);

            saved.ActiveHunt.EncounterHandoffPending = true;
            saved.ActiveHunt.EncounterId = "boss-handoff";
            Assert.That(ActiveHuntSnapshotAdapter.TryRestore(saved, destination, out _, out _, out reason), Is.False);
            Assert.That(reason, Does.Contain("遭遇交接"));
        }

        private HuntManager CreateManager(SettlementInstance settlement, HexTileData starting, HexTileData plain, int seed)
        {
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            return new HuntManager(eventSystem, seed) { StartingTileConfig = starting, TilePool = new List<HexTileData> { plain } };
        }

        private HexTileData CreateTile(string id, TileType type)
        {
            HexTileData tile = CreateAsset<HexTileData>(id);
            tile.ConfigureContentId(id);
            tile.tileType = type;
            tile.tileName = id;
            return tile;
        }

        private T CreateAsset<T>(string name) where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            createdAssets.Add(asset);
            return asset;
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
