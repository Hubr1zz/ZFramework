using System;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;

namespace Core
{
    public sealed class PlayableHuntRuntimeConfiguration
    {
        public Func<SettlementManager, HuntManager> CreateManager { get; }
        public Func<HuntManager, PlayableHuntEventOccurrenceStore, PlayableHuntActionSession> CreateActionSession { get; }

        public PlayableHuntRuntimeConfiguration(Func<SettlementManager, HuntManager> createManager, Func<HuntManager, PlayableHuntEventOccurrenceStore, PlayableHuntActionSession> createActionSession)
        {
            CreateManager = createManager ?? throw new ArgumentNullException(nameof(createManager));
            CreateActionSession = createActionSession ?? throw new ArgumentNullException(nameof(createActionSession));
        }
    }

    public interface IPlayableHuntRuntime
    {
        long GenerationId { get; }
        string ExpeditionId { get; }
        HuntManager Manager { get; }
        PlayableHuntActionSession ActionSession { get; }
        HuntExplorationRuntime Exploration { get; }
        IHuntExplorationPort ExplorationPort { get; }
        bool IsActionSessionActive { get; }
        bool IsActionSessionRunning { get; }
        bool TryActivateActionSession(PlayableHuntEventOccurrenceStore restoredOccurrences, out string reason);
        void DeactivateActionSession();
    }

    internal sealed class PlayableHuntRuntime : IPlayableHuntRuntime, IDisposable
    {
        private enum RuntimeState
        {
            Detached,
            Current,
            Disposed
        }

        private readonly PlayableHuntRuntimeConfiguration configuration;
        private PlayableHuntActionSession actionSession;
        private HuntExplorationRuntime exploration;
        private RuntimeState state;

        public long GenerationId { get; }
        public string ExpeditionId { get; }
        public HuntManager Manager { get; }
        public PlayableHuntActionSession ActionSession => actionSession;
        public HuntExplorationRuntime Exploration => exploration;
        public IHuntExplorationPort ExplorationPort => exploration?.Port;
        public bool IsActionSessionActive => actionSession?.IsActive == true;
        public bool IsActionSessionRunning => actionSession?.IsRunning == true;
        internal bool IsCurrent => state == RuntimeState.Current;
        internal bool IsDetached => state == RuntimeState.Detached;

        internal PlayableHuntRuntime(long generationId, string expeditionId, SettlementManager settlementManager, PlayableHuntRuntimeConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(expeditionId))
                throw new ArgumentException("远征 ID 不能为空。", nameof(expeditionId));
            GenerationId = generationId;
            ExpeditionId = expeditionId.Trim();
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Manager = configuration.CreateManager(settlementManager) ?? throw new InvalidOperationException("狩猎 Manager 工厂返回空结果。");
        }

        public bool TryActivateActionSession(PlayableHuntEventOccurrenceStore restoredOccurrences, out string reason)
        {
            ThrowIfDisposed();
            if (!IsCurrent)
            {
                reason = "狩猎运行世代不是当前权威，无法启动 ActionSession。";
                return false;
            }
            if (IsActionSessionActive)
            {
                reason = string.Empty;
                return true;
            }

            PlayableHuntActionSession staleSession = actionSession;
            actionSession = null;
            exploration = null;
            staleSession?.Dispose();

            PlayableHuntActionSession candidate = null;
            try
            {
                candidate = configuration.CreateActionSession(Manager, restoredOccurrences);
                if (candidate == null)
                {
                    reason = "狩猎 ActionSession 工厂返回空结果。";
                    return false;
                }
                var candidateExploration = new HuntExplorationRuntime(Manager, candidate);
                actionSession = candidate;
                exploration = candidateExploration;
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                candidate?.Dispose();
                reason = $"狩猎 ActionSession 初始化异常：{exception.Message}";
                return false;
            }
        }

        public void DeactivateActionSession()
        {
            if (state == RuntimeState.Disposed)
                return;
            PlayableHuntActionSession session = actionSession;
            actionSession = null;
            exploration = null;
            session?.Dispose();
        }

        internal void Publish()
        {
            ThrowIfDisposed();
            if (!IsDetached)
                throw new InvalidOperationException("狩猎运行世代不是可发布候选。");
            state = RuntimeState.Current;
        }

        internal void Detach()
        {
            ThrowIfDisposed();
            state = RuntimeState.Detached;
        }

        public void Dispose()
        {
            if (state == RuntimeState.Disposed)
                return;
            DeactivateActionSession();
            Manager.OnBossEncounterTriggered = null;
            Manager.OnHuntCompleted = null;
            Manager.EventInput = null;
            state = RuntimeState.Disposed;
        }

        private void ThrowIfDisposed()
        {
            if (state == RuntimeState.Disposed)
                throw new ObjectDisposedException(nameof(PlayableHuntRuntime));
        }
    }
}
