using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;

namespace Core
{
    internal sealed class PlayableSettlementPhaseManager : IDisposable
    {
        private readonly Func<IPlayableCampaignPersistentEffectProjection> persistentEffectProjectionProvider;
        private readonly HashSet<PlayableSettlementRuntime> runtimes = new();
        private PlayableSettlementRuntimeConfiguration configuration;
        private PlayableSettlementRuntime current;
        private long nextGenerationId;
        private bool disposed;

        internal IPlayableSettlementRuntime Current => current;

        internal PlayableSettlementPhaseManager(Func<IPlayableCampaignPersistentEffectProjection> persistentEffectProjectionProvider)
        {
            this.persistentEffectProjectionProvider = persistentEffectProjectionProvider ?? throw new ArgumentNullException(nameof(persistentEffectProjectionProvider));
        }

        internal void Configure(PlayableSettlementRuntimeConfiguration nextConfiguration)
        {
            ThrowIfDisposed();
            if (configuration != null) throw new InvalidOperationException("营地运行态配置已经安装。");
            configuration = nextConfiguration ?? throw new ArgumentNullException(nameof(nextConfiguration));
        }

        internal bool TryPrepareNew(out IPlayableSettlementRuntime candidate, out string reason)
        {
            ThrowIfDisposed();
            candidate = null;
            if (!TryGetConfiguration(out reason)) return false;
            var runtime = new PlayableSettlementRuntime(++nextGenerationId, new SettlementManager(), configuration, false);
            runtimes.Add(runtime);
            candidate = runtime;
            reason = string.Empty;
            return true;
        }

        internal bool TryPrepareRestore(SettlementInstance data, out IPlayableSettlementRuntime candidate, out string reason)
        {
            ThrowIfDisposed();
            candidate = null;
            if (!TryGetConfiguration(out reason)) return false;
            if (!SettlementManager.TryPrepareCandidate(data, out SettlementManager manager, out reason)) return false;
            var runtime = new PlayableSettlementRuntime(++nextGenerationId, manager, configuration, true);
            runtimes.Add(runtime);
            candidate = runtime;
            reason = string.Empty;
            return true;
        }

        internal bool TrySwap(IPlayableSettlementRuntime expectedCurrent, IPlayableSettlementRuntime replacement, out string reason)
        {
            ThrowIfDisposed();
            if (!ReferenceEquals(current, expectedCurrent))
            {
                reason = "权威营地运行世代已变化，拒绝提交过期候选。";
                return false;
            }
            PlayableSettlementRuntime next = replacement as PlayableSettlementRuntime;
            if (replacement != null && (next == null || !runtimes.Contains(next) || !next.IsDetached))
            {
                reason = "替换目标不是当前战役持有的可发布营地候选。";
                return false;
            }

            IPlayableCampaignPersistentEffectProjection projection = persistentEffectProjectionProvider();
            if (projection != null && !projection.TrySynchronize(next?.Data, out reason)) return false;
            if (next != null && !next.TryPreparePublication(out reason))
            {
                projection?.TrySynchronize(current?.Data, out _);
                return false;
            }

            PlayableSettlementRuntime previous = current;
            try
            {
                previous?.Detach();
                next?.Publish();
                current = next;
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                projection?.TrySynchronize(previous?.Data, out _);
                reason = $"提交营地运行世代失败：{exception.Message}";
                return false;
            }
        }

        internal void Release(IPlayableSettlementRuntime runtime)
        {
            ThrowIfDisposed();
            if (runtime is not PlayableSettlementRuntime owned || !runtimes.Contains(owned))
                throw new InvalidOperationException("营地运行世代不属于当前战役。");
            if (owned.IsCurrent)
                throw new InvalidOperationException("不能释放当前权威营地运行世代。");
            owned.Dispose();
            runtimes.Remove(owned);
        }

        internal bool Owns(PlayableSettlementRuntime runtime) => runtime != null && runtimes.Contains(runtime);

        internal void Reset()
        {
            ThrowIfDisposed();
            ResetRuntimes();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ResetRuntimes();
            configuration = null;
        }

        private bool TryGetConfiguration(out string reason)
        {
            if (configuration != null)
            {
                reason = string.Empty;
                return true;
            }
            reason = "营地运行态组合配置尚未安装。";
            return false;
        }

        private void ResetRuntimes()
        {
            foreach (PlayableSettlementRuntime runtime in runtimes)
                runtime.Dispose();
            runtimes.Clear();
            current = null;
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(PlayableSettlementPhaseManager));
        }
    }
}
