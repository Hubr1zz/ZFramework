using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.Data;

namespace Core
{
    internal interface ICampaignStartupTransactionHost
    {
        IPlayableCampaignRuntime CampaignRuntime { get; }
        void EnsureCampaignShell();
        bool TryRestoreActiveHunt(CampaignSnapshot snapshot, out string reason);
        bool TryStartCampaignRuntime(GamePhase startPhase, bool queueSettlementEvents, out string reason, IPlayableSettlementRuntime preparedSettlement, bool activateOnSuccess);
        void ResetFailedCampaignStartupRuntime();
        UniTask<bool> FinalizePreparedSettlementAsync(SettlementInstance settlement, string payload);
    }

    /// <summary>Owns new/continue campaign persistence, candidate publication, and retryable startup state.</summary>
    internal sealed class CampaignStartupTransaction
    {
        private readonly CampaignStartupLifecycle lifecycle = new();
        private ICampaignPersistencePort persistence;
        private ICampaignStartupTransactionHost host;

        public CampaignStartupTransaction(ICampaignPersistencePort persistence)
        {
            this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        }

        public CampaignStartupState State => lifecycle.State;
        public bool WaitForEntrySelection => lifecycle.WaitForEntrySelection;
        public bool IsRuntimeActive => lifecycle.IsRuntimeActive;
        public ICampaignPersistencePort Persistence => persistence;

        public bool Configure(bool waitForEntrySelection) => lifecycle.Configure(waitForEntrySelection);

        public bool ConfigurePersistence(ICampaignPersistencePort replacement)
        {
            if (replacement == null || host != null)
                return false;
            persistence = replacement;
            return true;
        }

        public void Bind(ICampaignStartupTransactionHost transactionHost)
        {
            if (transactionHost == null)
                throw new ArgumentNullException(nameof(transactionHost));
            if (host != null)
                throw new InvalidOperationException("战役启动事务已经绑定组合根。");
            host = transactionHost;
        }

        public UniTask<bool> HasSaveAsync(CancellationToken cancellationToken = default) => persistence.HasSaveAsync(cancellationToken);

        public UniTask<bool> DeleteSaveAsync(CancellationToken cancellationToken = default) => persistence.TryDeleteAsync(cancellationToken);

        public async UniTask<CampaignStartupResult> StartNewAsync(CancellationToken cancellationToken = default)
        {
            if (!lifecycle.TryBegin(CampaignStartupState.StartingNew, out string beginReason))
                return CampaignStartupResult.Failed(lifecycle.State, beginReason);
            try
            {
                if (host == null)
                    return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "战役启动事务尚未绑定组合根。");
                if (!await persistence.TryDeleteAsync(cancellationToken))
                    return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "旧战役记录未能可靠删除，请重试。");
                if (!host.TryStartCampaignRuntime(GamePhase.Settlement, true, out string startReason, null, true))
                    return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, startReason);
                lifecycle.ActivateRuntime();
                return CampaignStartupResult.Success();
            }
            catch (OperationCanceledException)
            {
                host?.ResetFailedCampaignStartupRuntime();
                return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "开始新战役已取消。");
            }
            catch (Exception exception)
            {
                host?.ResetFailedCampaignStartupRuntime();
                return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, $"开始新战役失败：{exception.Message}");
            }
            finally
            {
                lifecycle.CompleteAttempt();
            }
        }

        public async UniTask<CampaignStartupResult> ContinueAsync(CancellationToken cancellationToken = default)
        {
            if (!lifecycle.TryBegin(CampaignStartupState.Loading, out string beginReason))
                return CampaignStartupResult.Failed(lifecycle.State, beginReason);
            try
            {
                if (host == null)
                    return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "战役启动事务尚未绑定组合根。");
                CampaignSnapshot snapshot = await persistence.LoadAsync(cancellationToken);
                if (snapshot?.Settlement == null)
                    return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "没有可继续的有效战役存档。");
                if (snapshot.HasActiveHunt)
                    return RestoreActiveHunt(snapshot);
                return await RestoreSettlementAsync(snapshot);
            }
            catch (OperationCanceledException)
            {
                host?.ResetFailedCampaignStartupRuntime();
                return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "继续战役已取消。");
            }
            catch (Exception exception)
            {
                host?.ResetFailedCampaignStartupRuntime();
                return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, $"继续战役失败：{exception.Message}");
            }
            finally
            {
                lifecycle.CompleteAttempt();
            }
        }

        public void ActivateRuntime() => lifecycle.ActivateRuntime();

        public void DeactivateRuntime() => lifecycle.DeactivateRuntime();

        private CampaignStartupResult RestoreActiveHunt(CampaignSnapshot snapshot)
        {
            host.EnsureCampaignShell();
            if (host.TryRestoreActiveHunt(snapshot, out string reason))
            {
                lifecycle.ActivateRuntime();
                return CampaignStartupResult.Success();
            }
            host.ResetFailedCampaignStartupRuntime();
            return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, reason);
        }

        private async UniTask<CampaignStartupResult> RestoreSettlementAsync(CampaignSnapshot snapshot)
        {
            IPlayableCampaignRuntime runtime = host.CampaignRuntime;
            if (runtime == null)
                return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "战役运行态尚未初始化。");
            if (!runtime.TryPrepareSettlementRestore(snapshot.Settlement, out IPlayableSettlementRuntime candidate, out string reason))
                return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, reason);
            CampaignSnapshot candidateSnapshot = ActiveHuntSnapshotAdapter.CaptureSettlement(candidate.Data);
            if (!SaveLoadSystem.TryCreatePayload(candidateSnapshot, out string candidatePayload, out reason))
            {
                runtime.ReleaseSettlement(candidate);
                return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, reason);
            }
            if (!host.TryStartCampaignRuntime(GamePhase.Settlement, false, out string startReason, candidate, false))
                return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, startReason);
            if (!await host.FinalizePreparedSettlementAsync(candidate.Data, candidatePayload))
            {
                host.ResetFailedCampaignStartupRuntime();
                return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "战役存档恢复失败，当前运行态已撤销。");
            }
            lifecycle.ActivateRuntime();
            return CampaignStartupResult.Success();
        }
    }
}
