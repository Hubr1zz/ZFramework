using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.Data;

namespace Core
{
    internal interface ICampaignStartupTransactionHost
    {
        bool TryStartCampaignRuntime(GamePhase startPhase, bool queueSettlementEvents, out string reason, IPlayableSettlementRuntime preparedSettlement, bool activateOnSuccess);
        void ResetFailedCampaignStartupRuntime();
        UniTask<bool> RestoreSnapshotAsync(CampaignSnapshot snapshot, CancellationToken cancellationToken);
    }

    /// <summary>Owns new/continue campaign persistence, candidate publication, and retryable startup state.</summary>
    internal sealed class CampaignStartupTransaction
    {
        private readonly CampaignStartupLifecycle lifecycle = new();
        private ICampaignPersistencePort persistence;
        private ICampaignStartupTransactionHost host;
        private int operationGeneration;
        private bool disposed;

        public CampaignStartupTransaction(ICampaignPersistencePort persistence)
        {
            this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        }

        public CampaignStartupState State => lifecycle.State;
        public bool WaitForEntrySelection => lifecycle.WaitForEntrySelection;
        public bool IsRuntimeActive => lifecycle.IsRuntimeActive;
        public ICampaignPersistencePort Persistence => persistence;

        public bool Configure(bool waitForEntrySelection) => !disposed && lifecycle.Configure(waitForEntrySelection);

        public bool ConfigurePersistence(ICampaignPersistencePort replacement)
        {
            if (disposed || replacement == null || host != null)
                return false;
            persistence = replacement;
            return true;
        }

        public void Bind(ICampaignStartupTransactionHost transactionHost)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(CampaignStartupTransaction));
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
            int generation = ++operationGeneration;
            try
            {
                if (host == null)
                    return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "战役启动事务尚未绑定组合根。");
                if (!await persistence.TryDeleteAsync(cancellationToken))
                    return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "旧战役记录未能可靠删除，请重试。");
                if (!IsCurrent(generation))
                    return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "战役启动已被新的运行态取代。");
                if (!host.TryStartCampaignRuntime(GamePhase.Settlement, true, out string startReason, null, true))
                    return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, startReason);
                if (!IsCurrent(generation))
                    return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "战役启动已被新的运行态取代。");
                lifecycle.ActivateRuntime();
                return CampaignStartupResult.Success();
            }
            catch (OperationCanceledException)
            {
                if (IsCurrent(generation)) host?.ResetFailedCampaignStartupRuntime();
                return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "开始新战役已取消。");
            }
            catch (Exception exception)
            {
                if (IsCurrent(generation)) host?.ResetFailedCampaignStartupRuntime();
                return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, $"开始新战役失败：{exception.Message}");
            }
            finally
            {
                if (IsCurrent(generation)) lifecycle.CompleteAttempt();
            }
        }

        public async UniTask<CampaignStartupResult> ContinueAsync(CancellationToken cancellationToken = default)
        {
            if (!lifecycle.TryBegin(CampaignStartupState.Loading, out string beginReason))
                return CampaignStartupResult.Failed(lifecycle.State, beginReason);
            int generation = ++operationGeneration;
            try
            {
                if (host == null)
                    return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "战役启动事务尚未绑定组合根。");
                CampaignSnapshot snapshot = await persistence.LoadAsync(cancellationToken);
                if (!IsCurrent(generation))
                    return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "战役恢复已被新的运行态取代。");
                if (snapshot?.Settlement == null)
                    return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "没有可继续的有效战役存档。");
                if (!await host.RestoreSnapshotAsync(snapshot, cancellationToken))
                    return CampaignStartupResult.Failed(lifecycle.IsRuntimeActive ? CampaignStartupState.Active : CampaignStartupState.AwaitingChoice, lifecycle.IsRuntimeActive ? "战役存档已加载，但后置恢复尚未完成。" : "战役存档恢复失败，当前运行态已撤销。");
                if (!IsCurrent(generation))
                    return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "战役恢复已被新的运行态取代。");
                lifecycle.ActivateRuntime();
                return CampaignStartupResult.Success();
            }
            catch (OperationCanceledException)
            {
                if (IsCurrent(generation)) host?.ResetFailedCampaignStartupRuntime();
                return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, "继续战役已取消。");
            }
            catch (Exception exception)
            {
                if (IsCurrent(generation)) host?.ResetFailedCampaignStartupRuntime();
                return CampaignStartupResult.Failed(CampaignStartupState.AwaitingChoice, $"继续战役失败：{exception.Message}");
            }
            finally
            {
                if (IsCurrent(generation)) lifecycle.CompleteAttempt();
            }
        }

        public void ActivateRuntime()
        {
            if (!disposed) lifecycle.ActivateRuntime();
        }

        public void DeactivateRuntime()
        {
            if (!disposed) lifecycle.DeactivateRuntime();
        }

        public void Invalidate()
        {
            if (disposed) return;
            ++operationGeneration;
            lifecycle.DeactivateRuntime();
            lifecycle.CompleteAttempt();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ++operationGeneration;
            host = null;
            lifecycle.DeactivateRuntime();
            lifecycle.CompleteAttempt();
        }

        private bool IsCurrent(int generation) => !disposed && generation == operationGeneration;

    }
}
