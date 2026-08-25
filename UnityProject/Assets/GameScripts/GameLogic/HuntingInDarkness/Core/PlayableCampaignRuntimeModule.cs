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
        IActionEnvironmentInstallerRegistry ActionEnvironmentInstallers { get; }
        ReactorRegistry ActionReactors { get; }
        void ConfigureSettlementRuntime(PlayableSettlementRuntimeConfiguration configuration);
        bool TryPrepareNewSettlement(out IPlayableSettlementRuntime candidate, out string reason);
        bool TryPrepareSettlementRestore(SettlementInstance data, out IPlayableSettlementRuntime candidate, out string reason);
        bool TrySwapSettlement(IPlayableSettlementRuntime expectedCurrent, IPlayableSettlementRuntime replacement, out string reason);
        void ReleaseSettlement(IPlayableSettlementRuntime runtime);
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
            private readonly HashSet<PlayableSettlementRuntime> settlementRuntimes = new();
            private Action<GamePhase, GamePhase> onPhaseTransition;
            private PhaseManager phaseManager;
            private PlayableCampaignActionSession actionSession;
            private IDisposable gameplayInstallation;
            private PlayableSettlementRuntimeConfiguration settlementConfiguration;
            private PlayableSettlementRuntime settlement;
            private long nextSettlementGenerationId;
            private bool disposed;

            public long GenerationId { get; }
            public GamePhase CurrentPhase => phaseManager?.CurrentPhase ?? GamePhase.Settlement;
            public bool IsStarted => phaseManager?.IsStarted == true;
            public bool IsActionSessionActive => actionSession?.IsActive == true;
            public bool IsActionSessionRunning => actionSession?.IsRunning == true;
            public IPlayableSettlementRuntime Settlement => settlement;
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

            public void ConfigureSettlementRuntime(PlayableSettlementRuntimeConfiguration configuration)
            {
                ThrowIfDisposed();
                if (settlementConfiguration != null) throw new InvalidOperationException("营地运行态配置已经安装。");
                settlementConfiguration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            }

            public bool TryPrepareNewSettlement(out IPlayableSettlementRuntime candidate, out string reason)
            {
                ThrowIfDisposed();
                candidate = null;
                if (!TryGetSettlementConfiguration(out reason)) return false;
                var runtime = new PlayableSettlementRuntime(++nextSettlementGenerationId, new SettlementManager(), settlementConfiguration, false);
                settlementRuntimes.Add(runtime);
                candidate = runtime;
                reason = string.Empty;
                return true;
            }

            public bool TryPrepareSettlementRestore(SettlementInstance data, out IPlayableSettlementRuntime candidate, out string reason)
            {
                ThrowIfDisposed();
                candidate = null;
                if (!TryGetSettlementConfiguration(out reason)) return false;
                if (!SettlementManager.TryPrepareCandidate(data, out SettlementManager manager, out reason)) return false;
                var runtime = new PlayableSettlementRuntime(++nextSettlementGenerationId, manager, settlementConfiguration, true);
                settlementRuntimes.Add(runtime);
                candidate = runtime;
                reason = string.Empty;
                return true;
            }

            public bool TrySwapSettlement(IPlayableSettlementRuntime expectedCurrent, IPlayableSettlementRuntime replacement, out string reason)
            {
                ThrowIfDisposed();
                if (!ReferenceEquals(settlement, expectedCurrent))
                {
                    reason = "权威营地运行世代已变化，拒绝提交过期候选。";
                    return false;
                }
                PlayableSettlementRuntime next = replacement as PlayableSettlementRuntime;
                if (replacement != null && (next == null || !settlementRuntimes.Contains(next) || !next.IsDetached))
                {
                    reason = "替换目标不是当前战役持有的可发布营地候选。";
                    return false;
                }
                if (next != null && !next.TryPreparePublication(out reason)) return false;

                PlayableSettlementRuntime previous = settlement;
                previous?.Detach();
                next?.Publish();
                settlement = next;
                reason = string.Empty;
                return true;
            }

            public void ReleaseSettlement(IPlayableSettlementRuntime runtime)
            {
                ThrowIfDisposed();
                if (runtime is not PlayableSettlementRuntime owned || !settlementRuntimes.Contains(owned))
                    throw new InvalidOperationException("营地运行世代不属于当前战役。");
                if (owned.IsCurrent)
                    throw new InvalidOperationException("不能释放当前权威营地运行世代。");
                owned.Dispose();
                settlementRuntimes.Remove(owned);
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
                ResetSettlementRuntime();
                phaseManager.Shutdown();
                phaseManager = CreatePhaseManager();
            }

            public void Dispose()
            {
                if (disposed) return;

                disposed = true;
                ResetGameplayRuntime();
                ResetSettlementRuntime();
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
                ResetSettlementRuntime();
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

            private bool TryGetSettlementConfiguration(out string reason)
            {
                if (settlementConfiguration != null)
                {
                    reason = string.Empty;
                    return true;
                }
                reason = "营地运行态组合配置尚未安装。";
                return false;
            }

            private void ResetSettlementRuntime()
            {
                foreach (PlayableSettlementRuntime runtime in settlementRuntimes)
                    runtime.Dispose();
                settlementRuntimes.Clear();
                settlement = null;
            }

            private void ThrowIfDisposed()
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(CampaignRuntime));
            }
        }
    }
}
