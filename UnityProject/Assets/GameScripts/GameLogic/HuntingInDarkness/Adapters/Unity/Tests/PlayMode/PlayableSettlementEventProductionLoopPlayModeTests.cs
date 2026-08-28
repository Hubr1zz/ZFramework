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
using HuntingInDarkness.ViewLayer.Hunt;
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
