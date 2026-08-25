using System;
using System.Threading;
using CardGame.ActionQueue;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.ActionFlow;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using TEngine;

namespace Core
{
    public interface IPlayableCampaignRuntime : IDisposable
    {
        long GenerationId { get; }
        GamePhase CurrentPhase { get; }
        bool IsStarted { get; }
        bool IsActionSessionActive { get; }
        bool IsActionSessionRunning { get; }
        SettlementEventRestoreProjection SettlementEventRestore { get; }
        IActionEnvironmentInstallerRegistry ActionEnvironmentInstallers { get; }
        ReactorRegistry ActionReactors { get; }
        SettlementEventRestoreProjection CreateSettlementEventRestoreCandidate(SettlementInstance settlement, Func<string, EventData> resolveEvent);
        void PublishSettlementEventRestore(SettlementEventRestoreProjection candidate);
        void ClearSettlementEventRestore();
        void EnsureGameplayRuntime(IActionEnvironmentInstaller gameplayInstaller);
        void Start(GamePhase initialPhase);
        bool TransitionTo(GamePhase phase);
        UniTask<CampaignPhaseTransitionResult> TransitionAsync(GamePhase phase, CancellationToken cancellationToken = default);
        UniTask<CampaignPhaseTransitionResult> TransitionAsync(CampaignPhaseTransitionRequest request, CancellationToken cancellationToken = default);
        UniTask<CampaignEncounterStartResult> BeginEncounterAsync(CampaignEncounterRequest request, CancellationToken cancellationToken = default);
        void Reset();
    }

    public interface IPlayableCampaignRuntimeModule
    {
        IPlayableCampaignRuntime AcquireRuntime(ICampaignPhaseTransitionHost host, Action<GamePhase, GamePhase> onPhaseTransition);
    }

    /// <summary>
    /// ZFramework 管理的战役运行态入口。当前先统一拥有阶段 FSM，后续运行态职责按簇迁入。
    /// </summary>
    public sealed class PlayableCampaignRuntimeModule : Module, IPlayableCampaignRuntimeModule
    {
        private CampaignRuntime activeRuntime;
        private long nextGenerationId;

        public override void OnInit()
        {
        }

        public IPlayableCampaignRuntime AcquireRuntime(ICampaignPhaseTransitionHost host, Action<GamePhase, GamePhase> onPhaseTransition)
        {
            if (activeRuntime != null)
                throw new InvalidOperationException("已有 GameManager 持有战役运行态。");

            activeRuntime = new CampaignRuntime(++nextGenerationId, GameModule.Fsm, host, onPhaseTransition, Release);
            return activeRuntime;
        }

        public override void Shutdown()
        {
            CampaignRuntime runtime = activeRuntime;
            activeRuntime = null;
            runtime?.ReleaseFromModule();
        }

        private void Release(CampaignRuntime runtime)
        {
            if (ReferenceEquals(activeRuntime, runtime))
                activeRuntime = null;
        }

        private sealed class CampaignRuntime : IPlayableCampaignRuntime
        {
            private readonly IFsmModule fsmModule;
            private ICampaignPhaseTransitionHost host;
            private Action<CampaignRuntime> release;
            private readonly ActionEnvironmentInstallerRegistry actionEnvironmentInstallers = new();
            private Action<GamePhase, GamePhase> onPhaseTransition;
            private PhaseManager phaseManager;
            private PlayableCampaignActionSession actionSession;
            private IDisposable gameplayInstallation;
            private SettlementEventRestoreProjection settlementEventRestore;
            private bool disposed;

            public long GenerationId { get; }
            public GamePhase CurrentPhase => phaseManager?.CurrentPhase ?? GamePhase.Settlement;
            public bool IsStarted => phaseManager?.IsStarted == true;
            public bool IsActionSessionActive => actionSession?.IsActive == true;
            public bool IsActionSessionRunning => actionSession?.IsRunning == true;
            public SettlementEventRestoreProjection SettlementEventRestore => settlementEventRestore;
            public IActionEnvironmentInstallerRegistry ActionEnvironmentInstallers => actionEnvironmentInstallers;
            public ReactorRegistry ActionReactors => actionSession?.Reactors;

            public CampaignRuntime(long generationId, IFsmModule fsmModule, ICampaignPhaseTransitionHost host, Action<GamePhase, GamePhase> onPhaseTransition, Action<CampaignRuntime> release)
            {
                GenerationId = generationId;
                this.fsmModule = fsmModule ?? throw new ArgumentNullException(nameof(fsmModule));
                this.host = host ?? throw new ArgumentNullException(nameof(host));
                this.onPhaseTransition = onPhaseTransition;
                this.release = release ?? throw new ArgumentNullException(nameof(release));
                phaseManager = CreatePhaseManager();
            }

            public void EnsureGameplayRuntime(IActionEnvironmentInstaller gameplayInstaller)
            {
                ThrowIfDisposed();
                if (IsActionSessionActive) return;
                if (gameplayInstaller == null) throw new ArgumentNullException(nameof(gameplayInstaller));

                try
                {
                    gameplayInstallation ??= actionEnvironmentInstallers.Register(gameplayInstaller);
                    actionSession = new PlayableCampaignActionSession(host, actionEnvironmentInstallers);
                }
                catch
                {
                    actionSession?.Dispose();
                    actionSession = null;
                    gameplayInstallation?.Dispose();
                    gameplayInstallation = null;
                    throw;
                }
            }

            public SettlementEventRestoreProjection CreateSettlementEventRestoreCandidate(SettlementInstance settlement, Func<string, EventData> resolveEvent)
            {
                ThrowIfDisposed();
                return new SettlementEventRestoreProjection(settlement, resolveEvent);
            }

            public void PublishSettlementEventRestore(SettlementEventRestoreProjection candidate)
            {
                ThrowIfDisposed();
                settlementEventRestore = candidate ?? throw new ArgumentNullException(nameof(candidate));
            }

            public void ClearSettlementEventRestore()
            {
                ThrowIfDisposed();
                settlementEventRestore = null;
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

            public UniTask<CampaignPhaseTransitionResult> TransitionAsync(GamePhase phase, CancellationToken cancellationToken = default)
            {
                ThrowIfDisposed();
                if (IsActionSessionActive) return actionSession.TransitionAsync(phase, cancellationToken);
                return UniTask.FromResult(CampaignPhaseTransitionResult.Failed(CurrentPhase, "战役玩法运行态尚未启动"));
            }

            public UniTask<CampaignPhaseTransitionResult> TransitionAsync(CampaignPhaseTransitionRequest request, CancellationToken cancellationToken = default)
            {
                ThrowIfDisposed();
                if (IsActionSessionActive) return actionSession.TransitionAsync(request, cancellationToken);
                return UniTask.FromResult(CampaignPhaseTransitionResult.Failed(CurrentPhase, "战役玩法运行态尚未启动"));
            }

            public UniTask<CampaignEncounterStartResult> BeginEncounterAsync(CampaignEncounterRequest request, CancellationToken cancellationToken = default)
            {
                ThrowIfDisposed();
                if (IsActionSessionActive) return actionSession.BeginEncounterAsync(request, cancellationToken);
                return UniTask.FromResult(CampaignEncounterStartResult.Failed(request.EncounterId, "战役玩法运行态尚未启动"));
            }

            public void Reset()
            {
                ThrowIfDisposed();
                ResetGameplayRuntime();
                settlementEventRestore = null;
                phaseManager.Shutdown();
                phaseManager = CreatePhaseManager();
            }

            public void Dispose()
            {
                if (disposed) return;

                disposed = true;
                ResetGameplayRuntime();
                settlementEventRestore = null;
                actionEnvironmentInstallers.Dispose();
                phaseManager?.Shutdown();
                phaseManager = null;
                onPhaseTransition = null;
                host = null;
                Action<CampaignRuntime> releaseRuntime = release;
                release = null;
                releaseRuntime(this);
            }

            public void ReleaseFromModule()
            {
                if (disposed) return;

                disposed = true;
                ResetGameplayRuntime();
                settlementEventRestore = null;
                actionEnvironmentInstallers.Dispose();
                phaseManager?.Shutdown();
                phaseManager = null;
                onPhaseTransition = null;
                host = null;
                release = null;
            }

            private PhaseManager CreatePhaseManager()
            {
                var manager = new PhaseManager(fsmModule);
                manager.OnPhaseTransition = HandlePhaseTransition;
                return manager;
            }

            private void HandlePhaseTransition(GamePhase previous, GamePhase next) => onPhaseTransition?.Invoke(previous, next);

            private void ResetGameplayRuntime()
            {
                PlayableCampaignActionSession session = actionSession;
                actionSession = null;
                session?.Dispose();
                gameplayInstallation?.Dispose();
                gameplayInstallation = null;
            }

            private void ThrowIfDisposed()
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(CampaignRuntime));
            }
        }
    }
}
