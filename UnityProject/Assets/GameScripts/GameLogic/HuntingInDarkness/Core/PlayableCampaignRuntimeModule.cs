using System;
using GameplayBase;
using TEngine;

namespace Core
{
    public interface ICampaignPhaseRuntime : IDisposable
    {
        long GenerationId { get; }
        GamePhase CurrentPhase { get; }
        bool IsStarted { get; }
        void Start(GamePhase initialPhase);
        bool TransitionTo(GamePhase phase);
        void Reset();
    }

    public interface IPlayableCampaignRuntimeModule
    {
        ICampaignPhaseRuntime AcquirePhaseRuntime(Action<GamePhase, GamePhase> onPhaseTransition);
    }

    /// <summary>
    /// ZFramework 管理的战役运行态入口。当前先统一拥有阶段 FSM，后续运行态职责按簇迁入。
    /// </summary>
    public sealed class PlayableCampaignRuntimeModule : Module, IPlayableCampaignRuntimeModule
    {
        private CampaignPhaseRuntime activeRuntime;
        private long nextGenerationId;

        public override void OnInit()
        {
        }

        public ICampaignPhaseRuntime AcquirePhaseRuntime(Action<GamePhase, GamePhase> onPhaseTransition)
        {
            if (activeRuntime != null)
                throw new InvalidOperationException("已有 GameManager 持有战役阶段运行态。");

            activeRuntime = new CampaignPhaseRuntime(++nextGenerationId, GameModule.Fsm, onPhaseTransition, Release);
            return activeRuntime;
        }

        public override void Shutdown()
        {
            CampaignPhaseRuntime runtime = activeRuntime;
            activeRuntime = null;
            runtime?.ReleaseFromModule();
        }

        private void Release(CampaignPhaseRuntime runtime)
        {
            if (ReferenceEquals(activeRuntime, runtime))
                activeRuntime = null;
        }

        private sealed class CampaignPhaseRuntime : ICampaignPhaseRuntime
        {
            private readonly IFsmModule fsmModule;
            private readonly Action<CampaignPhaseRuntime> release;
            private Action<GamePhase, GamePhase> onPhaseTransition;
            private PhaseManager phaseManager;
            private bool disposed;

            public long GenerationId { get; }
            public GamePhase CurrentPhase => phaseManager?.CurrentPhase ?? GamePhase.Settlement;
            public bool IsStarted => phaseManager?.IsStarted == true;

            public CampaignPhaseRuntime(long generationId, IFsmModule fsmModule, Action<GamePhase, GamePhase> onPhaseTransition, Action<CampaignPhaseRuntime> release)
            {
                GenerationId = generationId;
                this.fsmModule = fsmModule ?? throw new ArgumentNullException(nameof(fsmModule));
                this.onPhaseTransition = onPhaseTransition;
                this.release = release ?? throw new ArgumentNullException(nameof(release));
                phaseManager = CreatePhaseManager();
            }

            public void Start(GamePhase initialPhase)
            {
                ThrowIfDisposed();
                phaseManager.Start(initialPhase);
            }

            public bool TransitionTo(GamePhase phase)
            {
                ThrowIfDisposed();
                return phaseManager.TransitionTo(phase);
            }

            public void Reset()
            {
                ThrowIfDisposed();
                phaseManager.Shutdown();
                phaseManager = CreatePhaseManager();
            }

            public void Dispose()
            {
                if (disposed) return;

                disposed = true;
                phaseManager?.Shutdown();
                phaseManager = null;
                onPhaseTransition = null;
                release(this);
            }

            public void ReleaseFromModule()
            {
                if (disposed) return;

                disposed = true;
                phaseManager?.Shutdown();
                phaseManager = null;
                onPhaseTransition = null;
            }

            private PhaseManager CreatePhaseManager()
            {
                var manager = new PhaseManager(fsmModule);
                manager.OnPhaseTransition = HandlePhaseTransition;
                return manager;
            }

            private void HandlePhaseTransition(GamePhase previous, GamePhase next) => onPhaseTransition?.Invoke(previous, next);

            private void ThrowIfDisposed()
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(CampaignPhaseRuntime));
            }
        }
    }
}
