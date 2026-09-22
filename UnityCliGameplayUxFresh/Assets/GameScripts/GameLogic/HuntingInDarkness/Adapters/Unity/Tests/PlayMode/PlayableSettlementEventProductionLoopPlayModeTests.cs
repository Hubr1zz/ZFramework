using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Core;
using Cards3D;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Bootstrap;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.ViewLayer.Hunt;
using HuntingInDarkness.ViewLayer.Settlement;
using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;
using UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class PlayableSettlementEventProductionLoopPlayModeTests
    {
        private const int FrameTimeout = 600;
        private static readonly FieldInfo squadGridField = typeof(TabletopHuntDeparturePanel3D).GetField("squadGrid", BindingFlags.Instance | BindingFlags.NonPublic);
        private GameObject managerObject;
        private PlayableBootstrapSettings settings;
        private PlayableCampaignContentCandidate contentCandidate;
        private HexTileData patchedTileConfig;
        private EventData originalPatchedTileRevealEvent;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ResetContentAssembly();
            settings = PlayableContentSourcePlayModeAssets.LoadBundle()?.Settings;
            Assert.That(settings, Is.Not.Null);
            PlayableSymptomRuntime.Configure(settings.Symptoms);
            Assert.That(PlayableCampaignContentAssembler.TryBuild(PlayableContentSourcePlayModeAssets.LoadBundle(), out contentCandidate, out PlayableContentDiagnosticReport buildReport), Is.True, buildReport.ToString());
            Assert.That(PlayableCampaignContentAssembler.Install(contentCandidate, out PlayableContentDiagnosticReport installReport), Is.True, installReport.ToString());
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RestorePatchedTile();
            if (managerObject != null)
                UnityEngine.Object.Destroy(managerObject);
            yield return null;
            ResetContentAssembly();
        }

        [UnityTest]
        public IEnumerator SafeChoice_CompletesPhysicalEventDepartureReturnAndContinueLoop()
        {
            var persistence = new MemoryCampaignPersistence { SnapshotToLoad = CreateSettlementSnapshot() };
            GameManager manager = CreateProductionManager(persistence, new RecordingRandomPresenter(10));
            UniTask<CampaignStartupResult>.Awaiter continueAttempt = manager.ContinueCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(continueAttempt);
            Assert.That(continueAttempt.GetResult().Succeeded, Is.True, continueAttempt.GetResult().Reason);

            PlayableSettlementEventView eventView = managerObject.GetComponent<PlayableSettlementEventView>();
            int initialStone = manager.SettlementData.GetResource("broken_stone");
            yield return WaitForChoice(eventView, "直接把碎石带回营地");
            Assert.That(PlayableHuntInputGuard.IsBlocked, Is.True);
            ClickCard(FindChoice(eventView, "直接把碎石带回营地"));
            yield return WaitForPrimary(eventView, "石脸的回声");
            ClickCard(FindChoice(eventView, "继续"));
            yield return WaitForPrimary(eventView, "未说完的话");
            ClickCard(FindChoice(eventView, "接受结果"));
            yield return WaitForSettlementIdle(manager);

            Assert.That(PlayableHuntInputGuard.IsBlocked, Is.False);
            Assert.That(manager.SettlementData.GetResource("broken_stone"), Is.EqualTo(initialStone + 2));
            Assert.That(manager.SettlementData.PendingEventChains, Is.Empty);
            Assert.That(manager.SettlementData.Timeline.Single(entry => entry.EventId == "main_face_echo").IsCompleted, Is.True);

            CampaignSnapshot saved = persistence.SnapshotToLoad;
            Assert.That(saved.Settlement.GetResource("broken_stone"), Is.EqualTo(initialStone + 2));
            Assert.That(saved.Settlement.Timeline.Single(entry => entry.EventId == "main_face_echo").IsCompleted, Is.True);
            Assert.That(saved.Settlement.PendingHuntReturn, Is.Null);
            int saveCountAfterResolution = persistence.SaveCount;

            UnityEngine.Object.Destroy(managerObject);
            managerObject = null;
            yield return null;
            persistence.SnapshotToLoad = saved;
            GameManager restoredManager = CreateProductionManager(persistence, new RecordingRandomPresenter(10));
            UniTask<CampaignStartupResult>.Awaiter restoreAttempt = restoredManager.ContinueCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(restoreAttempt);
            Assert.That(restoreAttempt.GetResult().Succeeded, Is.True, restoreAttempt.GetResult().Reason);
            yield return WaitForSettlementIdle(restoredManager);
            Assert.That(restoredManager.SettlementData.GetResource("broken_stone"), Is.EqualTo(initialStone + 2));
            Assert.That(persistence.SaveCount, Is.EqualTo(saveCountAfterResolution));
            int completedRandomEventsBeforeHunt = restoredManager.SettlementData.Timeline.Count(entry => entry.EntryType == TimelineEntryType.Random && entry.IsCompleted);

            TabletopDepartureLauncherCard3D launcher = restoredManager.GetComponentsInChildren<TabletopDepartureLauncherCard3D>(true).Single();
            ClickCard(launcher);
            PlayableHuntDestinationView destinationView = managerObject.GetComponent<PlayableHuntDestinationView>();
            yield return WaitUntil(() => destinationView.IsPresenting, "等待实体出猎编队面板打开超时。");
            TabletopHuntDeparturePanel3D departurePanel = destinationView.ActivePanel;
            HuntDepartureHunterCard3D hunterCard = departurePanel.GetComponentsInChildren<HuntDepartureHunterCard3D>(true).First(card => card.Hunter != null);
            SlotGrid squadGrid = (SlotGrid)squadGridField.GetValue(departurePanel);
            BeginAndDrop(hunterCard, squadGrid.Slots[0]);
            Assert.That(departurePanel.SquadCount, Is.EqualTo(1));
            ClickCard(FindChoice(departurePanel, "选择路线"));
            yield return WaitUntil(() => departurePanel.DestinationCount > 0, "等待实体狩猎路线面板打开超时。");
            ClickCard(FindChoice(departurePanel, "确认出发"));
            yield return WaitUntil(() => restoredManager.CurrentGamePhase == GamePhase.Hunt, "等待实体出猎进入狩猎阶段超时。");
            Assert.That(restoredManager.IsHuntActionSessionActive, Is.True);

            HuntRetreatPanel3D retreatPanel = restoredManager.GetComponentInChildren<HuntRetreatPanel3D>(true);
            Assert.That(retreatPanel, Is.Not.Null);
            ClickCard(retreatPanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Single(card => card.DisplayName == "收队回营"));
            Assert.That(retreatPanel.IsConfirmationOpen, Is.True);
            ClickCard(retreatPanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Single(card => card.IsInteractable && card.DisplayName == "结算并回营"));
            PlayableSettlementEventView returnEventView = managerObject.GetComponent<PlayableSettlementEventView>();
            yield return ResolveSettlementEventsAfterReturn(restoredManager, returnEventView);
            yield return WaitForSettlementIdle(restoredManager);
            Assert.That(restoredManager.SettlementData.CurrentYear, Is.EqualTo(3));
            Assert.That(restoredManager.SettlementData.CurrentSeasonIndex, Is.EqualTo(1));
            Assert.That(restoredManager.SettlementData.HuntHistory, Has.Count.EqualTo(1));
            Assert.That(restoredManager.SettlementData.PendingHuntReturn, Is.Null);
            List<string> completedRandomEventIds = restoredManager.SettlementData.Timeline.Where(entry => entry.EntryType == TimelineEntryType.Random && entry.IsCompleted).Select(entry => entry.EventId).OrderBy(id => id).ToList();
            Assert.That(completedRandomEventIds, Has.Count.GreaterThan(completedRandomEventsBeforeHunt));

            CampaignSnapshot returnedSnapshot = persistence.SnapshotToLoad;
            int saveCountAfterReturn = persistence.SaveCount;
            UnityEngine.Object.Destroy(managerObject);
            managerObject = null;
            yield return null;
            persistence.SnapshotToLoad = returnedSnapshot;
            GameManager returnedManager = CreateProductionManager(persistence, new RecordingRandomPresenter(10));
            UniTask<CampaignStartupResult>.Awaiter returnRestore = returnedManager.ContinueCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(returnRestore);
            Assert.That(returnRestore.GetResult().Succeeded, Is.True, returnRestore.GetResult().Reason);
            yield return WaitForSettlementIdle(returnedManager);
            Assert.That(returnedManager.SettlementData.CurrentYear, Is.EqualTo(3));
            Assert.That(returnedManager.SettlementData.CurrentSeasonIndex, Is.EqualTo(1));
            Assert.That(returnedManager.SettlementData.HuntHistory, Has.Count.EqualTo(1));
            Assert.That(returnedManager.SettlementData.PendingHuntReturn, Is.Null);
            Assert.That(returnedManager.SettlementData.Timeline.Where(entry => entry.EntryType == TimelineEntryType.Random && entry.IsCompleted).Select(entry => entry.EventId).OrderBy(id => id), Is.EqualTo(completedRandomEventIds));
            Assert.That(persistence.SaveCount, Is.EqualTo(saveCountAfterReturn), "Continue 不得重复提交回营季节或存档。");
            ClickCard(returnedManager.GetComponentsInChildren<TabletopDepartureLauncherCard3D>(true).Single());
            yield return WaitUntil(() => managerObject.GetComponent<PlayableHuntDestinationView>().IsPresenting, "回营恢复后应可再次打开实体出猎编队桌。");
        }

        [UnityTest]
        public IEnumerator RiskChoice_UsesProductionPhysicalDicePortAndCommitsSelectedHunterOutcome()
        {
            var presenter = new RecordingRandomPresenter(10);
            var persistence = new MemoryCampaignPersistence { SnapshotToLoad = CreateSettlementSnapshot() };
            GameManager manager = CreateProductionManager(persistence, presenter);
            UniTask<CampaignStartupResult>.Awaiter continueAttempt = manager.ContinueCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(continueAttempt);
            Assert.That(continueAttempt.GetResult().Succeeded, Is.True, continueAttempt.GetResult().Reason);

            PlayableSettlementEventView eventView = managerObject.GetComponent<PlayableSettlementEventView>();
            int initialStone = manager.SettlementData.GetResource("broken_stone");
            HunterInstance hunter = manager.SettlementData.GetAliveHunters().First();
            int initialUnderstanding = hunter.Understanding;
            yield return WaitForChoice(eventView, "辨认碎石上的纹路");
            ClickCard(FindChoice(eventView, "辨认碎石上的纹路"));
            yield return WaitForChoice(eventView, hunter.Name);
            ClickCard(FindChoice(eventView, hunter.Name));
            yield return WaitUntil(() => presenter.LastRequest.HasValue, "等待生产物理骰子请求超时。");
            Assert.That(presenter.LastRequest.Value.Kind, Is.EqualTo(TabletopRandomInteractionKind.PhysicalDice));
            Assert.That(presenter.LastRequest.Value.Count, Is.EqualTo(1));
            Assert.That(presenter.LastRequest.Value.Sides, Is.EqualTo(10));
            yield return WaitForChoice(eventView, "接受结果");
            ClickCard(FindChoice(eventView, "接受结果"));
            yield return WaitForChoice(eventView, "继续");
            ClickCard(FindChoice(eventView, "继续"));
            yield return WaitForPrimary(eventView, "未说完的话");
            ClickCard(FindChoice(eventView, "接受结果"));
            yield return WaitForSettlementIdle(manager);

            Assert.That(hunter.Understanding, Is.EqualTo(initialUnderstanding + 1));
            HunterRecoveryRules.GetHealth(hunter, HunterBodyPart.Arms, out int armsHealth, out int armsMaximumHealth);
            Assert.That(armsHealth, Is.EqualTo(armsMaximumHealth));
            Assert.That(manager.SettlementData.GetResource("broken_stone"), Is.EqualTo(initialStone + 1));
            Assert.That(manager.SettlementData.Timeline.Single(entry => entry.EventId == "main_face_echo").IsCompleted, Is.True);
            Assert.That(manager.SettlementData.PendingEventChains, Is.Empty);
        }

        [UnityTest]
        public IEnumerator RiskChoice_RerollsOnPhysicalTableAndCommitsPaymentExactlyOnce()
        {
            CampaignSnapshot snapshot = CreateSettlementSnapshot();
            HunterInstance snapshotHunter = snapshot.Settlement.GetAliveHunters().First();
            snapshotHunter.Willpower = 2;
            snapshotHunter.WillpowerMax = 2;
            var presenter = new RecordingRandomPresenter(1, 10);
            var persistence = new MemoryCampaignPersistence { SnapshotToLoad = snapshot };
            GameManager manager = CreateProductionManager(persistence, presenter);
            UniTask<CampaignStartupResult>.Awaiter continueAttempt = manager.ContinueCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(continueAttempt);
            Assert.That(continueAttempt.GetResult().Succeeded, Is.True, continueAttempt.GetResult().Reason);

            PlayableSettlementEventView eventView = managerObject.GetComponent<PlayableSettlementEventView>();
            HunterInstance hunter = manager.SettlementData.GetHunter(snapshotHunter.InstanceId);
            int initialWillpower = hunter.Willpower;
            int initialLuck = hunter.Luck;
            int initialStone = manager.SettlementData.GetResource("broken_stone");
            yield return WaitForChoice(eventView, "辨认碎石上的纹路");
            ClickCard(FindChoice(eventView, "辨认碎石上的纹路"));
            yield return WaitForChoice(eventView, hunter.Name);
            ClickCard(FindChoice(eventView, hunter.Name));
            yield return WaitUntil(() => presenter.Requests.Count == 1, "等待第一次实体骰子稳定超时。");
            yield return WaitForChoice(eventView, "重投");
            ClickCard(FindChoice(eventView, "重投"));
            yield return WaitUntil(() => presenter.Requests.Count == 2, "等待实体重投骰子稳定超时。");
            yield return WaitForChoice(eventView, "接受结果");
            ClickCard(FindChoice(eventView, "接受结果"));
            yield return WaitForChoice(eventView, "继续");
            ClickCard(FindChoice(eventView, "继续"));
            yield return WaitForPrimary(eventView, "未说完的话");
            ClickCard(FindChoice(eventView, "接受结果"));
            yield return WaitForSettlementIdle(manager);

            Assert.That(presenter.Requests[0].InteractionId, Does.Contain(":initial:"));
            Assert.That(presenter.Requests[1].InteractionId, Does.Contain(":reroll:"));
            Assert.That(presenter.Requests.All(request => request.Kind == TabletopRandomInteractionKind.PhysicalDice && request.ActorId == hunter.InstanceId.ToString()), Is.True);
            Assert.That(hunter.Willpower, Is.EqualTo(initialWillpower - 1));
            Assert.That(hunter.Luck, Is.EqualTo(initialLuck + 1));
            Assert.That(manager.SettlementData.GetResource("broken_stone"), Is.EqualTo(initialStone + 1));
            Assert.That(manager.SettlementData.EventMemories.Single(memory => memory.EventId == "main_face_echo").WasRerolled, Is.True);
            Assert.That(persistence.SnapshotToLoad.Settlement.GetHunter(hunter.InstanceId).Willpower, Is.EqualTo(initialWillpower - 1));
        }

        [UnityTest]
        public IEnumerator OldMaidChoice_UsesProductionPhysicalCardsAndCommitsActionQueueOutcome()
        {
            var persistence = new MemoryCampaignPersistence { SnapshotToLoad = CreateOldMaidSettlementSnapshot() };
            (GameManager manager, TabletopCardInteractionPresenter presenter) = CreateProductionManagerWithCards(persistence);
            UniTask<CampaignStartupResult>.Awaiter continueAttempt = manager.ContinueCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(continueAttempt);
            Assert.That(continueAttempt.GetResult().Succeeded, Is.True, continueAttempt.GetResult().Reason);

            PlayableSettlementEventView eventView = managerObject.GetComponent<PlayableSettlementEventView>();
            HunterInstance hunter = manager.SettlementData.GetAliveHunters().First();
            int initialBlackSalt = manager.SettlementData.GetResource("black_salt");
            yield return WaitForChoice(eventView, "让一名猎人从手牌中抽出一张");
            ClickCard(FindChoice(eventView, "让一名猎人从手牌中抽出一张"));
            yield return WaitForChoice(eventView, hunter.Name);
            ClickCard(FindChoice(eventView, hunter.Name));
            yield return WaitUntil(() => presenter.IsPresenting && UnityEngine.Object.FindObjectsByType<TabletopRandomCard3D>(FindObjectsSortMode.None).Length == 10, "等待生产抽鬼牌桌生成超时。");
            TabletopRandomCard3D safeCard = UnityEngine.Object.FindObjectsByType<TabletopRandomCard3D>(FindObjectsSortMode.None).First(card => !card.IsOldMaid && card.IsSelectable);
            ClickCard(safeCard);
            yield return WaitForChoice(eventView, "接受结果");
            ClickCard(FindChoice(eventView, "接受结果"));
            yield return WaitForChoice(eventView, "继续");
            ClickCard(FindChoice(eventView, "继续"));
            yield return WaitForSettlementIdle(manager);

            Assert.That(manager.SettlementData.GetResource("black_salt"), Is.EqualTo(initialBlackSalt + 1));
            Assert.That(manager.SettlementData.Timeline.Single(entry => entry.EventId == "random_faceless_hand").IsCompleted, Is.True);
            Assert.That(manager.SettlementData.EventMemories.Count(memory => memory.EventId == "random_faceless_hand"), Is.EqualTo(1));
            Assert.That(persistence.SnapshotToLoad.Settlement.GetResource("black_salt"), Is.EqualTo(initialBlackSalt + 1));
        }

        [UnityTest]
        public IEnumerator WhisperSickness_PersistsAndUnlocksActorScopedHuntReward()
        {
            var presenter = new RecordingRandomPresenter(1);
            var persistence = new MemoryCampaignPersistence { SnapshotToLoad = CreateWhisperSettlementSnapshot() };
            GameManager manager = CreateProductionManager(persistence, presenter);
            UniTask<CampaignStartupResult>.Awaiter continueAttempt = manager.ContinueCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(continueAttempt);
            CampaignStartupResult continueResult = continueAttempt.GetResult();
            Assert.That(continueResult.Succeeded, Is.True, continueResult.Reason);

            PlayableSettlementEventView eventView = managerObject.GetComponent<PlayableSettlementEventView>();
            HunterInstance hunter = manager.SettlementData.GetAliveHunters().First();
            int hunterId = hunter.InstanceId;
            int initialStrength = hunter.Stats.strength;
            yield return WaitForChoice(eventView, "从配方牌中翻出缺失的最后一张");
            ClickCard(FindChoice(eventView, "从配方牌中翻出缺失的最后一张"));
            yield return WaitForChoice(eventView, hunter.Name);
            ClickCard(FindChoice(eventView, hunter.Name));
            yield return WaitUntil(() => presenter.LastRequest.HasValue, "等待低语配方翻牌请求超时。");
            Assert.That(presenter.LastRequest.Value.Kind, Is.EqualTo(TabletopRandomInteractionKind.FlipCards));
            Assert.That(presenter.LastRequest.Value.ActorId, Is.EqualTo(hunterId.ToString()));
            yield return WaitForChoice(eventView, "接受结果");
            ClickCard(FindChoice(eventView, "接受结果"));
            yield return WaitForChoice(eventView, "继续");
            ClickCard(FindChoice(eventView, "继续"));
            yield return WaitForSettlementIdle(manager);

            HunterSymptomState acquiredState = HunterSymptomRules.Find(hunter, "symptom_whisper_sickness");
            Assert.That(acquiredState, Is.Not.Null);
            Assert.That(hunter.Stats.strength, Is.EqualTo(Mathf.Max(0, initialStrength - 1)));
            Assert.That(hunter.Ailments, Does.Contain("低语症").And.Not.Contain("symptom_whisper_sickness"));
            Assert.That(manager.SettlementData.Timeline.Single(entry => entry.EventId == "random_whispering_mortar").IsCompleted, Is.True);

            HunterSymptomPanel3D symptomPanel = managerObject.GetComponentInChildren<HunterSymptomPanel3D>(true);
            Assert.That(symptomPanel, Is.Not.Null);
            symptomPanel.Open(hunter, manager.SettlementData, settings.Symptoms, null, Vector3.zero);
            Assert.That(symptomPanel.GetComponentsInChildren<HunterSymptomCard3D>(true).Single(card => card.Definition?.Id == "symptom_whisper_sickness").DisplayName, Is.EqualTo("低语症"));
            symptomPanel.Hide();

            CampaignSnapshot symptomaticSnapshot = persistence.SnapshotToLoad;
            UnityEngine.Object.Destroy(managerObject);
            managerObject = null;
            yield return null;
            persistence.SnapshotToLoad = symptomaticSnapshot;
            presenter = new RecordingRandomPresenter(10);
            manager = CreateProductionManager(persistence, presenter);
            UniTask<CampaignStartupResult>.Awaiter restoreAttempt = manager.ContinueCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(restoreAttempt);
            CampaignStartupResult restoreResult = restoreAttempt.GetResult();
            Assert.That(restoreResult.Succeeded, Is.True, restoreResult.Reason);
            yield return WaitForSettlementIdle(manager);

            eventView = managerObject.GetComponent<PlayableSettlementEventView>();
            hunter = manager.SettlementData.GetHunter(hunterId);
            Assert.That(HunterSymptomRules.Find(hunter, "symptom_whisper_sickness"), Is.Not.Null);
            Assert.That(hunter.Ailments, Does.Contain("低语症"));
            EventData rootPulse = PlayableEventTableRuntime.GetEvents().First(item => item.ContentId == "hunt_root_pulse");
            EventOption symptomOption = rootPulse.options.First(option => option.conditions.Any(condition => condition.conditionKind == EventOptionConditionKind.HasAilment));
            Assert.That(PlayableEventOptionAvailability.CanUse(symptomOption, hunter, manager.SettlementData, out string availableReason), Is.True, availableReason);
            HunterInstance otherHunter = manager.SettlementData.GetAliveHunters().First(candidate => candidate.InstanceId != hunterId);
            Assert.That(PlayableEventOptionAvailability.CanUse(symptomOption, otherHunter, manager.SettlementData, out _), Is.False);

            UniTask<SettlementDepartureCommandResult>.Awaiter departure = manager.DepartForHuntAsync(new[] { hunterId }, contentCandidate.HuntDestinations.GetAvailable(3)[0]).GetAwaiter();
            yield return WaitForCompletion(departure);
            SettlementDepartureCommandResult departureResult = departure.GetResult();
            Assert.That(departureResult.Succeeded, Is.True, departureResult.Reason);
            yield return WaitUntil(() => !PlayableHuntInputGuard.IsBlocked, "等待低语症猎人进入狩猎桌超时。");

            HuntManager huntManager = manager.ActiveHuntRuntime.Manager;
            Assert.That(huntManager.BoundRoute.TryResolveEvent("hunt_root_pulse", out rootPulse), Is.True);
            HexTileInstance targetTile = huntManager.Map.Values.First(tile => tile.State == TileState.Interactable && !tile.HasBossEncounter && tile.Config != null);
            HuntMapVisualizer visualizer = managerObject.GetComponentInChildren<HuntMapVisualizer>(true);
            PlayableHexTileCard3D targetTileCard = visualizer.GetComponentsInChildren<PlayableHexTileCard3D>(true).First(card => card.gameObject.name == $"Tile_{targetTile.AxialCoord.x}_{targetTile.AxialCoord.y}");
            patchedTileConfig = targetTile.Config;
            originalPatchedTileRevealEvent = patchedTileConfig.tileRevealEvent;
            patchedTileConfig.tileRevealEvent = rootPulse;
            int initialRootCount = hunter.Collectibles.Where(item => item?.Data?.ContentId == "bulbous_root").Sum(item => item.Count);
            int initialSettlementRootCount = manager.SettlementData.GetResource("bulbous_root");
            targetTileCard.enabled = false;
            Assert.That(manager.ActiveHuntExplorationPort.TryCreateSnapshot(targetTile.AxialCoord, -1, out HuntExplorationSnapshot exploration), Is.True);
            UniTask<HuntTileCommandResult>.Awaiter reveal = manager.ActiveHuntExplorationPort.SubmitTileAsync(exploration).GetAwaiter();
            yield return WaitForChoice(eventView, "让患有低语症的猎人指出脉搏间隙");
            ClickCard(FindChoice(eventView, "让患有低语症的猎人指出脉搏间隙"));
            yield return WaitForChoice(eventView, "继续");
            ClickCard(FindChoice(eventView, "继续"));
            yield return WaitForCompletion(reveal);
            HuntTileCommandResult revealResult = reveal.GetResult();
            Assert.That(revealResult.Succeeded, Is.True, revealResult.Reason);
            Assert.That(hunter.Collectibles.Where(item => item?.Data?.ContentId == "bulbous_root").Sum(item => item.Count), Is.EqualTo(initialRootCount + 2));
            RestorePatchedTile();

            HuntRetreatPanel3D retreatPanel = manager.GetComponentInChildren<HuntRetreatPanel3D>(true);
            ClickCard(retreatPanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Single(card => card.DisplayName == "收队回营"));
            ClickCard(retreatPanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Single(card => card.IsInteractable && card.DisplayName == "结算并回营"));
            yield return ResolveSettlementEventsAfterReturn(manager, eventView);
            yield return WaitForSettlementIdle(manager);

            hunter = manager.SettlementData.GetHunter(hunterId);
            Assert.That(manager.SettlementData.GetResource("bulbous_root"), Is.EqualTo(initialSettlementRootCount + 2));
            Assert.That(HunterSymptomRules.Find(hunter, "symptom_whisper_sickness"), Is.Not.Null);
            Assert.That(manager.SettlementData.CurrentYear, Is.EqualTo(3));
            Assert.That(manager.SettlementData.CurrentSeasonIndex, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator StoneListenerBloodline_PersistsAndUnlocksLaterSettlementSolution()
        {
            var persistence = new MemoryCampaignPersistence { SnapshotToLoad = CreateBloodlineSettlementSnapshot() };
            GameManager manager = CreateProductionManager(persistence, new RecordingRandomPresenter(10));
            UniTask<CampaignStartupResult>.Awaiter continueAttempt = manager.ContinueCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(continueAttempt);
            CampaignStartupResult continueResult = continueAttempt.GetResult();
            Assert.That(continueResult.Succeeded, Is.True, continueResult.Reason);

            PlayableSettlementEventView eventView = managerObject.GetComponent<PlayableSettlementEventView>();
            HunterInstance listener = manager.SettlementData.GetAliveHunters().First();
            Assert.That(listener.BloodlineId, Is.EqualTo("stone-listener"));
            HunterInstance otherHunter = manager.SettlementData.GetAliveHunters().First(hunter => hunter.InstanceId != listener.InstanceId && !hunter.Traits.Contains("trait_stone_speaker"));
            int listenerId = listener.InstanceId;
            yield return WaitForChoice(eventView, "侧耳贴近石面，听清沉默中的回声");
            ClickCard(FindChoice(eventView, "侧耳贴近石面，听清沉默中的回声"));
            yield return WaitForChoice(eventView, listener.Name);
            TabletopEventChoiceCard3D[] bloodlineHunters = eventView.ActivePanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true);
            Assert.That(bloodlineHunters.Single(card => card.DisplayName == listener.Name).IsInteractable, Is.True);
            Assert.That(bloodlineHunters.Single(card => card.DisplayName == otherHunter.Name).IsInteractable, Is.False);
            ClickCard(FindChoice(eventView, listener.Name));
            yield return WaitForChoice(eventView, "继续");
            ClickCard(FindChoice(eventView, "继续"));
            yield return WaitForSettlementIdle(manager);

            Assert.That(listener.IsBloodlineActivated, Is.True);
            Assert.That(listener.Traits.Count(trait => trait == "trait_stone_speaker"), Is.EqualTo(1));
            Assert.That(manager.SettlementData.Timeline.Single(entry => entry.EventId == "random_bloodline_awakening").IsCompleted, Is.True);
            Assert.That(manager.SettlementData.EventMemories.Count(memory => memory.EventId == "random_bloodline_awakening"), Is.EqualTo(1));
            HunterEquipmentPanel3D equipmentPanel = managerObject.GetComponentInChildren<HunterEquipmentPanel3D>(true);
            Assert.That(equipmentPanel, Is.Not.Null);
            equipmentPanel.Show(listener, manager.SettlementData, Array.Empty<ItemData>(), Vector3.zero);
            string profileText = string.Join("\n", equipmentPanel.GetComponentsInChildren<TMPro.TextMeshPro>(true).Select(text => text.text));
            Assert.That(profileText, Does.Contain("听石之血 · 已激活").And.Contain("石语者"));
            equipmentPanel.Hide();

            Assert.That(persistence.SaveCount, Is.GreaterThan(0));
            Assert.That(persistence.Payload, Is.Not.Null.And.Not.Empty);
            CampaignSnapshot awakenedSnapshot = JsonUtility.FromJson<CampaignSnapshot>(persistence.Payload);
            EventData sealedStore = PlayableEventTableRuntime.GetEvents().Single(item => item.ContentId == "random_sealed_store");
            awakenedSnapshot.Settlement.Timeline.Add(new AnnalEntry { Year = awakenedSnapshot.Settlement.CurrentYear, EventId = sealedStore.ContentId, EventName = sealedStore.eventName, EntryType = TimelineEntryType.Random });
            int firstMemoryCount = awakenedSnapshot.Settlement.EventMemories.Count;
            UnityEngine.Object.Destroy(managerObject);
            managerObject = null;
            yield return null;
            persistence.SnapshotToLoad = awakenedSnapshot;
            manager = CreateProductionManager(persistence, new RecordingRandomPresenter(10));
            UniTask<CampaignStartupResult>.Awaiter restoreAttempt = manager.ContinueCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(restoreAttempt);
            CampaignStartupResult restoreResult = restoreAttempt.GetResult();
            Assert.That(restoreResult.Succeeded, Is.True, restoreResult.Reason);

            eventView = managerObject.GetComponent<PlayableSettlementEventView>();
            listener = manager.SettlementData.GetHunter(listenerId);
            otherHunter = manager.SettlementData.GetAliveHunters().First(hunter => hunter.InstanceId != listenerId && !hunter.Traits.Contains("trait_stone_speaker"));
            Assert.That(listener.BloodlineId, Is.EqualTo("stone-listener"));
            Assert.That(listener.IsBloodlineActivated, Is.True);
            Assert.That(listener.Traits.Count(trait => trait == "trait_stone_speaker"), Is.EqualTo(1));
            Assert.That(manager.SettlementData.EventMemories.Count(memory => memory.EventId == "random_bloodline_awakening"), Is.EqualTo(1));
            int initialStone = manager.SettlementData.GetResource("broken_stone");
            yield return WaitForChoice(eventView, "让理解石头的猎人判断洞壁收拢的节奏");
            ClickCard(FindChoice(eventView, "让理解石头的猎人判断洞壁收拢的节奏"));
            yield return WaitForChoice(eventView, listener.Name);
            TabletopEventChoiceCard3D[] traitHunters = eventView.ActivePanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true);
            Assert.That(traitHunters.Single(card => card.DisplayName == listener.Name).IsInteractable, Is.True);
            Assert.That(traitHunters.Single(card => card.DisplayName == otherHunter.Name).IsInteractable, Is.False);
            EventOption traitOption = sealedStore.options.Single(option => option.optionId == "read_wall_rhythm");
            Assert.That(PlayableEventOptionAvailability.CanUse(traitOption, otherHunter, manager.SettlementData, out string unavailableReason), Is.False);
            Assert.That(unavailableReason, Does.Contain("石语者").And.Not.Contain("trait_stone_speaker"));
            ClickCard(FindChoice(eventView, listener.Name));
            yield return WaitForChoice(eventView, "继续");
            ClickCard(FindChoice(eventView, "继续"));
            yield return WaitForSettlementIdle(manager);

            Assert.That(manager.SettlementData.GetResource("broken_stone"), Is.EqualTo(initialStone + 2));
            Assert.That(manager.SettlementData.Timeline.Single(entry => entry.EventId == "random_sealed_store").IsCompleted, Is.True);
            Assert.That(manager.SettlementData.EventMemories.Count, Is.EqualTo(firstMemoryCount + 1));
            Assert.That(manager.SettlementData.EventMemories.Count(memory => memory.EventId == "random_sealed_store"), Is.EqualTo(1));
            Assert.That(PlayableHuntInputGuard.IsBlocked, Is.False);
            ClickCard(manager.GetComponentsInChildren<TabletopDepartureLauncherCard3D>(true).Single());
            yield return WaitUntil(() => managerObject.GetComponent<PlayableHuntDestinationView>().IsPresenting, "血脉事件弧完成后应可继续打开实体出猎编队桌。");
        }

        private GameManager CreateProductionManager(MemoryCampaignPersistence persistence, ITabletopRandomInteractionPresenter presenter)
        {
            managerObject = new GameObject("Playable Settlement Event Production Loop");
            managerObject.SetActive(false);
            GameManager manager = managerObject.AddComponent<GameManager>();
            Assert.That(manager.ConfigureCampaign(new CampaignBootstrapRequest
            {
                BattleSetup = contentCandidate.DefaultBattleSetup,
                CellSize = contentCandidate.CellSize,
                SettlementContent = contentCandidate.SettlementContent,
                WorkshopContent = contentCandidate.WorkshopContent,
                WaitForEntrySelection = true,
                TabletopInteraction = presenter,
                Persistence = persistence
            }), Is.True);
            PlayableGameBootstrap.EnsureRequiredWorldSpacePorts(managerObject, manager, settings);
            managerObject.SetActive(true);
            return manager;
        }

        private (GameManager Manager, TabletopCardInteractionPresenter Presenter) CreateProductionManagerWithCards(MemoryCampaignPersistence persistence)
        {
            managerObject = new GameObject("Playable Physical Card Event Production Loop");
            managerObject.SetActive(false);
            TabletopCardInteractionPresenter presenter = managerObject.AddComponent<TabletopCardInteractionPresenter>();
            typeof(TabletopCardInteractionPresenter).GetField("revealDuration", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(presenter, 0f);
            typeof(TabletopCardInteractionPresenter).GetField("resultDisplayDuration", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(presenter, 0f);
            GameManager manager = managerObject.AddComponent<GameManager>();
            Assert.That(manager.ConfigureCampaign(new CampaignBootstrapRequest
            {
                BattleSetup = contentCandidate.DefaultBattleSetup,
                CellSize = contentCandidate.CellSize,
                SettlementContent = contentCandidate.SettlementContent,
                WorkshopContent = contentCandidate.WorkshopContent,
                WaitForEntrySelection = true,
                TabletopInteraction = presenter,
                Persistence = persistence
            }), Is.True);
            PlayableGameBootstrap.EnsureRequiredWorldSpacePorts(managerObject, manager, settings);
            managerObject.SetActive(true);
            return (manager, presenter);
        }

        private CampaignSnapshot CreateSettlementSnapshot()
        {
            var source = new SettlementManager(17);
            source.EnsureStartingConditions();
            source.Data.CurrentYear = 3;
            source.Data.CurrentSeasonIndex = 0;
            EventData gameEvent = source.Timeline.ResolveEvent("main_face_echo");
            Assert.That(gameEvent, Is.Not.Null);
            source.Data.Timeline.Add(new AnnalEntry { Year = 3, EventId = gameEvent.ContentId, EventName = gameEvent.eventName, IsMilestone = true, EntryType = TimelineEntryType.Scheduled });
            source.Data.Timeline.Add(new AnnalEntry { Year = 3, EventId = "random_stone_vigil", EventName = "石缝里的盐光", IsCompleted = true, EntryType = TimelineEntryType.Random });
            Assert.That(source.Data.PendingHuntReturn, Is.Null);
            return new CampaignSnapshot { Settlement = source.Data, CampaignSchemaVersion = CampaignSnapshot.CurrentSchemaVersion };
        }

        private void RestorePatchedTile()
        {
            if (patchedTileConfig == null) return;
            patchedTileConfig.tileRevealEvent = originalPatchedTileRevealEvent;
            patchedTileConfig = null;
            originalPatchedTileRevealEvent = null;
        }

        private static CampaignSnapshot CreateWhisperSettlementSnapshot()
        {
            var source = new SettlementManager(19);
            source.EnsureStartingConditions();
            source.Data.CurrentYear = 3;
            source.Data.CurrentSeasonIndex = 0;
            EventData gameEvent = PlayableEventTableRuntime.GetEvents().First(item => item.ContentId == "random_whispering_mortar");
            source.Data.Timeline.Add(new AnnalEntry { Year = 3, EventId = gameEvent.ContentId, EventName = gameEvent.eventName, EntryType = TimelineEntryType.Random });
            return new CampaignSnapshot { Settlement = source.Data, CampaignSchemaVersion = CampaignSnapshot.CurrentSchemaVersion };
        }

        private static CampaignSnapshot CreateOldMaidSettlementSnapshot()
        {
            var source = new SettlementManager(29);
            source.EnsureStartingConditions();
            source.Data.CurrentYear = 3;
            source.Data.CurrentSeasonIndex = 0;
            EventData gameEvent = PlayableEventTableRuntime.GetEvents().Single(item => item.ContentId == "random_faceless_hand");
            source.Data.Timeline.Add(new AnnalEntry { Year = 3, EventId = gameEvent.ContentId, EventName = gameEvent.eventName, EntryType = TimelineEntryType.Random });
            return new CampaignSnapshot { Settlement = source.Data, CampaignSchemaVersion = CampaignSnapshot.CurrentSchemaVersion };
        }

        private static CampaignSnapshot CreateBloodlineSettlementSnapshot()
        {
            var source = new SettlementManager(23);
            source.EnsureStartingConditions();
            source.Data.CurrentYear = 3;
            source.Data.CurrentSeasonIndex = 0;
            HunterInstance listener = source.Data.GetAliveHunters().First();
            listener.BloodlineId = "stone-listener";
            listener.BloodlineName = "听石之血";
            listener.IsBloodlineActivated = false;
            listener.Traits.RemoveAll(trait => trait == "trait_stone_speaker");
            foreach (HunterInstance hunter in source.Data.GetAliveHunters().Where(hunter => hunter.InstanceId != listener.InstanceId))
            {
                hunter.BloodlineId = "ember-remembered";
                hunter.BloodlineName = "余烬之血";
                hunter.IsBloodlineActivated = false;
                hunter.Traits.RemoveAll(trait => trait == "trait_stone_speaker");
            }
            EventData gameEvent = PlayableEventTableRuntime.GetEvents().Single(item => item.ContentId == "random_bloodline_awakening");
            source.Data.Timeline.Add(new AnnalEntry { Year = 3, EventId = gameEvent.ContentId, EventName = gameEvent.eventName, EntryType = TimelineEntryType.Random });
            return new CampaignSnapshot { Settlement = source.Data, CampaignSchemaVersion = CampaignSnapshot.CurrentSchemaVersion };
        }

        private static TabletopEventChoiceCard3D FindChoice(PlayableSettlementEventView view, string title) => view.ActivePanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Single(card => card.IsInteractable && card.DisplayName == title);

        private static TabletopEventChoiceCard3D FindChoice(TabletopHuntDeparturePanel3D panel, string title) => panel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Single(card => card.IsInteractable && card.DisplayName == title);

        private static void ClickCard(CardView3D card)
        {
            card.HandlePointerDown(Vector2.zero);
            card.HandlePointerUp();
        }

        private static void BeginAndDrop(HuntDepartureHunterCard3D card, CardSlot target)
        {
            Vector2 pointerDown = Vector2.zero;
            card.HandlePointerDown(pointerDown);
            card.HandlePointerDrag(pointerDown + Vector2.right * 10f, target.transform.position);
            card.HandlePointerUp();
        }

        private static IEnumerator WaitForChoice(PlayableSettlementEventView view, string title)
        {
            yield return WaitUntil(() => view != null && view.ActivePanel != null && view.ActivePanel.IsOpen && view.ActivePanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Any(card => card.IsInteractable && card.DisplayName == title), $"等待实体事件选项 {title} 超时。");
        }

        private static IEnumerator ResolveSettlementEventsAfterReturn(GameManager manager, PlayableSettlementEventView view)
        {
            const int maximumPrompts = 16;
            for (int promptIndex = 0; promptIndex < maximumPrompts; promptIndex++)
            {
                float deadline = Time.realtimeSinceStartup + 5f;
                TabletopEventChoiceCard3D[] choices = null;
                while (Time.realtimeSinceStartup < deadline)
                {
                    if (manager != null && !manager.IsSettlementActionSessionRunning && manager.IsSettlementEventRestoreReady) yield break;
                    choices = view?.ActivePanel?.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Where(card => card.IsInteractable).ToArray();
                    if (choices?.Length > 0) break;
                    yield return new WaitForFixedUpdate();
                }
                Assert.That(choices, Is.Not.Null.And.Not.Empty, "回营事件 Runner 正在等待，但实体事件桌没有可用选项。");
                TabletopEventChoiceCard3D choice = choices.FirstOrDefault(card => card.DisplayName == "接受结果") ?? choices.FirstOrDefault(card => card.DisplayName == "继续") ?? choices.FirstOrDefault(card => card.DisplayName != "返回") ?? choices[0];
                ClickCard(choice);
                yield return new WaitForFixedUpdate();
            }
            Assert.Fail("回营事件链超过 16 个实体提示，疑似未能收敛。");
        }

        private static IEnumerator WaitForPrimary(PlayableSettlementEventView view, string title)
        {
            yield return WaitUntil(() => view != null && view.ActivePanel != null && view.ActivePanel.IsOpen && view.ActivePanel.GetComponentsInChildren<TabletopEventPrimaryCard3D>(true).Any(card => card.DisplayName == title), $"等待实体事件主卡 {title} 超时。");
        }

        private static IEnumerator WaitForSettlementIdle(GameManager manager)
        {
            for (int frame = 0; frame < FrameTimeout; frame++)
            {
                if (manager != null && manager.CurrentGamePhase == GamePhase.Settlement && manager.IsCampaignActionSessionActive && !manager.IsSettlementActionSessionRunning && manager.IsSettlementEventRestoreReady && manager.SettlementData?.PendingHuntReturn == null)
                    yield break;
                yield return null;
            }
            PlayableSettlementEventView eventView = manager != null ? manager.GetComponent<PlayableSettlementEventView>() : null;
            string choices = eventView?.ActivePanel == null ? "none" : string.Join("|", eventView.ActivePanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Where(card => card.IsInteractable).Select(card => card.DisplayName));
            Assert.Fail($"等待营地事件生产闭环完成超时：phase={manager?.CurrentGamePhase}, campaign={manager?.IsCampaignActionSessionActive}, running={manager?.IsSettlementActionSessionRunning}, restore={manager?.IsSettlementEventRestoreReady}, pendingReturn={manager?.SettlementData?.PendingHuntReturn != null}, pendingEvents={manager?.SettlementData?.PendingEventChains?.Count ?? -1}, choices={choices}。");
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string message)
        {
            for (int frame = 0; frame < FrameTimeout; frame++)
            {
                if (condition())
                    yield break;
                yield return null;
            }
            Assert.Fail(message);
        }

        private static IEnumerator WaitForCompletion<T>(UniTask<T>.Awaiter awaiter)
        {
            for (int frame = 0; frame < FrameTimeout; frame++)
            {
                if (awaiter.IsCompleted)
                    yield break;
                yield return null;
            }
            Assert.Fail("等待战役启动操作完成超时。");
        }

        private static void ResetContentAssembly()
        {
            InvokeReset(typeof(PlayableCampaignContentAssembler));
            InvokeReset(typeof(PlayableHuntDestinationRuntime));
            InvokeReset(typeof(PlayableHuntContentRuntime));
            InvokeReset(typeof(PlayableSettlementContentRuntime));
            PlayableEventTableRuntime.ClearCache();
            PlayableSymptomRuntime.Configure(null);
        }

        private static void InvokeReset(Type type) => type.GetMethod("ResetRuntimeState", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);

        private sealed class RecordingRandomPresenter : ITabletopRandomInteractionPresenter
        {
            private readonly Queue<int> values;

            public RecordingRandomPresenter(params int[] values) => this.values = new Queue<int>(values);

            public TabletopRandomInteractionRequest? LastRequest { get; private set; }
            public List<TabletopRandomInteractionRequest> Requests { get; } = new();

            public UniTask<TabletopRandomInteractionResult> PresentAsync(TabletopRandomInteractionRequest request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                Requests.Add(request);
                IReadOnlyList<string> cardIds = request.Kind == TabletopRandomInteractionKind.PhysicalDice ? Array.Empty<string>() : new[] { $"{request.DeckId}:0" };
                int value = values.Count > 0 ? values.Dequeue() : 1;
                return UniTask.FromResult(new TabletopRandomInteractionResult(request.InteractionId, new[] { value }, cardIds));
            }
        }

        private sealed class MemoryCampaignPersistence : ICampaignPersistencePort
        {
            public CampaignSnapshot SnapshotToLoad { get; set; }
            public string Payload { get; private set; }
            public int SaveCount { get; private set; }

            public void InvalidatePendingWrites() { }

            public UniTask<bool> TrySavePayloadAsync(string payload, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Payload = payload;
                SnapshotToLoad = JsonUtility.FromJson<CampaignSnapshot>(payload);
                NormalizeLoadedSnapshot(SnapshotToLoad);
                SaveCount++;
                return UniTask.FromResult(true);
            }

            public UniTask<bool> HasSaveAsync(CancellationToken cancellationToken = default) => UniTask.FromResult(SnapshotToLoad != null || !string.IsNullOrWhiteSpace(Payload));

            public UniTask<CampaignSnapshot> LoadAsync(CancellationToken cancellationToken = default)
            {
                CampaignSnapshot snapshot = SnapshotToLoad ?? (string.IsNullOrWhiteSpace(Payload) ? null : JsonUtility.FromJson<CampaignSnapshot>(Payload));
                NormalizeLoadedSnapshot(snapshot);
                return UniTask.FromResult(snapshot);
            }

            public bool TrySavePayloadImmediate(string payload)
            {
                Payload = payload;
                SnapshotToLoad = JsonUtility.FromJson<CampaignSnapshot>(payload);
                NormalizeLoadedSnapshot(SnapshotToLoad);
                SaveCount++;
                return true;
            }

            public UniTask<bool> TryDeleteAsync(CancellationToken cancellationToken = default)
            {
                SnapshotToLoad = null;
                Payload = null;
                return UniTask.FromResult(true);
            }

            private static void NormalizeLoadedSnapshot(CampaignSnapshot snapshot)
            {
                if (snapshot?.Settlement?.PendingHuntReturn != null && string.IsNullOrWhiteSpace(snapshot.Settlement.PendingHuntReturn.RecordId))
                    snapshot.Settlement.PendingHuntReturn = null;
            }
        }
    }
}
