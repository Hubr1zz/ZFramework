using System;
using System.Collections.Generic;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;

namespace Core
{
    internal sealed class PlayableHuntPhaseManager : IDisposable
    {
        private readonly PlayableSettlementPhaseManager settlementManager;
        private readonly PlayableHuntPhaseCoordinator coordinator = new();
        private readonly HashSet<PlayableHuntRuntime> runtimes = new();
        private PlayableHuntRuntimeConfiguration configuration;
        private PlayableHuntRuntime current;
        private long nextGenerationId;
        private bool disposed;

        internal IPlayableHuntRuntime Current => current;
        internal PlayableHuntPhaseCoordinator Coordinator => coordinator;

        internal PlayableHuntPhaseManager(PlayableSettlementPhaseManager settlementManager)
        {
            this.settlementManager = settlementManager ?? throw new ArgumentNullException(nameof(settlementManager));
        }

        internal void Configure(PlayableHuntRuntimeConfiguration nextConfiguration)
        {
            ThrowIfDisposed();
            if (configuration != null) throw new InvalidOperationException("狩猎运行态配置已经安装。");
            configuration = nextConfiguration ?? throw new ArgumentNullException(nameof(nextConfiguration));
        }

        internal bool TryPrepareNew(IPlayableSettlementRuntime settlement, out IPlayableHuntRuntime candidate, out string reason)
        {
            return TryPrepare(settlement, Guid.NewGuid().ToString("N"), out candidate, out reason);
        }

        internal bool TryPrepareRestore(IPlayableSettlementRuntime settlement, string expeditionId, out IPlayableHuntRuntime candidate, out string reason)
        {
            return TryPrepare(settlement, expeditionId, out candidate, out reason);
        }

        internal bool TrySwap(IPlayableHuntRuntime expectedCurrent, IPlayableHuntRuntime replacement, out string reason)
        {
            ThrowIfDisposed();
            if (!ReferenceEquals(current, expectedCurrent))
            {
                reason = "权威狩猎运行世代已变化，拒绝提交过期候选。";
                return false;
            }
            PlayableHuntRuntime next = replacement as PlayableHuntRuntime;
            if (replacement != null && (next == null || !runtimes.Contains(next) || !next.IsDetached))
            {
                reason = "替换目标不是当前战役持有的可发布狩猎候选。";
                return false;
            }
            PlayableHuntRuntime previous = current;
            previous?.Detach();
            next?.Publish();
            current = next;
            reason = string.Empty;
            return true;
        }

        internal void Release(IPlayableHuntRuntime runtime)
        {
            ThrowIfDisposed();
            if (runtime is not PlayableHuntRuntime owned || !runtimes.Contains(owned))
                throw new InvalidOperationException("狩猎运行世代不属于当前战役。");
            if (owned.IsCurrent)
                throw new InvalidOperationException("不能释放当前权威狩猎运行世代。");
            owned.Dispose();
            runtimes.Remove(owned);
        }

        internal void Reset()
        {
            ThrowIfDisposed();
            coordinator.Cleanup();
            ResetRuntimes();
        }

        public void Dispose()
        {
            if (disposed) return;
            coordinator.Dispose();
            disposed = true;
            ResetRuntimes();
            configuration = null;
        }

        private bool TryPrepare(IPlayableSettlementRuntime settlement, string expeditionId, out IPlayableHuntRuntime candidate, out string reason)
        {
            ThrowIfDisposed();
            candidate = null;
            if (configuration == null)
            {
                reason = "狩猎运行态组合配置尚未安装。";
                return false;
            }
            if (settlement is not PlayableSettlementRuntime ownedSettlement || !settlementManager.Owns(ownedSettlement))
            {
                reason = "狩猎运行态引用的营地世代不属于当前战役。";
                return false;
            }
            try
            {
                var runtime = new PlayableHuntRuntime(++nextGenerationId, expeditionId, ownedSettlement.Manager, configuration);
                runtimes.Add(runtime);
                candidate = runtime;
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = $"准备狩猎运行世代失败：{exception.Message}";
                return false;
            }
        }

        private void ResetRuntimes()
        {
            foreach (PlayableHuntRuntime runtime in runtimes)
                runtime.Dispose();
            runtimes.Clear();
            current = null;
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(PlayableHuntPhaseManager));
        }
    }
}
