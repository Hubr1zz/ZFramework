using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Core;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.Bootstrap;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class ActiveHuntPersistenceTests
    {
        private const string SettingsPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Resources/HuntingInDarkness/PlayableBootstrapSettings.asset";
        private readonly List<UnityEngine.Object> createdAssets = new();
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
            foreach (UnityEngine.Object asset in createdAssets)
                if (asset != null)
                    UnityEngine.Object.DestroyImmediate(asset);
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
            settlement.PendingHuntNoiseLease = new PendingHuntNoiseLease { LeaseId = "hunt-noise:stone_vigil_risk", SourceEventId = "stone_vigil_risk", NoiseModifier = 2 };
            settlement.Hunters.Add(hunter);
            HexTileData starting = CreateTile("starting", TileType.Starting);
            HexTileData plain = CreateTile("plain", TileType.Plains);
            var source = CreateManager(settlement, starting, plain, 17);
            source.OnEnter(new List<HunterInstance> { hunter }, settlement.CurrentYear);
            ItemData resource = PlayableSettlementItemRegistry.Items.First(item => item != null && item.itemType == ItemType.Resource);
            source.Map[source.SquadPosition].ResourcePoints.Add(new ResourcePointInstance
            {
                ResourcePointId = "persistence-point",
                ResourceName = resource.itemName,
                Resource = resource,
                MaterialPool = new List<ItemData> { resource },
                DrawCount = 1,
                IsExhausted = true
            });
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
            Assert.That(destination.Map[destination.SquadPosition].ResourcePoints, Has.Count.EqualTo(1));
            Assert.That(destination.Map[destination.SquadPosition].ResourcePoints[0].IsExhausted, Is.True);
            Assert.That(occurrences.HasPendingOccurrences, Is.False);
            Assert.That(captured.ActiveHunt.DestinationId, Is.EqualTo(source.BoundRoute.DestinationId));
            Assert.That(captured.ActiveHunt.ContentBundleId, Is.EqualTo(source.ContentBundleId));
            Assert.That(saved.Settlement.PendingHuntNoiseLease, Is.Not.Null);
            Assert.That(saved.Settlement.PendingHuntNoiseLease.LeaseId, Is.EqualTo("hunt-noise:stone_vigil_risk"));
            Assert.That(saved.Settlement.PendingHuntNoiseLease.NoiseModifier, Is.EqualTo(2));

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

        [Test]
        public void Snapshot_RoundTripsPendingTriggeredEventAndRejectsForeignEventOrAncestor()
        {
            HunterData template = CreateAsset<HunterData>("hunter-template-triggered");
            var hunter = new HunterInstance(template, 401);
            var settlement = new SettlementInstance { CurrentYear = 3 };
            settlement.Hunters.Add(hunter);
            HuntManager source = CreateManager(settlement, null, null, 41);
            source.OnEnter(new List<HunterInstance> { hunter }, settlement.CurrentYear);
            Assert.That(source.BoundRoute.TryResolveEvent("hunt_rust_burial_open_eyes", out EventData child), Is.True);
            var occurrences = new PlayableHuntEventOccurrenceStore();
            Assert.That(occurrences.TryScheduleRoot(child, source.SquadPosition, settlement.CurrentYear, hunter.InstanceId, out _), Is.True);
            using var session = new PlayableHuntActionSession(source, "encounter", source.BoundRoute.DestinationId, restoredOccurrenceStore: occurrences);

            Assert.That(ActiveHuntSnapshotAdapter.TryCapture(settlement, source, session, "expedition-triggered", out CampaignSnapshot captured, out string reason), Is.True, reason);
            Assert.That(SaveLoadSystem.TryCreatePayload(captured, out string payload, out reason), Is.True, reason);
            Assert.That(CampaignSaveRecovery.TryRestore(new CampaignSaveCandidates(CampaignSaveCodec.Encode(payload), null), out CampaignSnapshot saved, out _, out reason), Is.True, reason);
            HuntManager destination = CreateManager(saved.Settlement, null, null, 99);

            Assert.That(ActiveHuntSnapshotAdapter.TryRestore(saved, destination, out _, out PlayableHuntEventOccurrenceStore restored, out reason), Is.True, reason);
            Assert.That(restored.TryGetNextPending(out PlayableHuntEventOccurrence pending), Is.True);
            Assert.That(pending.Event.ContentId, Is.EqualTo(child.ContentId));

            saved.ActiveHunt.EventStore.PendingOccurrences[0].EventId = "hunt:foreign-event";
            Assert.That(ActiveHuntSnapshotAdapter.TryRestore(saved, destination, out _, out _, out reason), Is.False);
            Assert.That(reason, Does.Contain("无法解析待恢复狩猎事件"));
            saved.ActiveHunt.EventStore.PendingOccurrences[0].EventId = child.ContentId;
            saved.ActiveHunt.EventStore.PendingOccurrences[0].AncestorEventIds.Add("hunt:foreign-ancestor");
            Assert.That(ActiveHuntSnapshotAdapter.TryRestore(saved, destination, out _, out _, out reason), Is.False);
            Assert.That(reason, Does.Contain("ancestor"));
        }

        [Test]
        public void Snapshot_RoundTripsRepeatedSiblingOccurrencesBySequenceAndOrder()
        {
            HunterData template = CreateAsset<HunterData>("hunter-template-repeated-siblings");
            var hunter = new HunterInstance(template, 403);
            var settlement = new SettlementInstance { CurrentYear = 3 };
            settlement.Hunters.Add(hunter);
            HuntManager source = CreateManager(settlement, null, null, 43);
            source.OnEnter(new List<HunterInstance> { hunter }, settlement.CurrentYear);
            Assert.That(source.BoundRoute.TryResolveEvent("hunt_rust_burial", out EventData parent), Is.True);
            Assert.That(source.BoundRoute.TryResolveEvent("hunt_rust_burial_open_eyes", out EventData child), Is.True);
            var occurrences = new PlayableHuntEventOccurrenceStore();
            Assert.That(occurrences.TryScheduleRoot(parent, source.SquadPosition, settlement.CurrentYear, hunter.InstanceId, out PlayableHuntEventOccurrence root), Is.True);
            PlayableHuntEventOccurrenceCommitResult committed = occurrences.Commit(root, new[] { child, child }, settlement.CurrentYear, hunter.InstanceId);
            Assert.That(committed.AppendedOccurrences, Has.Count.EqualTo(2));
            int firstSequence = committed.AppendedOccurrences[0].Sequence;
            int secondSequence = committed.AppendedOccurrences[1].Sequence;
            using var session = new PlayableHuntActionSession(source, "encounter", source.BoundRoute.DestinationId, restoredOccurrenceStore: occurrences);

            Assert.That(ActiveHuntSnapshotAdapter.TryCapture(settlement, source, session, "expedition-repeated-siblings", out CampaignSnapshot captured, out string reason), Is.True, reason);
            Assert.That(SaveLoadSystem.TryCreatePayload(captured, out string payload, out reason), Is.True, reason);
            Assert.That(CampaignSaveRecovery.TryRestore(new CampaignSaveCandidates(CampaignSaveCodec.Encode(payload), null), out CampaignSnapshot saved, out _, out reason), Is.True, reason);
            Assert.That(saved.ActiveHunt.EventStore.PendingOccurrences, Has.Count.EqualTo(2));
            Assert.That(saved.ActiveHunt.EventStore.PendingOccurrences[0].EventId, Is.EqualTo(child.ContentId));
            Assert.That(saved.ActiveHunt.EventStore.PendingOccurrences[1].EventId, Is.EqualTo(child.ContentId));
            Assert.That(saved.ActiveHunt.EventStore.PendingOccurrences[0].Sequence, Is.EqualTo(firstSequence));
            Assert.That(saved.ActiveHunt.EventStore.PendingOccurrences[1].Sequence, Is.EqualTo(secondSequence));

            HuntManager destination = CreateManager(saved.Settlement, null, null, 44);
            Assert.That(ActiveHuntSnapshotAdapter.TryRestore(saved, destination, out _, out PlayableHuntEventOccurrenceStore restored, out reason), Is.True, reason);
            PlayableHuntEventOccurrenceStoreState restoredState = restored.CaptureState();
            Assert.That(restoredState.PendingOccurrences, Has.Count.EqualTo(2));
            Assert.That(restoredState.PendingOccurrences[0].Occurrence.Sequence, Is.EqualTo(firstSequence));
            Assert.That(restoredState.PendingOccurrences[1].Occurrence.Sequence, Is.EqualTo(secondSequence));
            Assert.That(restoredState.PendingOccurrences[0].Occurrence.EventId, Is.EqualTo(child.ContentId));
            Assert.That(restoredState.PendingOccurrences[1].Occurrence.EventId, Is.EqualTo(child.ContentId));
        }

        [Test]
        public void EventStoreRestore_RejectsDuplicatePendingSequence()
        {
            EventData gameEvent = CreateAsset<EventData>("duplicate-pending-event");
            gameEvent.ConfigureContentId("hunt:duplicate-pending-event");
            var records = new List<PlayableHuntEventOccurrenceRecord>
            {
                new(new PlayableEventChainOccurrence(7, gameEvent.ContentId, gameEvent.eventName, 3, 403), Vector2Int.zero, new[] { "hunt_rust_burial" }),
                new(new PlayableEventChainOccurrence(7, gameEvent.ContentId, gameEvent.eventName, 3, 403), Vector2Int.one, new[] { "hunt_rust_burial" })
            };
            var state = new PlayableHuntEventOccurrenceStoreState { PendingOccurrences = records };

            Assert.That(PlayableHuntEventOccurrenceStore.TryRestore(state, id => id == gameEvent.ContentId ? gameEvent : null, out PlayableHuntEventOccurrenceStore restored, out string reason), Is.False);
            Assert.That(restored, Is.Null);
            Assert.That(reason, Does.Contain("重复 occurrence 序号"));
        }

        [Test]
        public void EventStoreRestore_RejectsPendingSequenceCommittedInSameCheckpoint()
        {
            EventData gameEvent = CreateAsset<EventData>("committed-pending-event");
            gameEvent.ConfigureContentId("hunt:committed-pending-event");
            var records = new List<PlayableHuntEventOccurrenceRecord>
            {
                new(new PlayableEventChainOccurrence(9, gameEvent.ContentId, gameEvent.eventName, 3, 404), Vector2Int.zero, new[] { "hunt_rust_burial" })
            };
            var state = new PlayableHuntEventOccurrenceStoreState { CommittedSequences = new[] { 9 }, PendingOccurrences = records };

            Assert.That(PlayableHuntEventOccurrenceStore.TryRestore(state, id => id == gameEvent.ContentId ? gameEvent : null, out PlayableHuntEventOccurrenceStore restored, out string reason), Is.False);
            Assert.That(restored, Is.Null);
            Assert.That(reason, Does.Contain("pending 与 committed"));
        }

        [Test]
        public void EventStoreRestore_RejectsZeroAndDuplicateCommittedSequences()
        {
            EventData gameEvent = CreateAsset<EventData>("invalid-committed-event");
            gameEvent.ConfigureContentId("hunt:invalid-committed-event");
            var zeroState = new PlayableHuntEventOccurrenceStoreState { CommittedSequences = new[] { 0 } };
            Assert.That(PlayableHuntEventOccurrenceStore.TryRestore(zeroState, id => id == gameEvent.ContentId ? gameEvent : null, out _, out string zeroReason), Is.False);
            Assert.That(zeroReason, Does.Contain("committed occurrence 序号 0"));

            var duplicateState = new PlayableHuntEventOccurrenceStoreState { CommittedSequences = new[] { 5, 5 } };
            Assert.That(PlayableHuntEventOccurrenceStore.TryRestore(duplicateState, id => id == gameEvent.ContentId ? gameEvent : null, out _, out string duplicateReason), Is.False);
            Assert.That(duplicateReason, Does.Contain("重复 committed occurrence 序号"));
        }

        [Test]
        public void EventStoreRestore_RejectsInvalidPendingSequenceBoundsAndCursors()
        {
            EventData gameEvent = CreateAsset<EventData>("invalid-pending-event");
            gameEvent.ConfigureContentId("hunt:invalid-pending-event");
            static PlayableHuntEventOccurrenceRecord Record(int sequence, EventData content) => new(new PlayableEventChainOccurrence(sequence, content.ContentId, content.eventName, 3, 405), Vector2Int.zero, new string[0]);

            var zeroState = new PlayableHuntEventOccurrenceStoreState { PendingOccurrences = new[] { Record(0, gameEvent) } };
            Assert.That(PlayableHuntEventOccurrenceStore.TryRestore(zeroState, id => id == gameEvent.ContentId ? gameEvent : null, out _, out string zeroReason), Is.False);
            Assert.That(zeroReason, Does.Contain("序号为 0"));

            var maxState = new PlayableHuntEventOccurrenceStoreState { NextSequence = int.MaxValue, PendingOccurrences = new[] { Record(int.MaxValue, gameEvent) } };
            Assert.That(PlayableHuntEventOccurrenceStore.TryRestore(maxState, id => id == gameEvent.ContentId ? gameEvent : null, out _, out string maxReason), Is.False);
            Assert.That(maxReason, Does.Contain("int.MaxValue"));

            var minState = new PlayableHuntEventOccurrenceStoreState { NextRootSequence = int.MinValue, PendingOccurrences = new[] { Record(int.MinValue, gameEvent) } };
            Assert.That(PlayableHuntEventOccurrenceStore.TryRestore(minState, id => id == gameEvent.ContentId ? gameEvent : null, out _, out string minReason), Is.False);
            Assert.That(minReason, Does.Contain("int.MinValue"));

            var committedMaxState = new PlayableHuntEventOccurrenceStoreState { NextSequence = int.MaxValue, CommittedSequences = new[] { int.MaxValue } };
            Assert.That(PlayableHuntEventOccurrenceStore.TryRestore(committedMaxState, id => id == gameEvent.ContentId ? gameEvent : null, out _, out string committedMaxReason), Is.False);
            Assert.That(committedMaxReason, Does.Contain("committed occurrence"));

            var committedMinState = new PlayableHuntEventOccurrenceStoreState { NextRootSequence = int.MinValue, CommittedSequences = new[] { int.MinValue } };
            Assert.That(PlayableHuntEventOccurrenceStore.TryRestore(committedMinState, id => id == gameEvent.ContentId ? gameEvent : null, out _, out string committedMinReason), Is.False);
            Assert.That(committedMinReason, Does.Contain("committed occurrence"));

            var positiveCursorState = new PlayableHuntEventOccurrenceStoreState { NextSequence = 3, PendingOccurrences = new[] { Record(3, gameEvent) } };
            Assert.That(PlayableHuntEventOccurrenceStore.TryRestore(positiveCursorState, id => id == gameEvent.ContentId ? gameEvent : null, out _, out string positiveReason), Is.False);
            Assert.That(positiveReason, Does.Contain("NextSequence"));

            var negativeCursorState = new PlayableHuntEventOccurrenceStoreState { NextRootSequence = -2, PendingOccurrences = new[] { Record(-2, gameEvent) } };
            Assert.That(PlayableHuntEventOccurrenceStore.TryRestore(negativeCursorState, id => id == gameEvent.ContentId ? gameEvent : null, out _, out string negativeReason), Is.False);
            Assert.That(negativeReason, Does.Contain("NextRootSequence"));

            var negativeRootReuseState = new PlayableHuntEventOccurrenceStoreState { NextRootSequence = -1, PendingOccurrences = new[] { Record(-1, gameEvent) } };
            Assert.That(PlayableHuntEventOccurrenceStore.TryRestore(negativeRootReuseState, id => id == gameEvent.ContentId ? gameEvent : null, out _, out string negativeRootReuseReason), Is.False);
            Assert.That(negativeRootReuseReason, Does.Contain("NextRootSequence"));
        }

        [Test]
        public async Task ProductionRustBurial_ResumesThroughHuntSessionAndPaysMetalCollectible()
        {
            HunterData template = CreateAsset<HunterData>("hunter-template-rust-burial-resume");
            var hunter = new HunterInstance(template, 406) { Understanding = 7 };
            var settlement = new SettlementInstance { CurrentYear = 3 };
            settlement.Hunters.Add(hunter);
            HuntManager source = CreateManager(settlement, null, null, 46);
            source.OnEnter(new List<HunterInstance> { hunter }, settlement.CurrentYear);
            Assert.That(source.BoundRoute.TryResolveEvent("hunt_rust_burial", out EventData parent), Is.True);
            Assert.That(source.BoundRoute.TryResolveEvent("hunt_rust_burial_open_eyes", out EventData child), Is.True);
            HexTileInstance target = source.Map.Values.First(tile => tile.State == TileState.Interactable && !tile.HasBossEncounter && tile.Config?.tileRevealEvent == null);
            EventData originalRevealEvent = target.Config.tileRevealEvent;
            target.Config.tileRevealEvent = parent;
            var sourceInput = new ProductionRustBurialInput(hunter, true);
            source.EventInput = sourceInput;
            Assert.That(settlement.GetResource("metal_fragment"), Is.Zero);
            try
            {
                using var sourceSession = new PlayableHuntActionSession(source, "encounter", source.BoundRoute.DestinationId, restoredOccurrenceStore: new PlayableHuntEventOccurrenceStore());
                HuntTileCommandResult first = await sourceSession.InteractTileAsync(target.AxialCoord);

                Assert.That(first.Succeeded, Is.False);
                Assert.That(sourceSession.HasPendingEventOccurrences, Is.True);
                Assert.That(sourceInput.ParentChoiceCount, Is.EqualTo(1));
                Assert.That(sourceInput.ChildChoiceCount, Is.Zero);
                Assert.That(hunter.Collectibles.Sum(item => item?.Data?.ContentId == "metal_fragment" ? item.Count : 0), Is.EqualTo(1));
                Assert.That(settlement.GetResource("metal_fragment"), Is.Zero);
                Assert.That(ActiveHuntSnapshotAdapter.TryCapture(settlement, source, sourceSession, "expedition-rust-burial-resume", out CampaignSnapshot captured, out string reason), Is.True, reason);
                Assert.That(SaveLoadSystem.TryCreatePayload(captured, out string payload, out reason), Is.True, reason);
                Assert.That(CampaignSaveRecovery.TryRestore(new CampaignSaveCandidates(CampaignSaveCodec.Encode(payload), null), out CampaignSnapshot saved, out _, out reason), Is.True, reason);

                HuntManager destination = CreateManager(saved.Settlement, null, null, 47);
                Assert.That(ActiveHuntSnapshotAdapter.TryRestore(saved, destination, out PlayableHuntRuntimeState runtime, out PlayableHuntEventOccurrenceStore restoredOccurrences, out reason), Is.True, reason);
                Assert.That(destination.TryRestore(runtime, out reason), Is.True, reason);
                var destinationInput = new ProductionRustBurialInput(destination.ActiveHunters[0], false);
                destination.EventInput = destinationInput;
                using var destinationSession = new PlayableHuntActionSession(destination, "encounter", destination.BoundRoute.DestinationId, restoredOccurrenceStore: restoredOccurrences);
                HuntRetreatCommandResult retreat = await destinationSession.PrepareRetreatAsync(3);

                Assert.That(retreat.Succeeded, Is.True, retreat.Reason);
                Assert.That(destinationInput.ParentChoiceCount, Is.Zero);
                Assert.That(destinationInput.ChildChoiceCount, Is.EqualTo(1));
                Assert.That(destinationInput.ResourceScope, Is.EqualTo(PlayableEventResourceScope.HuntCollectibles));
                Assert.That(destinationInput.ChildMetalAmount, Is.EqualTo(1));
                Assert.That(destination.ActiveHunters[0].Collectibles.Sum(item => item?.Data?.ContentId == "metal_fragment" ? item.Count : 0), Is.Zero);
                Assert.That(saved.Settlement.GetResource("metal_fragment"), Is.Zero);
                Assert.That(destinationSession.HasPendingEventOccurrences, Is.False);
            }
            finally
            {
                target.Config.tileRevealEvent = originalRevealEvent;
            }
        }

        [Test]
        public void Capture_RejectsPendingEventOutsideBoundRoute()
        {
            HunterData template = CreateAsset<HunterData>("hunter-template-foreign-event");
            var hunter = new HunterInstance(template, 402);
            var settlement = new SettlementInstance { CurrentYear = 3 };
            settlement.Hunters.Add(hunter);
            HuntManager manager = CreateManager(settlement, null, null, 42);
            manager.OnEnter(new List<HunterInstance> { hunter }, settlement.CurrentYear);
            EventData foreign = CreateAsset<EventData>("foreign-event");
            foreign.ConfigureContentId("hunt:foreign-event");
            var occurrences = new PlayableHuntEventOccurrenceStore();
            Assert.That(occurrences.TryScheduleRoot(foreign, manager.SquadPosition, settlement.CurrentYear, hunter.InstanceId, out _), Is.True);
            using var session = new PlayableHuntActionSession(manager, "encounter", manager.BoundRoute.DestinationId, restoredOccurrenceStore: occurrences);

            Assert.That(ActiveHuntSnapshotAdapter.TryCapture(settlement, manager, session, "expedition-foreign-event", out _, out string reason), Is.False);
            Assert.That(reason, Does.Contain("不属于当前路线"));
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

        private sealed class ProductionRustBurialInput : IPlayableEventInput
        {
            private readonly HunterInstance hunter;
            private bool failFirstResult;

            public ProductionRustBurialInput(HunterInstance hunter, bool failFirstResult)
            {
                this.hunter = hunter;
                this.failFirstResult = failFirstResult;
            }

            public int ParentChoiceCount { get; private set; }
            public int ChildChoiceCount { get; private set; }
            public PlayableEventResourceScope ResourceScope { get; private set; }
            public int ChildMetalAmount { get; private set; }

            public UniTask ConfirmNarrativeAsync(EventData gameEvent, HunterInstance actor, CancellationToken cancellationToken) => UniTask.CompletedTask;

            public UniTask<PlayableEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, IPlayableEventResourceAvailability resourceAvailability, CancellationToken cancellationToken)
            {
                if (gameEvent.ContentId == "hunt_rust_burial")
                {
                    ParentChoiceCount++;
                    return UniTask.FromResult(new PlayableEventChoiceSelection(1, hunter));
                }
                ChildChoiceCount++;
                ResourceScope = resourceAvailability.Scope;
                ChildMetalAmount = resourceAvailability.GetAvailableAmount("metal_fragment");
                return UniTask.FromResult(new PlayableEventChoiceSelection(1, hunter));
            }

            public UniTask<PlayableEventCheckDecision> PresentCheckAsync(PlayableEventChoiceTransaction transaction, CancellationToken cancellationToken) => UniTask.FromResult(PlayableEventCheckDecision.Accept);

            public UniTask ConfirmResultAsync(EventData gameEvent, EventResolutionResult result, CancellationToken cancellationToken)
            {
                if (!failFirstResult) return UniTask.CompletedTask;
                failFirstResult = false;
                return UniTask.FromException(new InvalidOperationException("测试中断父事件结果确认以保留子 occurrence"));
            }
        }
    }
}
