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
        private string stablePayload;

        internal CampaignPersistenceCoordinator(ICampaignPersistencePort persistence, TryCaptureCampaignSnapshot captureSnapshot)
        {
            this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            this.captureSnapshot = captureSnapshot ?? throw new ArgumentNullException(nameof(captureSnapshot));
        }

        internal string StablePayload => stablePayload;
        internal string LastFailureReason { get; private set; }

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
            if (!TryCapture(includeActiveHunt, out string payload, out _)) return false;
            long capturedGeneration = generation;
            bool saved;
            try
            {
                saved = await persistence.TrySavePayloadAsync(payload, cancellationToken);
            }
            catch (Exception exception)
            {
                LastFailureReason = $"异步保存战役存档异常：{exception.Message}";
                return false;
            }
            if (!saved)
            {
                LastFailureReason = "异步保存战役存档失败。";
                return false;
            }
            if (capturedGeneration != generation)
            {
                LastFailureReason = "战役存档世代已变化，忽略过期保存结果。";
                return false;
            }
            return true;
        }

        internal bool TrySaveImmediate(string payload)
        {
            LastFailureReason = string.Empty;
            if (string.IsNullOrWhiteSpace(payload))
            {
                LastFailureReason = "战役存档内容为空。";
                return false;
            }
            bool saved;
            try
            {
                saved = persistence.TrySavePayloadImmediate(payload);
            }
            catch (Exception exception)
            {
                LastFailureReason = $"立即保存战役存档异常：{exception.Message}";
                return false;
            }
            if (saved)
            {
                stablePayload = payload;
                generation++;
            }
            else
                LastFailureReason = "立即保存战役存档失败。";
            return saved;
        }

        internal void Adopt(string payload)
        {
            persistence.InvalidatePendingWrites();
            stablePayload = payload ?? string.Empty;
            generation++;
        }

        internal void Reset()
        {
            persistence.InvalidatePendingWrites();
            generation++;
            stablePayload = null;
        }
    }
}
