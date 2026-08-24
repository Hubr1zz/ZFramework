using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Bootstrap;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class GameManagerCampaignLoopPlayModeTests
    {
        private const int FrameTimeout = 600;
        private GameObject managerObject;
        private PlayableHuntDestinationCatalog destinationCatalog;
        private PlayableCampaignContentCandidate contentCandidate;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ResetContentAssembly();
            contentCandidate = null;
            destinationCatalog = null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (managerObject != null)
                Object.Destroy(managerObject);
            yield return null;
            ResetContentAssembly();
        }

        [UnityTest]
        public IEnumerator PublicCommands_CompleteOneYearLoopAndAllowNextDeparture()
        {
            var persistence = new MemoryCampaignPersistence();
            GameManager manager = CreateProductionManager(persistence);
            yield return WaitForSettlementIdle(manager);
            int initialYear = manager.SettlementData.CurrentYear;
            int hunterId = manager.SettlementData.GetAliveHunters()[0].InstanceId;

            UniTask<CampaignPhaseTransitionResult>.Awaiter bareTransition = manager.TransitionToPhaseAsync(GamePhase.Hunt).GetAwaiter();
            yield return WaitForCompletion(bareTransition);
            CampaignPhaseTransitionResult bareTransitionResult = bareTransition.GetResult();
            Assert.That(bareTransitionResult.Succeeded, Is.False, "正式运行不得通过旧阶段 API 绕过出发名册门禁。");

            UniTask<SettlementDepartureCommandResult>.Awaiter departure = manager.DepartForHuntAsync(new[] { hunterId }, GetDestination(initialYear)).GetAwaiter();
            yield return WaitForCompletion(departure);
            SettlementDepartureCommandResult departureResult = departure.GetResult();
            Assert.That(departureResult.Succeeded, Is.True, departureResult.Reason);
            Assert.That(manager.CurrentGamePhase, Is.EqualTo(GamePhase.Hunt));
            Assert.That(manager.ActiveHuntHunters, Has.Count.EqualTo(1));
            Assert.That(manager.SettlementData.DepartingHunterIds, Is.Empty);

            UniTask<HuntRetreatCommandResult>.Awaiter retreat = manager.RequestRetreatAsync().GetAwaiter();
            yield return WaitForCompletion(retreat);
            HuntRetreatCommandResult retreatResult = retreat.GetResult();
            Assert.That(retreatResult.Succeeded, Is.True, retreatResult.Reason);
            yield return WaitForSettlementIdle(manager);

            Assert.That(manager.CurrentGamePhase, Is.EqualTo(GamePhase.Settlement));
            Assert.That(manager.SettlementData.CurrentYear, Is.EqualTo(initialYear + 1));
            Assert.That(manager.SettlementData.HuntHistory, Has.Count.EqualTo(1));
            Assert.That(manager.SettlementData.PendingHuntReturn, Is.Null);
            Assert.That(manager.SettlementData.DepartingHunterIds, Is.Empty);
            Assert.That(persistence.HasAppliedPendingSave(initialYear + 1), Is.True, "缺少已应用但仍保留回营检查点的第一阶段存档。");

            UniTask<SettlementDepartureCommandResult>.Awaiter nextDeparture = manager.DepartForHuntAsync(new[] { hunterId }, GetDestination(manager.SettlementData.CurrentYear)).GetAwaiter();
            yield return WaitForCompletion(nextDeparture);
            SettlementDepartureCommandResult nextDepartureResult = nextDeparture.GetResult();
            Assert.That(nextDepartureResult.Succeeded, Is.True, nextDepartureResult.Reason);
            Assert.That(manager.CurrentGamePhase, Is.EqualTo(GamePhase.Hunt));
        }

        [UnityTest]
        public IEnumerator FailedReturnCheckpoint_LeavesCampaignInHuntWithoutAdvancingYear()
        {
            var persistence = new MemoryCampaignPersistence { RejectPendingReturn = true };
            GameManager manager = CreateProductionManager(persistence);
            yield return WaitForSettlementIdle(manager);
            int initialYear = manager.SettlementData.CurrentYear;
            int hunterId = manager.SettlementData.GetAliveHunters()[0].InstanceId;

            UniTask<SettlementDepartureCommandResult>.Awaiter departure = manager.DepartForHuntAsync(new[] { hunterId }, GetDestination(initialYear)).GetAwaiter();
            yield return WaitForCompletion(departure);
            SettlementDepartureCommandResult departureResult = departure.GetResult();
            Assert.That(departureResult.Succeeded, Is.True, departureResult.Reason);

            UniTask<HuntRetreatCommandResult>.Awaiter retreat = manager.RequestRetreatAsync().GetAwaiter();
            yield return WaitForCompletion(retreat);
            HuntRetreatCommandResult retreatResult = retreat.GetResult();
            Assert.That(retreatResult.Succeeded, Is.False);
            Assert.That(manager.CurrentGamePhase, Is.EqualTo(GamePhase.Hunt));
            Assert.That(manager.SettlementData.CurrentYear, Is.EqualTo(initialYear));
            Assert.That(manager.SettlementData.PendingHuntReturn, Is.Null);
            Assert.That(manager.SettlementData.HuntHistory, Is.Empty);
        }

        [UnityTest]
        public IEnumerator DelayedAppliedReturnSave_BlocksNextDepartureUntilPersistenceCompletes()
        {
            var persistence = new MemoryCampaignPersistence { DelayAppliedReturn = true };
            GameManager manager = CreateProductionManager(persistence);
            yield return WaitForSettlementIdle(manager);
            int year = manager.SettlementData.CurrentYear;
            int hunterId = manager.SettlementData.GetAliveHunters()[0].InstanceId;

            UniTask<SettlementDepartureCommandResult>.Awaiter departure = manager.DepartForHuntAsync(new[] { hunterId }, GetDestination(year)).GetAwaiter();
            yield return WaitForCompletion(departure);
            SettlementDepartureCommandResult departureResult = departure.GetResult();
            Assert.That(departureResult.Succeeded, Is.True, departureResult.Reason);

            UniTask<HuntRetreatCommandResult>.Awaiter retreat = manager.RequestRetreatAsync().GetAwaiter();
            yield return WaitForCompletion(retreat);
            HuntRetreatCommandResult retreatResult = retreat.GetResult();
            Assert.That(retreatResult.Succeeded, Is.True, retreatResult.Reason);
            yield return WaitForAppliedReturnSave(persistence);

            UniTask<CampaignPhaseTransitionResult>.Awaiter interruptedTransition = manager.TransitionToPhaseAsync(GamePhase.BossFight).GetAwaiter();
            yield return WaitForCompletion(interruptedTransition);
            CampaignPhaseTransitionResult interruptedTransitionResult = interruptedTransition.GetResult();
            Assert.That(interruptedTransitionResult.Succeeded, Is.False, "回营事务未结束时不得切换到其他阶段。");
            Assert.That(manager.CurrentGamePhase, Is.EqualTo(GamePhase.Settlement));
            Assert.That(manager.IsHuntReturnInFlight, Is.True);

            UniTask<SettlementDepartureCommandResult>.Awaiter blockedDeparture = manager.DepartForHuntAsync(new[] { hunterId }, GetDestination(year + 1)).GetAwaiter();
            yield return WaitForCompletion(blockedDeparture);
            Assert.That(blockedDeparture.GetResult().Succeeded, Is.False, "清理检查点持久化期间不得发起下一场远征。");

            persistence.CompleteAppliedReturnSave();
            yield return WaitForSettlementIdle(manager);
            UniTask<SettlementDepartureCommandResult>.Awaiter nextDeparture = manager.DepartForHuntAsync(new[] { hunterId }, GetDestination(year + 1)).GetAwaiter();
            yield return WaitForCompletion(nextDeparture);
            SettlementDepartureCommandResult nextDepartureResult = nextDeparture.GetResult();
            Assert.That(nextDepartureResult.Succeeded, Is.True, nextDepartureResult.Reason);
        }

        [UnityTest]
        public IEnumerator DeferredStartup_WaitsWithoutCreatingOrSavingCampaignRuntime()
        {
            var persistence = new MemoryCampaignPersistence();
            GameManager manager = CreateProductionManager(persistence, true);
            yield return null;
            yield return null;

            Assert.That(manager.CampaignStartupState, Is.EqualTo(CampaignStartupState.AwaitingChoice));
            Assert.That(manager.IsCampaignRuntimeActive, Is.False);
            Assert.That(manager.IsCampaignActionSessionActive, Is.False);
            Assert.That(manager.SettlementData, Is.Null);
            Assert.That(persistence.Snapshots, Is.Empty);
            Assert.That(persistence.DeleteCount, Is.Zero);

            UniTask<CampaignPhaseTransitionResult>.Awaiter transition = manager.TransitionToPhaseAsync(GamePhase.Settlement).GetAwaiter();
            yield return WaitForCompletion(transition);
            Assert.That(transition.GetResult().Succeeded, Is.False);
        }

        [UnityTest]
        public IEnumerator StartNewCampaign_DeletesOnceAndPublishesRuntimeOnce()
        {
            var persistence = new MemoryCampaignPersistence();
            GameManager manager = CreateProductionManager(persistence, true);
            yield return null;

            UniTask<CampaignStartupResult>.Awaiter first = manager.StartNewCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(first);
            CampaignStartupResult firstResult = first.GetResult();
            UniTask<CampaignStartupResult>.Awaiter duplicate = manager.StartNewCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(duplicate);

            Assert.That(firstResult.Succeeded, Is.True, firstResult.Reason);
            Assert.That(duplicate.GetResult().Succeeded, Is.False);
            Assert.That(persistence.DeleteCount, Is.EqualTo(1));
            Assert.That(manager.CampaignStartupState, Is.EqualTo(CampaignStartupState.Active));
            Assert.That(manager.SettlementData, Is.Not.Null);
            yield return WaitForSettlementIdle(manager);
        }

        [UnityTest]
        public IEnumerator StartNewCampaign_DeleteFailureKeepsEntryRetryable()
        {
            var persistence = new MemoryCampaignPersistence { RejectDelete = true };
            GameManager manager = CreateProductionManager(persistence, true);
            yield return null;

            UniTask<CampaignStartupResult>.Awaiter rejected = manager.StartNewCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(rejected);
            Assert.That(rejected.GetResult().Succeeded, Is.False);
            Assert.That(manager.CampaignStartupState, Is.EqualTo(CampaignStartupState.AwaitingChoice));
            Assert.That(manager.SettlementData, Is.Null);
            Assert.That(manager.IsCampaignActionSessionActive, Is.False);

            persistence.RejectDelete = false;
            UniTask<CampaignStartupResult>.Awaiter retry = manager.StartNewCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(retry);
            CampaignStartupResult retryResult = retry.GetResult();
            Assert.That(retryResult.Succeeded, Is.True, retryResult.Reason);
            Assert.That(persistence.DeleteCount, Is.EqualTo(2));
            Assert.That(manager.ActionEnvironmentInstallers.InstallerCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ContinueCampaign_PublishesPreparedSettlementWithoutNewYearProjection()
        {
            var source = new SettlementManager();
            source.EnsureStartingConditions();
            source.Data.CurrentYear = 7;
            source.Data.AddResource("startup-test-resource", 3);
            var persistence = new MemoryCampaignPersistence { SnapshotToLoad = ActiveHuntSnapshotAdapter.CaptureSettlement(source.Data) };
            GameManager manager = CreateProductionManager(persistence, true);
            yield return null;

            UniTask<CampaignStartupResult>.Awaiter load = manager.ContinueCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(load);
            CampaignStartupResult result = load.GetResult();

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(manager.CampaignStartupState, Is.EqualTo(CampaignStartupState.Active));
            Assert.That(manager.SettlementData.CurrentYear, Is.EqualTo(7));
            Assert.That(manager.SettlementData.GetResource("startup-test-resource"), Is.EqualTo(3));
            Assert.That(persistence.LoadCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ContinueCampaign_DelayedLoadRejectsDuplicateAndKeepsAwaitingStateOnFailure()
        {
            var persistence = new MemoryCampaignPersistence { DelayLoad = true };
            GameManager manager = CreateProductionManager(persistence, true);
            yield return null;

            UniTask<CampaignStartupResult>.Awaiter first = manager.ContinueCampaignAsync().GetAwaiter();
            yield return null;
            Assert.That(manager.CampaignStartupState, Is.EqualTo(CampaignStartupState.Loading));
            UniTask<CampaignStartupResult>.Awaiter duplicate = manager.ContinueCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(duplicate);
            Assert.That(duplicate.GetResult().Succeeded, Is.False);
            persistence.CompleteLoad(null);
            yield return WaitForCompletion(first);

            Assert.That(first.GetResult().Succeeded, Is.False);
            Assert.That(manager.CampaignStartupState, Is.EqualTo(CampaignStartupState.AwaitingChoice));
            Assert.That(manager.SettlementData, Is.Null);
            Assert.That(manager.IsCampaignActionSessionActive, Is.False);
            Assert.That(persistence.LoadCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ContinueCampaign_RestoresActiveHuntFromDeferredEntry()
        {
            var persistence = new MemoryCampaignPersistence();
            GameManager sourceManager = CreateProductionManager(persistence, true);
            yield return null;
            UniTask<CampaignStartupResult>.Awaiter start = sourceManager.StartNewCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(start);
            Assert.That(start.GetResult().Succeeded, Is.True);
            yield return WaitForSettlementIdle(sourceManager);
            int hunterId = sourceManager.SettlementData.GetAliveHunters()[0].InstanceId;
            UniTask<SettlementDepartureCommandResult>.Awaiter departure = sourceManager.DepartForHuntAsync(new[] { hunterId }, GetDestination(sourceManager.SettlementData.CurrentYear)).GetAwaiter();
            yield return WaitForCompletion(departure);
            SettlementDepartureCommandResult departureResult = departure.GetResult();
            Assert.That(departureResult.Succeeded, Is.True, departureResult.Reason);
            yield return null;
            CampaignSnapshot activeSnapshot = JsonUtility.FromJson<CampaignSnapshot>(persistence.Payload);
            Assert.That(activeSnapshot?.HasActiveHunt, Is.True);

            Object.Destroy(managerObject);
            managerObject = null;
            yield return null;
            string contentBundleId = activeSnapshot.ActiveHunt.ContentBundleId;
            activeSnapshot.ActiveHunt.ContentBundleId = "missing-startup-bundle";
            persistence.SnapshotToLoad = activeSnapshot;
            GameManager restoredManager = CreateProductionManager(persistence, true);
            yield return null;
            UniTask<CampaignStartupResult>.Awaiter rejectedRestore = restoredManager.ContinueCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(rejectedRestore);
            Assert.That(rejectedRestore.GetResult().Succeeded, Is.False);
            Assert.That(restoredManager.CampaignStartupState, Is.EqualTo(CampaignStartupState.AwaitingChoice));
            Assert.That(restoredManager.SettlementData, Is.Null);
            Assert.That(restoredManager.IsCampaignActionSessionActive, Is.False);
            Assert.That(restoredManager.IsHuntActionSessionActive, Is.False);
            Assert.That(restoredManager.ActionEnvironmentInstallers.InstallerCount, Is.Zero);

            activeSnapshot.ActiveHunt.ContentBundleId = contentBundleId;
            UniTask<CampaignStartupResult>.Awaiter restore = restoredManager.ContinueCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(restore);
            CampaignStartupResult restoreResult = restore.GetResult();

            Assert.That(restoreResult.Succeeded, Is.True, restoreResult.Reason);
            Assert.That(restoredManager.CampaignStartupState, Is.EqualTo(CampaignStartupState.Active));
            Assert.That(restoredManager.CurrentGamePhase, Is.EqualTo(GamePhase.Hunt));
            Assert.That(restoredManager.IsHuntActionSessionActive, Is.True);
            Assert.That(restoredManager.ActiveHuntHunters, Has.Count.EqualTo(1));
        }

        private GameManager CreateProductionManager(ICampaignPersistencePort persistence, bool deferStartup = false)
        {
            if (contentCandidate == null)
            {
                PlayableBootstrapSettings settings = Resources.Load<PlayableBootstrapSettings>("HuntingInDarkness/PlayableBootstrapSettings");
                Assert.That(settings, Is.Not.Null);
                PlayableSymptomRuntime.Configure(settings.Symptoms);
                Assert.That(PlayableCampaignContentAssembler.TryBuild(settings, out contentCandidate, out PlayableContentDiagnosticReport buildReport), Is.True, buildReport.ToString());
                Assert.That(PlayableCampaignContentAssembler.Install(contentCandidate, out PlayableContentDiagnosticReport installReport), Is.True, installReport.ToString());
                destinationCatalog = contentCandidate.HuntDestinations;
            }

            managerObject = new GameObject("GameManager Campaign Loop Smoke");
            managerObject.SetActive(false);
            var manager = managerObject.AddComponent<GameManager>();
            manager.ConfigurePlayableRuntime(contentCandidate.DefaultBattleSetup, contentCandidate.CellSize);
            manager.ConfigureSettlementContent(contentCandidate.SettlementContent);
            manager.ConfigureWorkshopContent(contentCandidate.WorkshopContent);
            Assert.That(manager.ConfigurePlayableStartup(deferStartup), Is.True);
            manager.SetPlayableEventInput(new ImmediateEventInput());
            Assert.That(manager.ConfigureTabletopInteraction(new ImmediateTabletopInteraction()), Is.True);
            Assert.That(manager.ConfigureCampaignPersistence(persistence), Is.True);
            managerObject.SetActive(true);
            Assert.That(manager.ConfigureCampaignPersistence(new MemoryCampaignPersistence()), Is.False, "Awake 后即使停用对象也不得替换持久化端口。");
            return manager;
        }

        private PlayableHuntDestination GetDestination(int year)
        {
            List<PlayableHuntDestination> destinations = destinationCatalog.GetAvailable(year);
            Assert.That(destinations, Is.Not.Empty, $"第 {year} 年缺少可用狩猎目的地。");
            return destinations[0];
        }

        private static IEnumerator WaitForSettlementIdle(GameManager manager)
        {
            int stableFrames = 0;
            for (int frame = 0; frame < FrameTimeout; frame++)
            {
                bool ready = manager.SettlementData != null && manager.IsCampaignActionSessionActive && manager.CurrentGamePhase == GamePhase.Settlement && !manager.IsHuntReturnInFlight && !manager.IsSettlementActionSessionRunning && manager.IsSettlementEventRestoreReady && manager.SettlementData.PendingHuntReturn == null;
                stableFrames = ready ? stableFrames + 1 : 0;
                if (stableFrames >= 2)
                    yield break;
                yield return new WaitForFixedUpdate();
            }
            Assert.Fail($"等待营地 Runner 空闲超时：data={manager.SettlementData != null}, campaign={manager.IsCampaignActionSessionActive}, phase={manager.CurrentGamePhase}, running={manager.IsSettlementActionSessionRunning}, restore={manager.IsSettlementEventRestoreReady}。");
        }

        private static IEnumerator WaitForCompletion<T>(UniTask<T>.Awaiter awaiter)
        {
            for (int frame = 0; frame < FrameTimeout; frame++)
            {
                if (awaiter.IsCompleted)
                    yield break;
                yield return new WaitForFixedUpdate();
            }
            Assert.Fail("等待异步命令完成超时。");
        }

        private static IEnumerator WaitForAppliedReturnSave(MemoryCampaignPersistence persistence)
        {
            for (int frame = 0; frame < FrameTimeout; frame++)
            {
                if (persistence.IsAppliedReturnSavePending)
                    yield break;
                yield return new WaitForFixedUpdate();
            }
            Assert.Fail($"等待已应用回营状态存档开始超时：{persistence.DescribeSnapshots()}。");
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

        private sealed class ImmediateEventInput : IPlayableEventInput
        {
            public UniTask ConfirmNarrativeAsync(EventData gameEvent, HunterInstance actor, CancellationToken cancellationToken) => UniTask.CompletedTask;

            public UniTask<PlayableEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, CancellationToken cancellationToken)
            {
                HunterInstance selectedActor = actor ?? (hunters != null && hunters.Count > 0 ? hunters[0] : null);
                return UniTask.FromResult(new PlayableEventChoiceSelection(0, selectedActor));
            }

            public UniTask<PlayableEventCheckDecision> PresentCheckAsync(PlayableEventChoiceTransaction transaction, CancellationToken cancellationToken) => UniTask.FromResult(PlayableEventCheckDecision.Accept);

            public UniTask ConfirmResultAsync(EventData gameEvent, EventResolutionResult result, CancellationToken cancellationToken) => UniTask.CompletedTask;
        }

        private sealed class ImmediateTabletopInteraction : ITabletopRandomInteractionPresenter
        {
            public UniTask<TabletopRandomInteractionResult> PresentAsync(TabletopRandomInteractionRequest request, CancellationToken cancellationToken)
            {
                var values = new List<int>(request.Count);
                var cardIds = new List<string>(request.Count);
                for (int index = 0; index < request.Count; index++)
                {
                    values.Add(1);
                    if (request.Kind != TabletopRandomInteractionKind.PhysicalDice)
                        cardIds.Add($"{request.DeckId}:{index}");
                }
                return UniTask.FromResult(new TabletopRandomInteractionResult(request.InteractionId, values, cardIds));
            }
        }

        private sealed class MemoryCampaignPersistence : ICampaignPersistencePort
        {
            public bool RejectPendingReturn { get; set; }
            public bool DelayAppliedReturn { get; set; }
            public bool DelayLoad { get; set; }
            public CampaignSnapshot SnapshotToLoad { get; set; }
            public int DeleteCount { get; private set; }
            public int LoadCount { get; private set; }
            public string Payload { get; private set; }
            public List<CampaignSnapshot> Snapshots { get; } = new();
            private List<bool> pendingReturnFlags = new();
            public bool IsAppliedReturnSavePending => appliedReturnSaveCompletion != null;
            private UniTaskCompletionSource<bool> appliedReturnSaveCompletion;
            private UniTaskCompletionSource<CampaignSnapshot> loadCompletion;
            private bool hasDelayedAppliedReturn;

            public UniTask<bool> TrySavePayloadAsync(string payload, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CampaignSnapshot snapshot = JsonUtility.FromJson<CampaignSnapshot>(payload);
                Snapshots.Add(snapshot);
                bool hasPendingReturn = payload.Contains("\"PendingHuntReturn\": {");
                pendingReturnFlags.Add(hasPendingReturn);
                if (RejectPendingReturn && hasPendingReturn)
                    return UniTask.FromResult(false);
                Payload = payload;
                if (DelayAppliedReturn && !hasDelayedAppliedReturn && snapshot?.Settlement?.CurrentYear > 1 && hasPendingReturn && snapshot.Settlement.HuntHistory.Count == 1)
                {
                    hasDelayedAppliedReturn = true;
                    appliedReturnSaveCompletion = new UniTaskCompletionSource<bool>();
                    return appliedReturnSaveCompletion.Task;
                }
                return UniTask.FromResult(true);
            }

            public bool TrySavePayloadImmediate(string payload)
            {
                Payload = payload;
                return true;
            }

            public UniTask<bool> HasSaveAsync(CancellationToken cancellationToken = default) => UniTask.FromResult(!string.IsNullOrWhiteSpace(Payload));

            public UniTask<CampaignSnapshot> LoadAsync(CancellationToken cancellationToken = default)
            {
                LoadCount++;
                if (!DelayLoad)
                {
                    NormalizeLoadedSnapshot(SnapshotToLoad);
                    return UniTask.FromResult(SnapshotToLoad);
                }
                loadCompletion = new UniTaskCompletionSource<CampaignSnapshot>();
                return loadCompletion.Task;
            }

            public bool RejectDelete { get; set; }

            public UniTask<bool> TryDeleteAsync(CancellationToken cancellationToken = default)
            {
                DeleteCount++;
                if (RejectDelete) return UniTask.FromResult(false);
                Payload = null;
                SnapshotToLoad = null;
                return UniTask.FromResult(true);
            }

            public void CompleteLoad(CampaignSnapshot snapshot)
            {
                NormalizeLoadedSnapshot(snapshot);
                UniTaskCompletionSource<CampaignSnapshot> completion = loadCompletion;
                loadCompletion = null;
                completion?.TrySetResult(snapshot);
            }

            private static void NormalizeLoadedSnapshot(CampaignSnapshot snapshot)
            {
                if (snapshot?.Settlement?.PendingHuntReturn != null && string.IsNullOrWhiteSpace(snapshot.Settlement.PendingHuntReturn.RecordId))
                    snapshot.Settlement.PendingHuntReturn = null;
            }

            public void CompleteAppliedReturnSave()
            {
                UniTaskCompletionSource<bool> completion = appliedReturnSaveCompletion;
                appliedReturnSaveCompletion = null;
                completion?.TrySetResult(true);
            }

            public string DescribeSnapshots()
            {
                var descriptions = new List<string>();
                for (int index = 0; index < Snapshots.Count; index++)
                {
                    CampaignSnapshot snapshot = Snapshots[index];
                    descriptions.Add($"year={snapshot?.Settlement?.CurrentYear},pending={pendingReturnFlags[index]},history={snapshot?.Settlement?.HuntHistory?.Count}");
                }
                return string.Join(";", descriptions);
            }

            public bool HasAppliedPendingSave(int year)
            {
                for (int index = 0; index < Snapshots.Count; index++)
                    if (Snapshots[index]?.Settlement?.CurrentYear == year && pendingReturnFlags[index]) return true;
                return false;
            }

        }
    }
}
