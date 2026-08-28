using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class CampaignPersistenceCoordinatorTests
    {
        [Test]
        public void TryCapture_WhenSnapshotFails_PreservesStablePayload()
        {
            var persistence = new DeferredPersistence();
            var coordinator = new CampaignPersistenceCoordinator(persistence, FailCapture);
            coordinator.Adopt("stable-payload");

            bool captured = coordinator.TryCapture(false, out string payload, out string reason);

            Assert.That(captured, Is.False);
            Assert.That(payload, Is.Empty);
            Assert.That(reason, Is.EqualTo("capture rejected"));
            Assert.That(coordinator.StablePayload, Is.EqualTo("stable-payload"));
        }

        [Test]
        public async Task TrySaveAsync_WhenResetCompletesFirst_RejectsStaleCompletion()
        {
            var persistence = new DeferredPersistence();
            var coordinator = new CampaignPersistenceCoordinator(persistence, CaptureSettlement);

            UniTask<bool> save = coordinator.TrySaveAsync(false, CancellationToken.None);
            coordinator.Reset();
            persistence.CompleteSave(true);

            Assert.That(await save, Is.False);
            Assert.That(coordinator.StablePayload, Is.Null);
            Assert.That(persistence.InvalidationCount, Is.EqualTo(1));
            Assert.That(coordinator.Status.State, Is.EqualTo(CampaignSaveState.Idle));
            Assert.That(coordinator.LastFailureReason, Is.Empty);
        }

        [Test]
        public void TrySaveImmediate_WhenStorageRejects_PreservesStablePayload()
        {
            var persistence = new DeferredPersistence { ImmediateSaveResult = false };
            var coordinator = new CampaignPersistenceCoordinator(persistence, CaptureSettlement);
            coordinator.Adopt("stable-payload");

            Assert.That(coordinator.TrySaveImmediate("candidate-payload"), Is.False);
            Assert.That(coordinator.StablePayload, Is.EqualTo("stable-payload"));
        }

        [Test]
        public async Task OlderSuccessfulSave_ReturnsTrueButCannotReplaceNewerFailedStatus()
        {
            var persistence = new DeferredPersistence();
            var coordinator = new CampaignPersistenceCoordinator(persistence, CaptureSettlement);

            UniTask<bool> older = coordinator.TrySaveAsync(false, CancellationToken.None);
            UniTask<bool> newer = coordinator.TrySaveAsync(false, CancellationToken.None);
            persistence.CompleteSave(false, 1);

            Assert.That(await newer, Is.False);
            persistence.CompleteSave(true, 0);

            Assert.That(await older, Is.True);
            Assert.That(coordinator.Status.State, Is.EqualTo(CampaignSaveState.Failed));
            Assert.That(coordinator.Status.Reason, Does.Contain("异步保存"));
        }

        [Test]
        public async Task CancelledRetry_RestoresPriorFailure()
        {
            var persistence = new DeferredPersistence();
            var coordinator = new CampaignPersistenceCoordinator(persistence, CaptureSettlement);
            UniTask<bool> initial = coordinator.TrySaveAsync(false, CancellationToken.None);
            persistence.CompleteSave(false);
            Assert.That(await initial, Is.False);
            string reason = coordinator.Status.Reason;
            using var cancellation = new CancellationTokenSource();

            UniTask<bool> retry = coordinator.RetryPendingSaveAsync(false, cancellation.Token);
            cancellation.Cancel();
            persistence.CompleteSave(false, 1);

            Assert.That(await retry, Is.False);
            Assert.That(coordinator.Status.State, Is.EqualTo(CampaignSaveState.Failed));
            Assert.That(coordinator.Status.Reason, Is.EqualTo(reason));
        }

        [Test]
        public async Task ResetDetachesOldRetryAndAllowsNewOwner()
        {
            var persistence = new DeferredPersistence();
            var coordinator = new CampaignPersistenceCoordinator(persistence, CaptureSettlement);
            UniTask<bool> initial = coordinator.TrySaveAsync(false, CancellationToken.None);
            persistence.CompleteSave(false);
            Assert.That(await initial, Is.False);

            UniTask<bool> oldRetry = coordinator.RetryPendingSaveAsync(false, CancellationToken.None);
            coordinator.Reset();
            Assert.That(await oldRetry, Is.False);

            UniTask<bool> newSave = coordinator.TrySaveAsync(false, CancellationToken.None);
            persistence.CompleteSave(false, 2);
            Assert.That(await newSave, Is.False);
            UniTask<bool> newRetry = coordinator.RetryPendingSaveAsync(false, CancellationToken.None);
            persistence.CompleteSave(true, 1);
            Assert.That(persistence.RequestCount, Is.EqualTo(4));
            UniTask<bool> duplicate = coordinator.RetryPendingSaveAsync(false, CancellationToken.None);
            Assert.That(persistence.RequestCount, Is.EqualTo(4));
            persistence.CompleteSave(false, 3);

            Assert.That(await newRetry, Is.False);
            Assert.That(await duplicate, Is.False);
        }

        [Test]
        public async Task RetryRecapturesLatestPayload()
        {
            int currentYear = 1;
            var persistence = new DeferredPersistence();
            var coordinator = new CampaignPersistenceCoordinator(persistence, CaptureSettlementWithYear);

            UniTask<bool> initial = coordinator.TrySaveAsync(false, CancellationToken.None);
            persistence.CompleteSave(false);
            Assert.That(await initial, Is.False);
            currentYear = 7;

            UniTask<bool> retry = coordinator.RetryPendingSaveAsync(false, CancellationToken.None);
            persistence.CompleteSave(true, 1);

            Assert.That(await retry, Is.True);
            CampaignSnapshot snapshot = JsonUtility.FromJson<CampaignSnapshot>(persistence.Payloads[1]);
            Assert.That(snapshot.Settlement.CurrentYear, Is.EqualTo(7));

            bool CaptureSettlementWithYear(bool includeActiveHunt, out CampaignSnapshot snapshot, out string reason)
            {
                snapshot = new CampaignSnapshot
                {
                    CampaignSchemaVersion = CampaignSnapshot.CurrentSchemaVersion,
                    Settlement = new SettlementInstance { CurrentYear = currentYear }
                };
                reason = string.Empty;
                return true;
            }
        }

        [Test]
        public async Task DuplicateRetry_UsesSingleStorageCall()
        {
            var persistence = new DeferredPersistence();
            var coordinator = new CampaignPersistenceCoordinator(persistence, CaptureSettlement);
            UniTask<bool> initial = coordinator.TrySaveAsync(false, CancellationToken.None);
            persistence.CompleteSave(false);
            Assert.That(await initial, Is.False);

            UniTask<bool> first = coordinator.RetryPendingSaveAsync(false, CancellationToken.None);
            UniTask<bool> second = coordinator.RetryPendingSaveAsync(false, CancellationToken.None);
            Assert.That(persistence.RequestCount, Is.EqualTo(2));
            persistence.CompleteSave(true, 1);

            Assert.That(await first, Is.True);
            Assert.That(await second, Is.True);
        }

        [Test]
        public async Task EncounterExecute_WhenConcurrentRequestArrives_RejectsOnlySecondRequest()
        {
            var host = new DeferredEncounterHost();
            var transaction = new CampaignEncounterHandoffTransaction(host);
            CampaignEncounterRequest request = CreateEncounterRequest("encounter-owner");

            UniTask<CampaignEncounterStartResult> first = transaction.ExecuteAsync(request, CancellationToken.None);
            CampaignEncounterStartResult second = await transaction.ExecuteAsync(CreateEncounterRequest("encounter-duplicate"), CancellationToken.None);
            host.Complete(0, new CampaignEncounterStartResult(true, request.EncounterId, string.Empty));

            Assert.That(second.Succeeded, Is.False);
            Assert.That(second.Reason, Does.Contain("正在执行"));
            Assert.That((await first).Succeeded, Is.True);
            Assert.That(host.RequestCount, Is.EqualTo(1));
        }

        [Test]
        public async Task EncounterExecute_WhenResetInvalidatesOwner_OldContinuationCannotClearNewOwner()
        {
            var host = new DeferredEncounterHost();
            var transaction = new CampaignEncounterHandoffTransaction(host);

            UniTask<CampaignEncounterStartResult> stale = transaction.ExecuteAsync(CreateEncounterRequest("encounter-stale"), CancellationToken.None);
            transaction.Reset();
            UniTask<CampaignEncounterStartResult> current = transaction.ExecuteAsync(CreateEncounterRequest("encounter-current"), CancellationToken.None);
            host.Complete(0, new CampaignEncounterStartResult(true, "encounter-stale", string.Empty));
            CampaignEncounterStartResult blocked = await transaction.ExecuteAsync(CreateEncounterRequest("encounter-third"), CancellationToken.None);
            host.Complete(1, new CampaignEncounterStartResult(true, "encounter-current", string.Empty));

            Assert.That((await stale).Succeeded, Is.False);
            Assert.That(blocked.Succeeded, Is.False);
            Assert.That((await current).Succeeded, Is.True);
            Assert.That(host.RequestCount, Is.EqualTo(2));
        }

        private static CampaignEncounterRequest CreateEncounterRequest(string encounterId)
            => new(System.Guid.NewGuid(), encounterId, CampaignEncounterSourceKind.HuntEvent, GamePhase.Hunt, Vector2Int.zero, "test", "route");

        private static bool FailCapture(bool includeActiveHunt, out CampaignSnapshot snapshot, out string reason)
        {
            snapshot = null;
            reason = "capture rejected";
            return false;
        }

        private static bool CaptureSettlement(bool includeActiveHunt, out CampaignSnapshot snapshot, out string reason)
        {
            snapshot = new CampaignSnapshot
            {
                CampaignSchemaVersion = CampaignSnapshot.CurrentSchemaVersion,
                Settlement = new SettlementInstance()
            };
            reason = string.Empty;
            return true;
        }

        private sealed class DeferredPersistence : ICampaignPersistencePort
        {
            private readonly List<UniTaskCompletionSource<bool>> saveCompletions = new();

            internal bool ImmediateSaveResult { get; set; } = true;
            internal int InvalidationCount { get; private set; }
            internal List<string> Payloads { get; } = new();
            internal int RequestCount => Payloads.Count;

            public void InvalidatePendingWrites() => InvalidationCount++;
            public UniTask<bool> TrySavePayloadAsync(string payload, CancellationToken cancellationToken = default)
            {
                Payloads.Add(payload);
                var completion = new UniTaskCompletionSource<bool>();
                saveCompletions.Add(completion);
                return completion.Task;
            }
            public bool TrySavePayloadImmediate(string payload) => ImmediateSaveResult;
            public UniTask<bool> HasSaveAsync(CancellationToken cancellationToken = default) => UniTask.FromResult(false);
            public UniTask<CampaignSnapshot> LoadAsync(CancellationToken cancellationToken = default) => UniTask.FromResult<CampaignSnapshot>(null);
            public UniTask<bool> TryDeleteAsync(CancellationToken cancellationToken = default) => UniTask.FromResult(true);

            internal void CompleteSave(bool result, int index = 0) => saveCompletions[index].TrySetResult(result);
        }

        private sealed class DeferredEncounterHost : ICampaignEncounterHandoffHost
        {
            private readonly List<UniTaskCompletionSource<CampaignEncounterStartResult>> completions = new();

            public GamePhase CurrentPhase => GamePhase.Hunt;
            public IPlayableCampaignRuntime CampaignRuntime => null;
            public IPlayableHuntRuntime HuntRuntime => null;
            public PlayableHuntActionSession HuntActionSession => null;
            public PlayableSettlementActionSession SettlementActionSession => null;
            public SettlementManager SettlementManager => null;
            public HuntManager HuntManager => null;
            public CampaignPersistenceCoordinator Persistence => null;
            public int RequestCount => completions.Count;

            public bool TryApplyBossFightTransition(out string reason)
            {
                reason = string.Empty;
                return true;
            }

            public UniTask<CampaignEncounterStartResult> RunEncounterActionAsync(CampaignEncounterRequest request, CancellationToken cancellationToken)
            {
                var completion = new UniTaskCompletionSource<CampaignEncounterStartResult>();
                completions.Add(completion);
                return completion.Task;
            }

            internal void Complete(int index, CampaignEncounterStartResult result) => completions[index].TrySetResult(result);
        }
    }
}
