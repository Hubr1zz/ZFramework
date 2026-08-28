using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.ActionFlow;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;

namespace Core
{
    /// <summary>面向普通表现层的战役只读状态，不暴露阶段 Manager、Runtime 或 ActionSession。</summary>
    internal interface ICampaignReadModel
    {
        CampaignStartupState StartupState { get; }
        GamePhase CurrentPhase { get; }
        SettlementInstance Settlement { get; }
        IReadOnlyList<CraftRecipe> SettlementRecipes { get; }
        IReadOnlyList<HunterInstance> ActiveHuntHunters { get; }
        bool IsCampaignActive { get; }
        bool IsHuntActionRunning { get; }
        bool IsSettlementActionRunning { get; }
        bool IsSettlementEventRestoreReady { get; }
        bool IsHuntReturnInFlight { get; }
        CampaignSaveStatus SaveStatus { get; }
    }

    /// <summary>面向表现层的顶层战役命令；阶段内玩法继续使用各自的窄 gameplay port。</summary>
    internal interface ICampaignCommandPort
    {
        IPlayableSettlementGameplayPort SettlementGameplay { get; }
        IHuntExplorationPort HuntExploration { get; }
        UniTask<bool> HasSaveAsync(CancellationToken cancellationToken = default);
        UniTask<bool> DeleteSaveAsync(CancellationToken cancellationToken = default);
        UniTask<CampaignStartupResult> StartNewAsync(CancellationToken cancellationToken = default);
        UniTask<CampaignStartupResult> ContinueAsync(CancellationToken cancellationToken = default);
        UniTask<CampaignRestartResult> RestartAsync(CancellationToken cancellationToken = default);
        UniTask SaveAsync(bool includeActiveHunt, CancellationToken cancellationToken = default);
        UniTask<bool> RetryPendingSaveAsync(CancellationToken cancellationToken = default);
        UniTask<SettlementDepartureCommandResult> DepartForHuntAsync(IReadOnlyList<int> hunterIds, PlayableHuntDestination destination);
        UniTask<HuntRetreatCommandResult> RetreatAsync(HuntRetreatDecision decision, CancellationToken cancellationToken = default);
        UniTask<CampaignPhaseTransitionResult> TransitionAsync(CampaignPhaseTransitionRequest request, CancellationToken cancellationToken = default);
        UniTask<CampaignEncounterStartResult> BeginEncounterAsync(CampaignEncounterRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>仅供架构验证和内部诊断使用；具体运行态不得重新成为普通 View 的依赖。</summary>
    internal interface ICampaignDiagnostics
    {
        IPlayableHuntRuntime ActiveHuntRuntime { get; }
        IActionEnvironmentInstallerRegistry ActionEnvironmentInstallers { get; }
        ReactorRegistry CampaignReactors { get; }
        ReactorRegistry SettlementReactors { get; }
        ReactorRegistry HuntReactors { get; }
        bool IsCampaignActionSessionActive { get; }
        bool IsHuntActionSessionActive { get; }
    }

    /// <summary>把唯一 CampaignFlowCoordinator 投影为普通读、写与诊断三个互不混用的入口。</summary>
    internal sealed class CampaignAccessPorts : ICampaignReadModel, ICampaignCommandPort, ICampaignDiagnostics
    {
        private readonly CampaignFlowCoordinator flow;

        internal CampaignAccessPorts(CampaignFlowCoordinator flow)
        {
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
        }

        CampaignStartupState ICampaignReadModel.StartupState => flow.StartupState;
        GamePhase ICampaignReadModel.CurrentPhase => flow.CurrentPhase;
        SettlementInstance ICampaignReadModel.Settlement => flow.SettlementData;
        IReadOnlyList<CraftRecipe> ICampaignReadModel.SettlementRecipes => flow.SettlementRecipes;
        IReadOnlyList<HunterInstance> ICampaignReadModel.ActiveHuntHunters => flow.ActiveHuntHunters;
        bool ICampaignReadModel.IsCampaignActive => flow.CampaignStarted;
        bool ICampaignReadModel.IsHuntActionRunning => flow.IsHuntActionSessionRunning;
        bool ICampaignReadModel.IsSettlementActionRunning => flow.IsSettlementActionSessionRunning;
        bool ICampaignReadModel.IsSettlementEventRestoreReady => flow.IsSettlementEventRestoreReady;
        bool ICampaignReadModel.IsHuntReturnInFlight => flow.IsHuntReturnRecoveryInFlight;
        CampaignSaveStatus ICampaignReadModel.SaveStatus => flow.SaveStatus;

        IPlayableSettlementGameplayPort ICampaignCommandPort.SettlementGameplay => flow.SettlementGameplay;
        IHuntExplorationPort ICampaignCommandPort.HuntExploration => flow.ActiveHuntExplorationPort;
        UniTask<bool> ICampaignCommandPort.HasSaveAsync(CancellationToken cancellationToken) => flow.HasSaveAsync(cancellationToken);
        UniTask<bool> ICampaignCommandPort.DeleteSaveAsync(CancellationToken cancellationToken) => flow.DeleteSaveAsync(cancellationToken);
        UniTask<CampaignStartupResult> ICampaignCommandPort.StartNewAsync(CancellationToken cancellationToken) => flow.StartNewAsync(cancellationToken);
        UniTask<CampaignStartupResult> ICampaignCommandPort.ContinueAsync(CancellationToken cancellationToken) => flow.ContinueAsync(cancellationToken);
        UniTask<CampaignRestartResult> ICampaignCommandPort.RestartAsync(CancellationToken cancellationToken) => flow.RestartCampaignAsync(cancellationToken);
        UniTask ICampaignCommandPort.SaveAsync(bool includeActiveHunt, CancellationToken cancellationToken) => flow.SaveCampaignAsync(includeActiveHunt, cancellationToken);
        UniTask<bool> ICampaignCommandPort.RetryPendingSaveAsync(CancellationToken cancellationToken) => flow.RetryPendingSaveAsync(cancellationToken);
        UniTask<SettlementDepartureCommandResult> ICampaignCommandPort.DepartForHuntAsync(IReadOnlyList<int> hunterIds, PlayableHuntDestination destination) => flow.DepartForHuntAsyncGuarded(hunterIds, destination);
        UniTask<HuntRetreatCommandResult> ICampaignCommandPort.RetreatAsync(HuntRetreatDecision decision, CancellationToken cancellationToken) => flow.RequestRetreatAsync(decision, cancellationToken);
        UniTask<CampaignPhaseTransitionResult> ICampaignCommandPort.TransitionAsync(CampaignPhaseTransitionRequest request, CancellationToken cancellationToken) => flow.TransitionToPhaseAsync(request, cancellationToken);
        UniTask<CampaignEncounterStartResult> ICampaignCommandPort.BeginEncounterAsync(CampaignEncounterRequest request, CancellationToken cancellationToken) => flow.BeginEncounterAsync(request, cancellationToken);

        IPlayableHuntRuntime ICampaignDiagnostics.ActiveHuntRuntime => flow.ActiveHuntRuntime;
        IActionEnvironmentInstallerRegistry ICampaignDiagnostics.ActionEnvironmentInstallers => flow.ActionEnvironmentInstallers;
        ReactorRegistry ICampaignDiagnostics.CampaignReactors => flow.CampaignActionReactors;
        ReactorRegistry ICampaignDiagnostics.SettlementReactors => flow.SettlementActionReactors;
        ReactorRegistry ICampaignDiagnostics.HuntReactors => flow.HuntActionReactors;
        bool ICampaignDiagnostics.IsCampaignActionSessionActive => flow.IsCampaignActionSessionActive;
        bool ICampaignDiagnostics.IsHuntActionSessionActive => flow.IsHuntActionSessionActive;
    }
}
