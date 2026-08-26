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
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.Bootstrap;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.ViewLayer.Settlement;
using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;
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

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ResetContentAssembly();
            settings = Resources.Load<PlayableBootstrapSettings>("HuntingInDarkness/PlayableBootstrapSettings");
            Assert.That(settings, Is.Not.Null);
            PlayableSymptomRuntime.Configure(settings.Symptoms);
            Assert.That(PlayableCampaignContentAssembler.TryBuild(settings, out contentCandidate, out PlayableContentDiagnosticReport buildReport), Is.True, buildReport.ToString());
            Assert.That(PlayableCampaignContentAssembler.Install(contentCandidate, out PlayableContentDiagnosticReport installReport), Is.True, installReport.ToString());
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (managerObject != null)
                UnityEngine.Object.Destroy(managerObject);
            yield return null;
            ResetContentAssembly();
        }

        [UnityTest]
        public IEnumerator SafeChoice_CompletesPhysicalEventChainPersistsOnceAndReachesDeparture()
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
            FindChoice(eventView, "直接把碎石带回营地").Clicked.Invoke();
            yield return WaitForPrimary(eventView, "石脸的回声");
            FindChoice(eventView, "继续").Clicked.Invoke();
            yield return WaitForPrimary(eventView, "未说完的话");
            FindChoice(eventView, "接受结果").Clicked.Invoke();
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

            TabletopDepartureLauncherCard3D launcher = restoredManager.GetComponentsInChildren<TabletopDepartureLauncherCard3D>(true).Single();
            launcher.Clicked.Invoke();
            PlayableHuntDestinationView destinationView = managerObject.GetComponent<PlayableHuntDestinationView>();
            yield return WaitUntil(() => destinationView.IsPresenting, "等待实体出猎编队面板打开超时。");
            TabletopHuntDeparturePanel3D departurePanel = destinationView.ActivePanel;
            HuntDepartureHunterCard3D hunterCard = departurePanel.GetComponentsInChildren<HuntDepartureHunterCard3D>(true).First(card => card.Hunter != null);
            SlotGrid squadGrid = (SlotGrid)squadGridField.GetValue(departurePanel);
            hunterCard.CurrentSlot?.ClearCard();
            Assert.That(squadGrid.TryPlaceCard(hunterCard), Is.True);
            hunterCard.PlacementChanged?.Invoke();
            Assert.That(departurePanel.SquadCount, Is.EqualTo(1));
            FindChoice(departurePanel, "选择路线").Clicked.Invoke();
            yield return WaitUntil(() => departurePanel.DestinationCount > 0, "等待实体狩猎路线面板打开超时。");
            FindChoice(departurePanel, "确认出发").Clicked.Invoke();
            yield return WaitUntil(() => restoredManager.CurrentGamePhase == GamePhase.Hunt, "等待实体出猎进入狩猎阶段超时。");
            Assert.That(restoredManager.IsHuntActionSessionActive, Is.True);
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
            FindChoice(eventView, "辨认碎石上的纹路").Clicked.Invoke();
            yield return WaitForChoice(eventView, hunter.Name);
            FindChoice(eventView, hunter.Name).Clicked.Invoke();
            yield return WaitUntil(() => presenter.LastRequest.HasValue, "等待生产物理骰子请求超时。");
            Assert.That(presenter.LastRequest.Value.Kind, Is.EqualTo(TabletopRandomInteractionKind.PhysicalDice));
            Assert.That(presenter.LastRequest.Value.Count, Is.EqualTo(1));
            Assert.That(presenter.LastRequest.Value.Sides, Is.EqualTo(10));
            yield return WaitForChoice(eventView, "接受结果");
            FindChoice(eventView, "接受结果").Clicked.Invoke();
            yield return WaitForChoice(eventView, "继续");
            FindChoice(eventView, "继续").Clicked.Invoke();
            yield return WaitForPrimary(eventView, "未说完的话");
            FindChoice(eventView, "接受结果").Clicked.Invoke();
            yield return WaitForSettlementIdle(manager);

            Assert.That(hunter.Understanding, Is.EqualTo(initialUnderstanding + 1));
            HunterRecoveryRules.GetHealth(hunter, HunterBodyPart.Arms, out int armsHealth, out int armsMaximumHealth);
            Assert.That(armsHealth, Is.EqualTo(armsMaximumHealth));
            Assert.That(manager.SettlementData.GetResource("broken_stone"), Is.EqualTo(initialStone + 1));
            Assert.That(manager.SettlementData.Timeline.Single(entry => entry.EventId == "main_face_echo").IsCompleted, Is.True);
            Assert.That(manager.SettlementData.PendingEventChains, Is.Empty);
        }

        private GameManager CreateProductionManager(MemoryCampaignPersistence persistence, ITabletopRandomInteractionPresenter presenter)
        {
            managerObject = new GameObject("Playable Settlement Event Production Loop");
            managerObject.SetActive(false);
            GameManager manager = managerObject.AddComponent<GameManager>();
            manager.ConfigurePlayableRuntime(contentCandidate.DefaultBattleSetup, contentCandidate.CellSize);
            manager.ConfigureSettlementContent(contentCandidate.SettlementContent);
            manager.ConfigureWorkshopContent(contentCandidate.WorkshopContent);
            Assert.That(manager.ConfigurePlayableStartup(true), Is.True);
            Assert.That(manager.ConfigureTabletopInteraction(presenter), Is.True);
            Assert.That(manager.ConfigureCampaignPersistence(persistence), Is.True);
            PlayableGameBootstrap.EnsureRequiredWorldSpacePorts(managerObject, manager, settings);
            managerObject.SetActive(true);
            return manager;
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

        private static TabletopEventChoiceCard3D FindChoice(PlayableSettlementEventView view, string title) => view.ActivePanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Single(card => card.IsInteractable && card.DisplayName == title);

        private static TabletopEventChoiceCard3D FindChoice(TabletopHuntDeparturePanel3D panel, string title) => panel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Single(card => card.IsInteractable && card.DisplayName == title);

        private static IEnumerator WaitForChoice(PlayableSettlementEventView view, string title)
        {
            yield return WaitUntil(() => view != null && view.ActivePanel != null && view.ActivePanel.IsOpen && view.ActivePanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Any(card => card.IsInteractable && card.DisplayName == title), $"等待实体事件选项 {title} 超时。");
        }

        private static IEnumerator WaitForPrimary(PlayableSettlementEventView view, string title)
        {
            yield return WaitUntil(() => view != null && view.ActivePanel != null && view.ActivePanel.IsOpen && view.ActivePanel.GetComponentsInChildren<TabletopEventPrimaryCard3D>(true).Any(card => card.DisplayName == title), $"等待实体事件主卡 {title} 超时。");
        }

        private static IEnumerator WaitForSettlementIdle(GameManager manager)
        {
            yield return WaitUntil(() => manager != null && manager.CurrentGamePhase == GamePhase.Settlement && manager.IsCampaignActionSessionActive && !manager.IsSettlementActionSessionRunning && manager.IsSettlementEventRestoreReady && manager.SettlementData?.PendingHuntReturn == null, "等待营地事件生产闭环完成超时。");
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
            private readonly int value;

            public RecordingRandomPresenter(int value) => this.value = value;

            public TabletopRandomInteractionRequest? LastRequest { get; private set; }

            public UniTask<TabletopRandomInteractionResult> PresentAsync(TabletopRandomInteractionRequest request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                return UniTask.FromResult(new TabletopRandomInteractionResult(request.InteractionId, new[] { value }, Array.Empty<string>()));
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
