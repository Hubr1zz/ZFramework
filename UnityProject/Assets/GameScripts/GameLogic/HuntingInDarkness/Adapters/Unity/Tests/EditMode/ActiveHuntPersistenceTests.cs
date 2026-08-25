using System.Collections.Generic;
using System.Reflection;
using Core;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.Bootstrap;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class ActiveHuntPersistenceTests
    {
        private const string SettingsPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Resources/HuntingInDarkness/PlayableBootstrapSettings.asset";
        private readonly List<Object> createdAssets = new();
        private PlayableHuntDestination selectedDestination;

        [SetUp]
        public void SetUp()
        {
            ResetContentAssembly();
            PlayableBootstrapSettings settings = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);
            PlayableSymptomRuntime.Configure(settings.Symptoms);
            Assert.That(PlayableCampaignContentAssembler.TryBuild(settings, out PlayableCampaignContentCandidate candidate, out PlayableContentDiagnosticReport buildReport), Is.True, buildReport.ToString());
            Assert.That(PlayableCampaignContentAssembler.Install(candidate, out PlayableContentDiagnosticReport installReport), Is.True, installReport.ToString());
            selectedDestination = settings.HuntDestinations.GetAvailable(3)[0];
            Assert.That(PlayableHuntDestinationRuntime.TrySelect(selectedDestination, 3, out string reason), Is.True, reason);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in createdAssets)
                if (asset != null)
                    Object.DestroyImmediate(asset);
            createdAssets.Clear();
            ResetContentAssembly();
        }

        [Test]
        public void Snapshot_RoundTripsMapRosterPositionAndRandomState()
        {
            HunterData template = CreateAsset<HunterData>("hunter-template");
            var hunter = new HunterInstance(template, 101);
            hunter.HP.arms = 1;
            var settlement = new SettlementInstance { CurrentYear = 3 };
            settlement.Hunters.Add(hunter);
            HexTileData starting = CreateTile("starting", TileType.Starting);
            HexTileData plain = CreateTile("plain", TileType.Plains);
            var source = CreateManager(settlement, starting, plain, 17);
            source.OnEnter(new List<HunterInstance> { hunter }, settlement.CurrentYear);
            using var session = new PlayableHuntActionSession(source, "encounter", "destination");

            Assert.That(ActiveHuntSnapshotAdapter.TryCapture(settlement, source, session, "expedition-1", out CampaignSnapshot captured, out string reason), Is.True, reason);
            Assert.That(SaveLoadSystem.TryCreatePayload(captured, out string payload, out reason), Is.True, reason);
            Assert.That(CampaignSaveRecovery.TryRestore(new CampaignSaveCandidates(CampaignSaveCodec.Encode(payload), null), out CampaignSnapshot saved, out _, out reason), Is.True, reason);
            var destination = CreateManager(saved.Settlement, starting, plain, 99);

            Assert.That(ActiveHuntSnapshotAdapter.TryRestore(saved, destination, out PlayableHuntRuntimeState runtime, out PlayableHuntEventOccurrenceStore occurrences, out reason), Is.True, reason);
            Assert.That(destination.TryRestore(runtime, out reason), Is.True, reason);
            Assert.That(destination.CurrentYear, Is.EqualTo(3));
            Assert.That(destination.ActiveHunters, Has.Count.EqualTo(1));
            Assert.That(destination.ActiveHunters[0].InstanceId, Is.EqualTo(101));
            Assert.That(destination.ActiveHunters[0].HP.arms, Is.EqualTo(1));
            Assert.That(destination.Map.Count, Is.EqualTo(source.Map.Count));
            Assert.That(destination.SquadPosition, Is.EqualTo(source.SquadPosition));
            Assert.That(destination.CaptureRandomState().Value, Is.EqualTo(source.CaptureRandomState().Value));
            Assert.That(occurrences.HasPendingOccurrences, Is.False);
            Assert.That(captured.ActiveHunt.DestinationId, Is.EqualTo(source.BoundRoute.DestinationId));
            Assert.That(captured.ActiveHunt.ContentBundleId, Is.EqualTo(source.ContentBundleId));

            saved.ActiveHunt.EncounterHandoffPending = true;
            saved.ActiveHunt.EncounterId = "boss-handoff";
            Assert.That(ActiveHuntSnapshotAdapter.TryRestore(saved, destination, out _, out _, out reason), Is.False);
            Assert.That(reason, Does.Contain("遭遇交接"));
        }

        [Test]
        public void Restore_RejectsDestinationOrBundleMismatchBeforeMutatingCollectibles()
        {
            HunterData template = CreateAsset<HunterData>("hunter-template-mismatch");
            var hunter = new HunterInstance(template, 201);
            var settlement = new SettlementInstance { CurrentYear = 3 };
            settlement.Hunters.Add(hunter);
            HexTileData starting = CreateTile("starting-mismatch", TileType.Starting);
            HexTileData plain = CreateTile("plain-mismatch", TileType.Plains);
            var source = CreateManager(settlement, starting, plain, 17);
            source.OnEnter(new List<HunterInstance> { hunter }, settlement.CurrentYear);
            using var session = new PlayableHuntActionSession(source, "encounter", source.BoundRoute.DestinationId);
            Assert.That(ActiveHuntSnapshotAdapter.TryCapture(settlement, source, session, "expedition-mismatch", out CampaignSnapshot captured, out string reason), Is.True, reason);
            var destination = CreateManager(settlement, starting, plain, 99);
            captured.ActiveHunt.DestinationId = "route:not-current";
            Assert.That(ActiveHuntSnapshotAdapter.TryRestore(captured, destination, out _, out _, out reason), Is.False);
            Assert.That(reason, Does.Contain("目的地或内容 Bundle"));
            captured.ActiveHunt.DestinationId = source.BoundRoute.DestinationId;
            captured.ActiveHunt.ContentBundleId = "bundle:not-current";
            Assert.That(ActiveHuntSnapshotAdapter.TryRestore(captured, destination, out _, out _, out reason), Is.False);
            Assert.That(reason, Does.Contain("目的地或内容 Bundle"));
            Assert.That(hunter.Collectibles, Is.Empty);
        }

        [Test]
        public void RestoreRouteResolver_RequiresExactCurrentBundleWithoutPublishingSelection()
        {
            PlayableHuntDestination beforeDestination = PlayableHuntDestinationRuntime.ActiveDestination;
            PlayableHuntRoutePlan beforeRoute = PlayableHuntDestinationRuntime.ActiveRoutePlan;
            PlayableHuntContentBundle currentBundle = PlayableHuntContentRuntime.CurrentBundle;

            Assert.That(PlayableHuntDestinationRuntime.TryResolveRouteForRestore(selectedDestination.DestinationId, 3, currentBundle.BundleId, out PlayableHuntRoutePlan route, out string reason), Is.True, reason);
            Assert.That(route.ContentBundleId, Is.EqualTo(currentBundle.BundleId));
            Assert.That(PlayableHuntDestinationRuntime.TryResolveRouteForRestore(string.Empty, 3, currentBundle.BundleId, out PlayableHuntRoutePlan defaultRoute, out reason), Is.True, reason);
            Assert.That(defaultRoute.DestinationId, Is.Empty);
            Assert.That(PlayableHuntDestinationRuntime.TryResolveRouteForRestore(selectedDestination.DestinationId, 3, "bundle:stale", out _, out reason), Is.False);
            Assert.That(reason, Does.Contain("不兼容"));
            Assert.That(PlayableHuntDestinationRuntime.ActiveDestination, Is.SameAs(beforeDestination));
            Assert.That(PlayableHuntDestinationRuntime.ActiveRoutePlan, Is.SameAs(beforeRoute));
        }

        [Test]
        public void Capture_RejectsSameIdObjectsFromAnotherContentGeneration()
        {
            HunterData template = CreateAsset<HunterData>("hunter-template-generation");
            var hunter = new HunterInstance(template, 301);
            var settlement = new SettlementInstance { CurrentYear = 3 };
            settlement.Hunters.Add(hunter);
            var manager = CreateManager(settlement, null, null, 31);
            manager.OnEnter(new List<HunterInstance> { hunter }, settlement.CurrentYear);
            using var session = new PlayableHuntActionSession(manager, "encounter", manager.BoundRoute.DestinationId);

            settlement.Hunters[0] = new HunterInstance(template, hunter.InstanceId);
            Assert.That(ActiveHuntSnapshotAdapter.TryCapture(settlement, manager, session, "expedition-generation", out _, out string reason), Is.False);
            Assert.That(reason, Does.Contain("营地世代"));

            settlement.Hunters[0] = hunter;
            HexTileInstance tile = manager.Map[manager.SquadPosition];
            HexTileData canonicalTile = tile.Config;
            HexTileData foreignTile = CreateTile(canonicalTile.ContentId, canonicalTile.tileType);
            tile.Config = foreignTile;
            Assert.That(ActiveHuntSnapshotAdapter.TryCapture(settlement, manager, session, "expedition-generation", out _, out reason), Is.False);
            Assert.That(reason, Does.Contain("路线世代"));
        }

        private HuntManager CreateManager(SettlementInstance settlement, HexTileData starting, HexTileData plain, int seed)
        {
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            var manager = new HuntManager(eventSystem, seed, false);
            Assert.That(PlayableHuntDestinationRuntime.TryApplyTo(manager, out string reason), Is.True, reason);
            return manager;
        }

        private static void ResetContentAssembly()
        {
            InvokeReset(typeof(PlayableCampaignContentAssembler));
            InvokeReset(typeof(PlayableHuntDestinationRuntime));
            InvokeReset(typeof(PlayableHuntContentRuntime));
            InvokeReset(typeof(PlayableSettlementContentRuntime));
            PlayableEventTableRuntime.ClearCache();
        }

        private static void InvokeReset(System.Type type)
        {
            MethodInfo method = type.GetMethod("ResetRuntimeState", BindingFlags.Static | BindingFlags.NonPublic);
            method?.Invoke(null, null);
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
