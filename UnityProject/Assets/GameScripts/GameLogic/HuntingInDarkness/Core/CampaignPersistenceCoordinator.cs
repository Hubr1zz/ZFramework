using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;

namespace Core
{
    internal delegate bool TryCaptureCampaignSnapshot(bool includeActiveHunt, out CampaignSnapshot snapshot, out string reason);

    internal sealed class CampaignPersistenceCoordinator
    {
        private readonly ICampaignPersistencePort persistence;
        private readonly TryCaptureCampaignSnapshot captureSnapshot;
        private long generation;
        private long saveRevision;
        private long retryEpoch;
        private string stablePayload;
        private CampaignSaveStatus status;
        private UniTaskCompletionSource<bool> retryCompletion;

        internal CampaignPersistenceCoordinator(ICampaignPersistencePort persistence, TryCaptureCampaignSnapshot captureSnapshot)
        {
            this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            this.captureSnapshot = captureSnapshot ?? throw new ArgumentNullException(nameof(captureSnapshot));
            status = CampaignSaveStatus.Idle();
        }

        internal string StablePayload => stablePayload;
        internal string LastFailureReason { get; private set; }
        internal CampaignSaveStatus Status => status;

        internal bool TryCapture(bool includeActiveHunt, out string payload, out string reason)
        {
            payload = string.Empty;
            LastFailureReason = string.Empty;
            reason = string.Empty;
            try
            {
                if (!captureSnapshot(includeActiveHunt, out CampaignSnapshot snapshot, out reason))
                {
                    LastFailureReason = reason;
                    return false;
                }
                if (!SaveLoadSystem.TryCreatePayload(snapshot, out payload, out reason))
                {
                    LastFailureReason = reason;
                    return false;
                }
            }
            catch (Exception exception)
            {
                reason = $"创建战役存档快照异常：{exception.Message}";
                LastFailureReason = reason;
                return false;
            }
            stablePayload = payload;
            return true;
        }

        internal async UniTask<bool> TrySaveAsync(bool includeActiveHunt, CancellationToken cancellationToken)
        {
            CampaignSaveStatus priorStatus = status;
            long requestRevision = ++saveRevision;
            long requestGeneration = generation;
            SetStatus(new CampaignSaveStatus(CampaignSaveState.Saving, string.Empty, requestRevision, false), requestRevision, requestGeneration);
            if (!TryCapture(includeActiveHunt, out string payload, out string captureReason))
            {
                SetStatus(new CampaignSaveStatus(CampaignSaveState.Failed, captureReason, requestRevision, true), requestRevision, requestGeneration);
                return false;
            }
            bool saved;
            try
            {
                saved = await persistence.TrySavePayloadAsync(payload, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                RestoreAfterCancellation(priorStatus, requestRevision, requestGeneration);
                return false;
            }
            catch (Exception exception)
            {
                SetStatus(new CampaignSaveStatus(CampaignSaveState.Failed, $"异步保存战役存档异常：{exception.Message}", requestRevision, true), requestRevision, requestGeneration);
                return false;
            }
            if (!saved)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    RestoreAfterCancellation(priorStatus, requestRevision, requestGeneration);
                    return false;
                }
                SetStatus(new CampaignSaveStatus(CampaignSaveState.Failed, "异步保存战役存档失败。", requestRevision, true), requestRevision, requestGeneration);
                return false;
            }
            if (requestGeneration != generation)
            {
                return false;
            }
            SetStatus(CampaignSaveStatus.Idle(requestRevision), requestRevision, requestGeneration);
            return true;
        }

        internal UniTask<bool> RetryPendingSaveAsync(bool includeActiveHunt, CancellationToken cancellationToken)
        {
            if (retryCompletion != null) return retryCompletion.Task;
            if (status.State == CampaignSaveState.Idle) return UniTask.FromResult(true);
            if (status.State != CampaignSaveState.Failed || !status.CanRetry) return UniTask.FromResult(false);
            retryCompletion = new UniTaskCompletionSource<bool>();
            long ownerEpoch = retryEpoch;
            RetryPendingSaveCoreAsync(includeActiveHunt, cancellationToken, retryCompletion, ownerEpoch).Forget();
            return retryCompletion.Task;
        }

        internal bool TrySaveImmediate(string payload)
        {
            long requestRevision = ++saveRevision;
            long requestGeneration = generation;
            SetStatus(new CampaignSaveStatus(CampaignSaveState.Saving, string.Empty, requestRevision, false), requestRevision, requestGeneration);
            if (string.IsNullOrWhiteSpace(payload))
            {
                SetStatus(new CampaignSaveStatus(CampaignSaveState.Failed, "战役存档内容为空。", requestRevision, true), requestRevision, requestGeneration);
                return false;
            }
            bool saved;
            try
            {
                saved = persistence.TrySavePayloadImmediate(payload);
            }
            catch (Exception exception)
            {
                SetStatus(new CampaignSaveStatus(CampaignSaveState.Failed, $"立即保存战役存档异常：{exception.Message}", requestRevision, true), requestRevision, requestGeneration);
                return false;
            }
            if (saved)
            {
                stablePayload = payload;
                generation++;
                status = CampaignSaveStatus.Idle(requestRevision);
                LastFailureReason = string.Empty;
            }
            else
            {
                SetStatus(new CampaignSaveStatus(CampaignSaveState.Failed, "立即保存战役存档失败。", requestRevision, true), requestRevision, requestGeneration);
            }
            return saved;
        }

        internal void Adopt(string payload)
        {
            DetachRetryOwner();
            persistence.InvalidatePendingWrites();
            stablePayload = payload ?? string.Empty;
            generation++;
            saveRevision++;
            status = CampaignSaveStatus.Idle(saveRevision);
            LastFailureReason = string.Empty;
        }

        internal void Reset()
        {
            DetachRetryOwner();
            persistence.InvalidatePendingWrites();
            generation++;
            saveRevision++;
            stablePayload = null;
            status = CampaignSaveStatus.Idle(saveRevision);
            LastFailureReason = string.Empty;
        }

        private async UniTaskVoid RetryPendingSaveCoreAsync(bool includeActiveHunt, CancellationToken cancellationToken, UniTaskCompletionSource<bool> completion, long ownerEpoch)
        {
            bool result = false;
            try
            {
                result = await TrySaveAsync(includeActiveHunt, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                result = false;
            }
            catch (Exception)
            {
                result = false;
            }
            finally
            {
                completion.TrySetResult(result);
                if (ownerEpoch == retryEpoch && ReferenceEquals(retryCompletion, completion)) retryCompletion = null;
            }
        }

        private bool SetStatus(CampaignSaveStatus nextStatus, long requestRevision, long requestGeneration)
        {
            if (requestRevision != saveRevision || requestGeneration != generation) return false;
            status = nextStatus;
            LastFailureReason = nextStatus.State == CampaignSaveState.Failed ? nextStatus.Reason : string.Empty;
            return true;
        }

        private void RestoreAfterCancellation(CampaignSaveStatus priorStatus, long requestRevision, long requestGeneration)
        {
            if (priorStatus.State == CampaignSaveState.Failed)
            {
                SetStatus(new CampaignSaveStatus(CampaignSaveState.Failed, priorStatus.Reason, requestRevision, true), requestRevision, requestGeneration);
                return;
            }
            SetStatus(CampaignSaveStatus.Idle(requestRevision), requestRevision, requestGeneration);
        }

        private void DetachRetryOwner()
        {
            retryEpoch++;
            retryCompletion?.TrySetResult(false);
            retryCompletion = null;
        }
    }
}
