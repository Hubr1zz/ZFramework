using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameplayBase;

namespace Core
{
    internal readonly struct CampaignRestartPayload
    {
        private CampaignRestartPayload(bool succeeded, string payload, string reason)
        {
            Succeeded = succeeded;
            Payload = payload ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Payload { get; }
        public string Reason { get; }
        public static CampaignRestartPayload Success(string payload) => new(true, payload, string.Empty);
        public static CampaignRestartPayload Failed(string reason) => new(false, string.Empty, reason);
    }

    internal readonly struct CampaignRestartTransactionResult
    {
        private CampaignRestartTransactionResult(bool succeeded, string stablePayload, string reason)
        {
            Succeeded = succeeded;
            StablePayload = stablePayload ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string StablePayload { get; }
        public string Reason { get; }
        public static CampaignRestartTransactionResult Success(string stablePayload) => new(true, stablePayload, string.Empty);
        public static CampaignRestartTransactionResult Failed(string reason) => new(false, string.Empty, reason);
    }

    internal delegate CampaignRestartPayload PrepareCampaignRestartPayload(IPlayableSettlementRuntime settlement);

    /// <summary>线性化存档替换与 ZFramework 战役运行世代发布，不负责场景表现。</summary>
    internal sealed class CampaignRestartTransaction
    {
        private readonly IPlayableCampaignRuntime runtime;
        private readonly ICampaignPersistencePort persistence;
        private readonly PrepareCampaignRestartPayload preparePayload;
        private readonly Action<string> reportWarning;

        public CampaignRestartTransaction(IPlayableCampaignRuntime runtime, ICampaignPersistencePort persistence, PrepareCampaignRestartPayload preparePayload, Action<string> reportWarning = null)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            this.preparePayload = preparePayload ?? throw new ArgumentNullException(nameof(preparePayload));
            this.reportWarning = reportWarning;
        }

        public async UniTask<CampaignRestartTransactionResult> ExecuteAsync(string previousStablePayload, CancellationToken cancellationToken)
        {
            IPlayableSettlementRuntime previousSettlement = runtime.Settlement;
            IPlayableHuntRuntime previousHunt = runtime.Hunt;
            if (!runtime.TryPrepareNewSettlement(out IPlayableSettlementRuntime candidateSettlement, out string reason))
                return CampaignRestartTransactionResult.Failed(reason);

            CampaignRestartPayload candidatePayload;
            try
            {
                candidatePayload = preparePayload(candidateSettlement);
            }
            catch (Exception exception)
            {
                ReleaseCandidate(candidateSettlement);
                return CampaignRestartTransactionResult.Failed($"准备新战役快照时发生异常：{exception.Message}");
            }
            if (!candidatePayload.Succeeded)
            {
                ReleaseCandidate(candidateSettlement);
                return CampaignRestartTransactionResult.Failed(candidatePayload.Reason);
            }

            bool deletionConfirmed = false;
            try
            {
                deletionConfirmed = await persistence.TryDeleteAsync(cancellationToken);
                if (!deletionConfirmed)
                {
                    ReleaseCandidate(candidateSettlement);
                    return CampaignRestartTransactionResult.Failed("旧战役记录未能可靠删除，请重试。");
                }
                if (!await persistence.TrySavePayloadAsync(candidatePayload.Payload, cancellationToken))
                {
                    ReleaseCandidate(candidateSettlement);
                    string restoreFailure = await RestoreStorageAsync(previousStablePayload);
                    return CampaignRestartTransactionResult.Failed(AppendFailure("新战役记录未能可靠建立，请重试。", restoreFailure));
                }
            }
            catch (OperationCanceledException)
            {
                ReleaseCandidate(candidateSettlement);
                if (deletionConfirmed)
                {
                    string restoreFailure = await RestoreStorageAsync(previousStablePayload);
                    if (!string.IsNullOrWhiteSpace(restoreFailure))
                        reportWarning?.Invoke($"重启取消后的存档补偿失败：{restoreFailure}");
                }
                throw;
            }
            catch (Exception exception)
            {
                ReleaseCandidate(candidateSettlement);
                string restoreFailure = deletionConfirmed ? await RestoreStorageAsync(previousStablePayload) : string.Empty;
                return CampaignRestartTransactionResult.Failed(AppendFailure($"重建战役记录时发生异常：{exception.Message}", restoreFailure));
            }

            if (!runtime.TrySwapSettlement(previousSettlement, candidateSettlement, out reason))
            {
                ReleaseCandidate(candidateSettlement);
                string restoreFailure = await RestoreStorageAsync(previousStablePayload);
                return CampaignRestartTransactionResult.Failed(AppendFailure(reason, restoreFailure));
            }

            bool huntDetached = previousHunt != null;
            if (huntDetached && !runtime.TrySwapHunt(previousHunt, null, out reason))
                return await RollbackPublishedRuntimeAsync(candidateSettlement, previousSettlement, previousHunt, false, previousStablePayload, reason);
            if (!candidateSettlement.TryActivateActionSession(out reason))
                return await RollbackPublishedRuntimeAsync(candidateSettlement, previousSettlement, previousHunt, huntDetached, previousStablePayload, reason);

            try
            {
                if (runtime.CurrentPhase != GamePhase.Settlement && !runtime.TransitionTo(GamePhase.Settlement))
                    return await RollbackPublishedRuntimeAsync(candidateSettlement, previousSettlement, previousHunt, huntDetached, previousStablePayload, "无法切换到新战役的营地阶段。");
            }
            catch (Exception exception)
            {
                if (runtime.CurrentPhase != GamePhase.Settlement)
                    return await RollbackPublishedRuntimeAsync(candidateSettlement, previousSettlement, previousHunt, huntDetached, previousStablePayload, $"切换到新战役营地阶段时发生异常：{exception.Message}");
                reportWarning?.Invoke($"新战役营地阶段已经切换，但阶段通知存在异常：{exception.Message}");
            }

            previousHunt?.DeactivateActionSession();
            previousSettlement?.DeactivateActionSession();
            ReleaseRetiredRuntime(previousHunt, previousSettlement);
            return CampaignRestartTransactionResult.Success(candidatePayload.Payload);
        }

        private async UniTask<CampaignRestartTransactionResult> RollbackPublishedRuntimeAsync(IPlayableSettlementRuntime candidateSettlement, IPlayableSettlementRuntime previousSettlement, IPlayableHuntRuntime previousHunt, bool huntDetached, string previousStablePayload, string reason)
        {
            var failures = new List<string>();
            if (huntDetached && !runtime.TrySwapHunt(null, previousHunt, out string huntReason))
                failures.Add($"狩猎运行世代恢复失败：{huntReason}");

            bool settlementRestored = runtime.TrySwapSettlement(candidateSettlement, previousSettlement, out string settlementReason);
            if (settlementRestored)
                ReleaseCandidate(candidateSettlement);
            else
                failures.Add($"营地运行世代恢复失败：{settlementReason}");

            string storageFailure = await RestoreStorageAsync(previousStablePayload);
            if (!string.IsNullOrWhiteSpace(storageFailure))
                failures.Add(storageFailure);
            return CampaignRestartTransactionResult.Failed(AppendFailure(reason, failures.Count > 0 ? string.Join("；", failures) : string.Empty));
        }

        private async UniTask<string> RestoreStorageAsync(string previousStablePayload)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(previousStablePayload))
                    return await persistence.TryDeleteAsync(CancellationToken.None) ? string.Empty : "无法清除未提交的新战役记录";
                return persistence.TrySavePayloadImmediate(previousStablePayload) ? string.Empty : "无法恢复上一份稳定战役记录";
            }
            catch (Exception exception)
            {
                return $"恢复上一份战役记录时发生异常：{exception.Message}";
            }
        }

        private void ReleaseCandidate(IPlayableSettlementRuntime candidate)
        {
            try
            {
                runtime.ReleaseSettlement(candidate);
            }
            catch (Exception exception)
            {
                reportWarning?.Invoke($"释放未提交营地运行世代时发生异常：{exception.Message}");
            }
        }

        private void ReleaseRetiredRuntime(IPlayableHuntRuntime previousHunt, IPlayableSettlementRuntime previousSettlement)
        {
            try
            {
                if (previousHunt != null)
                    runtime.ReleaseHunt(previousHunt);
            }
            catch (Exception exception)
            {
                reportWarning?.Invoke($"退役旧狩猎运行世代时发生异常，新战役仍然有效：{exception.Message}");
            }
            try
            {
                if (previousSettlement != null)
                    runtime.ReleaseSettlement(previousSettlement);
            }
            catch (Exception exception)
            {
                reportWarning?.Invoke($"退役旧营地运行世代时发生异常，新战役仍然有效：{exception.Message}");
            }
        }

        private static string AppendFailure(string primary, string secondary)
        {
            if (string.IsNullOrWhiteSpace(secondary)) return primary ?? string.Empty;
            if (string.IsNullOrWhiteSpace(primary)) return secondary;
            return $"{primary}；{secondary}";
        }
    }
}
