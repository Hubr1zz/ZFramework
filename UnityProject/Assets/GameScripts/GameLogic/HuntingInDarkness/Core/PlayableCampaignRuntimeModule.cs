using System;
using System.Collections.Generic;
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
        IPlayableSettlementRuntime Settlement { get; }
        IPlayableHuntRuntime Hunt { get; }
        IPlayableCampaignPersistentEffectProjection PersistentEffectProjection { get; }
        IActionEnvironmentInstallerRegistry ActionEnvironmentInstallers { get; }
        ReactorRegistry ActionReactors { get; }
        void ConfigurePersistentEffectProjection(Func<IActionEnvironmentInstallerRegistry, IPlayableCampaignPersistentEffectProjection> factory);
        bool TryPrepareNewSettlement(out IPlayableSettlementRuntime candidate, out string reason);
        bool TryPrepareSettlementRestore(SettlementInstance data, out IPlayableSettlementRuntime candidate, out string reason);
        bool TrySwapSettlement(IPlayableSettlementRuntime expectedCurrent, IPlayableSettlementRuntime replacement, out string reason);
        void ReleaseSettlement(IPlayableSettlementRuntime runtime);
        bool TryPrepareNewHunt(IPlayableSettlementRuntime settlement, out IPlayableHuntRuntime candidate, out string reason);
        bool TryPrepareHuntRestore(IPlayableSettlementRuntime settlement, string expeditionId, out IPlayableHuntRuntime candidate, out string reason);
        bool TrySwapHunt(IPlayableHuntRuntime expectedCurrent, IPlayableHuntRuntime replacement, out string reason);
        void ReleaseHunt(IPlayableHuntRuntime runtime);
        void EnsureGameplayRuntime(IActionEnvironmentInstaller gameplayInstaller);
        void Start(GamePhase initialPhase);
        bool TransitionTo(GamePhase phase);
        UniTask<CampaignPhaseTransitionResult> TransitionAsync(GamePhase phase, CancellationToken cancellationToken = default);
        UniTask<CampaignPhaseTransitionResult> TransitionAsync(CampaignPhaseTransitionRequest request, CancellationToken cancellationToken = default);
        UniTask<CampaignEncounterStartResult> BeginEncounterAsync(CampaignEncounterRequest request, CancellationToken cancellationToken = default);
        UniTask<CampaignRestartResult> RestartAsync(CancellationToken cancellationToken = default);
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

        private sealed class CampaignRuntime : IPlayableCampaignRuntime, IPlayableCampaignPhasePortAccess
        {
            private readonly IFsmModule fsmModule;
            private ICampaignPhaseTransitionHost host;
            private Action<CampaignRuntime> release;
            private readonly ActionEnvironmentInstallerRegistry actionEnvironmentInstallers = new();
            private readonly PlayableSettlementPhaseManager settlementPhaseManager;
            private readonly PlayableHuntPhaseManager huntPhaseManager;
            private readonly PlayableShowdownPhaseManager showdownPhaseManager;
            private Action<GamePhase, GamePhase> onPhaseTransition;
            private PhaseManager phaseManager;
            private PlayableCampaignActionSession actionSession;
            private IDisposable gameplayInstallation;
            private IPlayableCampaignPersistentEffectProjection persistentEffectProjection;
            private bool disposed;

            public long GenerationId { get; }
            public GamePhase CurrentPhase => phaseManager?.CurrentPhase ?? GamePhase.Settlement;
            public bool IsStarted => phaseManager?.IsStarted == true;
            public bool IsActionSessionActive => actionSession?.IsActive == true;
            public bool IsActionSessionRunning => actionSession?.IsRunning == true;
            public IPlayableSettlementRuntime Settlement => settlementPhaseManager.Current;
            public IPlayableHuntRuntime Hunt => huntPhaseManager.Current;
            public IPlayableCampaignPersistentEffectProjection PersistentEffectProjection => persistentEffectProjection;
            public IActionEnvironmentInstallerRegistry ActionEnvironmentInstallers => actionEnvironmentInstallers;
            public ReactorRegistry ActionReactors => actionSession?.Reactors;
            IPlayableSettlementPhasePort IPlayableCampaignPhasePortAccess.SettlementPhase => settlementPhaseManager;
            IPlayableHuntPhasePort IPlayableCampaignPhasePortAccess.HuntPhase => huntPhaseManager;
            IPlayableShowdownPhasePort IPlayableCampaignPhasePortAccess.ShowdownPhase => showdownPhaseManager;

            public CampaignRuntime(long generationId, IFsmModule fsmModule, ICampaignPhaseTransitionHost host, Action<GamePhase, GamePhase> onPhaseTransition, Action<CampaignRuntime> release)
            {
                GenerationId = generationId;
                this.fsmModule = fsmModule ?? throw new ArgumentNullException(nameof(fsmModule));
                this.host = host ?? throw new ArgumentNullException(nameof(host));
                this.onPhaseTransition = onPhaseTransition;
                this.release = release ?? throw new ArgumentNullException(nameof(release));
                settlementPhaseManager = new PlayableSettlementPhaseManager(() => persistentEffectProjection);
                huntPhaseManager = new PlayableHuntPhaseManager(settlementPhaseManager);
                showdownPhaseManager = new PlayableShowdownPhaseManager();
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

            public void ConfigurePersistentEffectProjection(Func<IActionEnvironmentInstallerRegistry, IPlayableCampaignPersistentEffectProjection> factory)
            {
                ThrowIfDisposed();
                if (persistentEffectProjection != null)
                    throw new InvalidOperationException("战役持久效果投影已经安装。");
                if (factory == null)
                    throw new ArgumentNullException(nameof(factory));
                persistentEffectProjection = factory(actionEnvironmentInstallers) ?? throw new InvalidOperationException("战役持久效果投影工厂返回空结果。");
                if (settlementPhaseManager.Current != null && !persistentEffectProjection.TrySynchronize(settlementPhaseManager.Current.Data, out string reason))
                {
                    persistentEffectProjection.Dispose();
                    persistentEffectProjection = null;
                    throw new InvalidOperationException(reason);
                }
            }

            public bool TryPrepareNewSettlement(out IPlayableSettlementRuntime candidate, out string reason)
            {
                return settlementPhaseManager.TryPrepareNew(out candidate, out reason);
            }

            public bool TryPrepareSettlementRestore(SettlementInstance data, out IPlayableSettlementRuntime candidate, out string reason)
            {
                return settlementPhaseManager.TryPrepareRestore(data, out candidate, out reason);
            }

            public bool TrySwapSettlement(IPlayableSettlementRuntime expectedCurrent, IPlayableSettlementRuntime replacement, out string reason)
            {
                return settlementPhaseManager.TrySwap(expectedCurrent, replacement, out reason);
            }

            public void ReleaseSettlement(IPlayableSettlementRuntime runtime)
            {
                settlementPhaseManager.Release(runtime);
            }

            public bool TryPrepareNewHunt(IPlayableSettlementRuntime settlement, out IPlayableHuntRuntime candidate, out string reason)
                => huntPhaseManager.TryPrepareNew(settlement, out candidate, out reason);

            public bool TryPrepareHuntRestore(IPlayableSettlementRuntime settlement, string expeditionId, out IPlayableHuntRuntime candidate, out string reason)
                => huntPhaseManager.TryPrepareRestore(settlement, expeditionId, out candidate, out reason);

            public bool TrySwapHunt(IPlayableHuntRuntime expectedCurrent, IPlayableHuntRuntime replacement, out string reason)
            {
                return huntPhaseManager.TrySwap(expectedCurrent, replacement, out reason);
            }

            public void ReleaseHunt(IPlayableHuntRuntime runtime)
            {
                huntPhaseManager.Release(runtime);
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

            public UniTask<CampaignRestartResult> RestartAsync(CancellationToken cancellationToken = default)
            {
                ThrowIfDisposed();
                if (IsActionSessionActive) return actionSession.RestartAsync(cancellationToken);
                return UniTask.FromResult(CampaignRestartResult.Failed("战役玩法运行态尚未启动"));
            }

            public void Reset()
            {
                ThrowIfDisposed();
                ResetGameplayRuntime();
                showdownPhaseManager.ResetCurrent();
                ResetHuntRuntime();
                ResetSettlementRuntime();
                persistentEffectProjection?.TrySynchronize(null, out _);
                phaseManager.Shutdown();
                phaseManager = CreatePhaseManager();
            }

            public void Dispose()
            {
                if (disposed) return;

                disposed = true;
                ResetGameplayRuntime();
                showdownPhaseManager.Dispose();
                huntPhaseManager.Dispose();
                settlementPhaseManager.Dispose();
                persistentEffectProjection?.Dispose();
                persistentEffectProjection = null;
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
                showdownPhaseManager.Dispose();
                huntPhaseManager.Dispose();
                settlementPhaseManager.Dispose();
                persistentEffectProjection?.Dispose();
                persistentEffectProjection = null;
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

            private void ResetHuntRuntime()
            {
                huntPhaseManager.Reset();
            }

            private void ResetSettlementRuntime()
            {
                settlementPhaseManager.Reset();
            }

            private void ThrowIfDisposed()
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(CampaignRuntime));
            }
        }
    }
}
