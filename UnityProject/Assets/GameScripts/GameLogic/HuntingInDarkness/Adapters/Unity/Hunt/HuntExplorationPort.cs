using System;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunt;
using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    /// <summary>由世界空间 Hunt View 提交的不可变交互快照。</summary>
    public readonly struct HuntExplorationSnapshot
    {
        public HuntExplorationSnapshot(Guid sessionId, Vector2Int coordinate, int resourcePointIndex = -1, string resourceContentId = null, int resourceDrawCount = 0)
        {
            SessionId = sessionId;
            Coordinate = coordinate;
            ResourcePointIndex = resourcePointIndex;
            ResourceContentId = resourceContentId ?? string.Empty;
            ResourceDrawCount = Math.Max(0, resourceDrawCount);
        }

        public Guid SessionId { get; }
        public Vector2Int Coordinate { get; }
        public int ResourcePointIndex { get; }
        public string ResourceContentId { get; }
        public int ResourceDrawCount { get; }
        public bool IsResourcePointSelection => ResourcePointIndex >= 0;
    }

    /// <summary>狩猎 View 到当前 ActionQueue 会话的窄交互端口。</summary>
    public interface IHuntExplorationPort
    {
        Guid SessionId { get; }
        bool TryCreateSnapshot(Vector2Int coordinate, int resourcePointIndex, out HuntExplorationSnapshot snapshot);
        UniTask<HuntTileCommandResult> SubmitTileAsync(HuntExplorationSnapshot snapshot);
        UniTask<bool> SubmitResourcePointAsync(HuntExplorationSnapshot snapshot);
        UniTask<PlayableHarvestTransaction> PrepareHarvestAsync(HuntExplorationSnapshot target);
        UniTask<PlayableHarvestStepResult> AdvanceHarvestAsync(Guid sessionId, PlayableHarvestTransaction transaction);
    }

    /// <summary>一次狩猎会话的能力租约；旧世界物件无法借组合根为新会话重新签名。</summary>
    public sealed class HuntExplorationSessionPort : IHuntExplorationPort
    {
        private readonly HuntExplorationRuntime runtime;

        internal HuntExplorationSessionPort(HuntExplorationRuntime runtime, Guid sessionId)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            SessionId = sessionId;
        }

        public Guid SessionId { get; }
        public bool TryCreateSnapshot(Vector2Int coordinate, int resourcePointIndex, out HuntExplorationSnapshot snapshot) => runtime.TryCreateSnapshot(SessionId, coordinate, resourcePointIndex, out snapshot);
        public UniTask<HuntTileCommandResult> SubmitTileAsync(HuntExplorationSnapshot snapshot) => runtime.SubmitTileAsync(SessionId, snapshot);
        public UniTask<bool> SubmitResourcePointAsync(HuntExplorationSnapshot snapshot) => runtime.SubmitResourcePointAsync(SessionId, snapshot);
        public UniTask<PlayableHarvestTransaction> PrepareHarvestAsync(HuntExplorationSnapshot target) => runtime.PrepareHarvestAsync(SessionId, target);
        public UniTask<PlayableHarvestStepResult> AdvanceHarvestAsync(Guid sessionId, PlayableHarvestTransaction transaction) => runtime.AdvanceHarvestAsync(SessionId, sessionId, transaction);
    }

    /// <summary>持有单次狩猎的权威交互租约与快照校验；组合根只负责装配和释放。</summary>
    public sealed class HuntExplorationRuntime
    {
        private readonly HuntManager manager;
        private readonly PlayableHuntActionSession session;

        public HuntExplorationRuntime(HuntManager manager, PlayableHuntActionSession session)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            Port = new HuntExplorationSessionPort(this, session.SessionId);
        }

        public IHuntExplorationPort Port { get; }
        public bool IsActive => session.IsActive;

        internal bool TryCreateSnapshot(Guid leaseSessionId, Vector2Int coordinate, int resourcePointIndex, out HuntExplorationSnapshot snapshot)
        {
            snapshot = default;
            if (!IsCurrentLease(leaseSessionId) || resourcePointIndex < -1 || !manager.Map.TryGetValue(coordinate, out HexTileInstance tile)) return false;
            if (resourcePointIndex >= 0 && (tile.ResourcePoints == null || resourcePointIndex >= tile.ResourcePoints.Count)) return false;
            ResourcePointInstance point = resourcePointIndex >= 0 ? tile.ResourcePoints[resourcePointIndex] : null;
            if (resourcePointIndex >= 0 && (point?.Resource == null || string.IsNullOrWhiteSpace(point.Resource.ContentId))) return false;
            snapshot = new HuntExplorationSnapshot(leaseSessionId, coordinate, resourcePointIndex, point?.Resource.ContentId, point?.DrawCount ?? 0);
            return true;
        }

        internal UniTask<HuntTileCommandResult> SubmitTileAsync(Guid leaseSessionId, HuntExplorationSnapshot snapshot)
        {
            if (!TryValidateSnapshot(leaseSessionId, snapshot, false, out string reason)) return UniTask.FromResult(HuntTileCommandResult.Failed(reason));
            return session.InteractTileAsync(snapshot.Coordinate);
        }

        internal UniTask<bool> SubmitResourcePointAsync(Guid leaseSessionId, HuntExplorationSnapshot snapshot)
        {
            if (!TryValidateSnapshot(leaseSessionId, snapshot, true, out _)) return UniTask.FromResult(false);
            return session.SelectResourcePointAsync(snapshot.Coordinate, snapshot.ResourcePointIndex);
        }

        internal UniTask<PlayableHarvestTransaction> PrepareHarvestAsync(Guid leaseSessionId, HuntExplorationSnapshot target)
        {
            if (!TryValidateSnapshot(leaseSessionId, target, true, out _)) return UniTask.FromResult<PlayableHarvestTransaction>(null);
            return session.PrepareHarvestAsync(target.Coordinate, target.ResourcePointIndex);
        }

        internal UniTask<PlayableHarvestStepResult> AdvanceHarvestAsync(Guid leaseSessionId, Guid targetSessionId, PlayableHarvestTransaction transaction)
        {
            if (!IsCurrentLease(leaseSessionId) || targetSessionId != leaseSessionId || transaction == null)
                return UniTask.FromResult(PlayableHarvestStepResult.Failed("采集事务属于旧的狩猎会话。"));
            return session.AdvanceHarvestAsync(transaction);
        }

        private bool IsCurrentLease(Guid leaseSessionId) => session.IsActive && session.SessionId == leaseSessionId && Port.SessionId == leaseSessionId;

        private bool TryValidateSnapshot(Guid leaseSessionId, HuntExplorationSnapshot snapshot, bool requireResourcePoint, out string reason)
        {
            reason = string.Empty;
            if (!IsCurrentLease(leaseSessionId))
            {
                reason = "当前没有可用的狩猎交互会话。";
                return false;
            }
            if (snapshot.SessionId != leaseSessionId)
            {
                reason = "狩猎交互请求属于旧会话。";
                return false;
            }
            if (snapshot.ResourcePointIndex < -1 || requireResourcePoint != snapshot.IsResourcePointSelection)
            {
                reason = "狩猎交互请求类型无效。";
                return false;
            }
            if (!manager.Map.TryGetValue(snapshot.Coordinate, out HexTileInstance tile))
            {
                reason = "狩猎地块已不存在。";
                return false;
            }
            if (!snapshot.IsResourcePointSelection) return true;
            if (tile.ResourcePoints == null || snapshot.ResourcePointIndex >= tile.ResourcePoints.Count)
            {
                reason = "狩猎资源点索引已失效。";
                return false;
            }
            ResourcePointInstance point = tile.ResourcePoints[snapshot.ResourcePointIndex];
            if (point?.Resource != null && string.Equals(point.Resource.ContentId, snapshot.ResourceContentId, StringComparison.Ordinal) && point.DrawCount == snapshot.ResourceDrawCount) return true;
            reason = "狩猎资源点内容已经改变。";
            return false;
        }
    }

    public readonly struct HuntResourcePointPresentationRequest
    {
        public HuntResourcePointPresentationRequest(Vector2Int coordinate, int pointIndex, string resourceName, int drawCount)
        {
            Coordinate = coordinate;
            PointIndex = pointIndex;
            ResourceName = resourceName ?? string.Empty;
            DrawCount = Math.Max(0, drawCount);
        }

        public Vector2Int Coordinate { get; }
        public int PointIndex { get; }
        public string ResourceName { get; }
        public int DrawCount { get; }
    }
}
