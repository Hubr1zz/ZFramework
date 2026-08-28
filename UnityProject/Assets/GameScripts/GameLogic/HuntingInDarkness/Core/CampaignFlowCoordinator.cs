using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Config;
using GameplayBase;
using GameplayBase.Config;
using GameplayBase.CombatSystem;
using HuntingInDarkness.ActionFlow;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.ActionFlow.Inventions;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using CardGame.ActionQueue;

namespace Core
{
    /// <summary>
    /// Campaign-owned flow coordinator. It owns all runtime leases and cross-stage transactions;
    /// Unity presentation/configuration is supplied by CampaignFlowBindings.
    /// </summary>
    internal sealed class CampaignFlowCoordinator : ICampaignPhaseTransitionHost, ICampaignPhaseTransitionRequestHost, ICampaignRestartHost, ICampaignStartupTransactionHost, ICampaignHuntReturnHost, ICampaignHuntDepartureHost, ICampaignShowdownOutcomeHost, ICampaignEncounterHandoffHost, ISettlementDepartureRequestPort, IPlayableHuntRetreatInput
    {
        private readonly CampaignFlowBindings bindings;
        private readonly ICampaignPersistencePort persistence;
        private readonly IPlayableCampaignRuntime campaignRuntime;
        private readonly IPlayableSettlementPhasePort settlementPhase;
        private readonly IPlayableSettlementGameplayPort settlementGameplay;
        private readonly IPlayableHuntPhasePort huntPhase;
        private readonly IPlayableShowdownPhasePort showdownPhase;
        private readonly CampaignPersistenceCoordinator persistenceCoordinator;
        private readonly CampaignStartupTransaction startup;
        private CampaignRestartTransaction restart;
        private readonly CampaignHuntReturnTransaction huntReturn;
        private readonly CampaignHuntDepartureTransaction huntDeparture;
        private readonly CampaignShowdownOutcomeTransaction showdownOutcome;
        private readonly CampaignEncounterHandoffTransaction encounterHandoff;
        private readonly ActiveHuntRestoreTransaction activeHuntRestore;
        private IPlayableEventInput playableEventInput;
        private IPlayableHuntDepartureInput playableHuntDepartureInput;
        private int devLoadGeneration;
        private bool devLoadInFlight;
        private bool disposed;

        internal IPlayableCampaignRuntime CampaignRuntime => campaignRuntime;
        internal IPlayableSettlementPhasePort SettlementPhase => settlementPhase;
        internal IPlayableHuntPhasePort HuntPhase => huntPhase;
        internal IPlayableShowdownPhasePort ShowdownPhase => showdownPhase;
        internal IPlayableSettlementGameplayPort SettlementGameplay => settlementGameplay;
        internal IPlayableShowdownGameplayPort ShowdownGameplay => showdownPhase?.Gameplay;
        internal CampaignPersistenceCoordinator Persistence => persistenceCoordinator;
        internal CampaignStartupTransaction Startup => startup;
        internal CampaignRestartTransaction Restart => restart;
        internal CampaignHuntReturnTransaction HuntReturn => huntReturn;
        internal CampaignHuntDepartureTransaction HuntDeparture => huntDeparture;
        internal CampaignShowdownOutcomeTransaction ShowdownOutcome => showdownOutcome;
        internal CampaignEncounterHandoffTransaction EncounterHandoff => encounterHandoff;
        internal ActiveHuntRestoreTransaction ActiveHuntRestore => activeHuntRestore;
        internal bool CampaignStarted => startup.IsRuntimeActive;
        internal IPlayableSettlementRuntime SettlementRuntime => campaignRuntime?.Settlement;
        internal IPlayableHuntRuntime HuntRuntime => campaignRuntime?.Hunt;
        internal PlayableSettlementActionSession SettlementActionSession => settlementPhase?.CurrentSession;
        internal PlayableHuntActionSession HuntActionSession => HuntRuntime?.ActionSession;
        internal SettlementManager SettlementManager => SettlementRuntime?.Manager;
        internal HuntManager HuntManager => HuntRuntime?.Manager;
        internal GamePhase CurrentPhase => campaignRuntime?.CurrentPhase ?? GamePhase.Settlement;
        internal bool IsHuntReturnRecoveryInFlight => huntReturn?.IsRecoveryInFlight == true;
        internal string ActiveExpeditionId => HuntRuntime?.ExpeditionId;

        internal CampaignFlowCoordinator(CampaignFlowBindings flowBindings, ICampaignPersistencePort campaignPersistence, bool waitForEntrySelection)
        {
            bindings = flowBindings ?? throw new ArgumentNullException(nameof(flowBindings));
            persistence = campaignPersistence ?? throw new ArgumentNullException(nameof(campaignPersistence));
            campaignRuntime = GameModule.Campaign.AcquireRuntime(this, bindings.ApplyPhaseRoots);
            if (campaignRuntime is not IPlayableCampaignPhasePortAccess phaseAccess)
                throw new InvalidOperationException("战役运行态未提供阶段管理器组合根访问接口。");
            settlementPhase = phaseAccess.SettlementPhase;
            settlementGameplay = phaseAccess.SettlementGameplay;
            huntPhase = phaseAccess.HuntPhase;
            showdownPhase = phaseAccess.ShowdownPhase;
            persistenceCoordinator = new CampaignPersistenceCoordinator(persistence, TryCaptureCampaignSnapshot);
            startup = new CampaignStartupTransaction(persistence);
            startup.Configure(waitForEntrySelection);
            restart = new CampaignRestartTransaction(campaignRuntime, persistence, PrepareCampaignRestartPayload, message => bindings.Warning?.Invoke(message));
            huntReturn = new CampaignHuntReturnTransaction(this);
            huntDeparture = new CampaignHuntDepartureTransaction(this);
            showdownOutcome = new CampaignShowdownOutcomeTransaction(this);
            encounterHandoff = new CampaignEncounterHandoffTransaction(this);
            activeHuntRestore = new ActiveHuntRestoreTransaction(campaignRuntime, () => playableEventInput, huntPhase.TryStartCurrentPresentationAndSession, huntPhase.DeactivateCurrentActionSession, () => huntPhase.CleanupCurrentPresentation(), RestorePreviousHuntPresentation, message => bindings.Warning?.Invoke(message));
            startup.Bind(this);
        }

        internal void ConfigurePersistentEffectProjection(Func<IActionEnvironmentInstallerRegistry, IPlayableCampaignPersistentEffectProjection> factory)
            => campaignRuntime.ConfigurePersistentEffectProjection(factory);

        internal void ConfigureGameplay(ITabletopRandomInteractionPresenter tabletop)
        {
            settlementPhase.ConfigureGameplay(() => playableEventInput, tabletop, () => campaignRuntime.ActionEnvironmentInstallers, () => campaignRuntime.PersistentEffectProjection);
        }

        internal void ConfigureSettlement()
            => settlementPhase.ConfigureRuntime(this);

        internal void ConfigureSettlementPresentation()
            => settlementPhase.ConfigurePresentation(bindings.SettlementTable, bindings.SettlementRoot, bindings.WorkshopCatalog, bindings.SettlementContentCatalog, RequestHuntDepartureFromSettlement);

        private void RequestHuntDepartureFromSettlement(List<HunterInstance> squad)
            => RequestHuntDeparture(squad != null ? squad.Where(hunter => hunter != null).Select(hunter => hunter.InstanceId).ToList() : new List<int>());

        internal void ConfigureHunt()
        {
            huntPhase.Configure(() => campaignRuntime.ActionEnvironmentInstallers, bindings.TabletopInteraction, bindings.HuntRoot, bindings.UiHunt, this, RequestEncounter, HandleHuntCompleted, OnHuntCheckpointCommitted);
            huntPhase.ConfigureRuntime();
        }

        private void RequestEncounter(CampaignEncounterRequest request)
            => BeginEncounterAsync(request, ResolveLifetimeToken()).Forget();

        internal void EnsureGameplayRuntime(IActionEnvironmentInstaller gameplayInstaller)
            => campaignRuntime.EnsureGameplayRuntime(gameplayInstaller);

        internal bool TryStart(GamePhase startPhase, bool queueSettlementEvents, out string reason, IPlayableSettlementRuntime preparedSettlement = null, bool activateOnSuccess = true)
        {
            reason = string.Empty;
            if (CampaignStarted)
                return Fail("战役运行态已经启动。", out reason);
            try
            {
                IPlayableSettlementRuntime candidate = preparedSettlement;
                if (candidate == null && !campaignRuntime.TryPrepareNewSettlement(out candidate, out reason)) return false;
                if (!campaignRuntime.TrySwapSettlement(null, candidate, out reason))
                {
                    campaignRuntime.ReleaseSettlement(candidate);
                    return false;
                }
                EnsureCampaignShell();
                campaignRuntime.Start(startPhase);
                if (startPhase == GamePhase.Settlement)
                {
                    if (preparedSettlement == null) SettlementManager?.EnsureStartingConditions();
                    if (!settlementPhase.ActivateCurrentActionSession(out reason)) throw new InvalidOperationException(reason);
                    if (activateOnSuccess) settlementPhase.EnsurePresentation(SettlementManager);
                    if (queueSettlementEvents) QueueSettlementEvents(SettlementManager?.OnEnterWorkItems());
                }
                else if (startPhase == GamePhase.Hunt)
                {
                    SettlementManager?.EnsureStartingConditions();
                    if (!huntDeparture.TryStartDevelopmentHunt(out string huntReason))
                    {
                        bindings.Error?.Invoke($"开发者狩猎直启失败：{huntReason}");
                        campaignRuntime.TransitionTo(GamePhase.Settlement);
                        if (!settlementPhase.ActivateCurrentActionSession(out reason)) throw new InvalidOperationException(reason);
                        settlementPhase.EnsurePresentation(SettlementManager);
                        if (queueSettlementEvents) QueueSettlementEvents(SettlementManager?.OnEnterWorkItems());
                    }
                    else
                        PlayableCampaignLoopContract.ConsumeDepartureRoster(SettlementManager?.Data);
                }
                else if (startPhase == GamePhase.BossFight)
                {
                    if (!TryPrepareCombatSession(out reason)) throw new InvalidOperationException(reason);
                    StartPreparedCombatSession();
                }
                if (activateOnSuccess) startup.ActivateRuntime();
                return true;
            }
            catch (Exception exception)
            {
                ResetFailedStartup();
                reason = $"战役运行态初始化异常：{exception.Message}";
                return false;
            }
        }

        internal bool TryRestoreActiveHunt(CampaignSnapshot snapshot, out string reason)
        {
            ActiveHuntRestoreResult result = activeHuntRestore.Execute(snapshot);
            reason = result.Reason;
            if (!string.IsNullOrWhiteSpace(result.StablePayload)) persistenceCoordinator.Adopt(result.StablePayload);
            return result.Succeeded;
        }

        internal void ResetFailedStartup()
        {
            if (disposed) return;
            huntDeparture.Reset();
            huntReturn.Reset();
            encounterHandoff.Reset();
            settlementPhase?.DeactivateCurrentActionSession();
            huntPhase?.DeactivateCurrentActionSession();
            persistenceCoordinator.Reset();
            startup.DeactivateRuntime();
            campaignRuntime.Reset();
            huntPhase?.CleanupCurrentPresentation();
            bindings.DeactivatePhaseRoots?.Invoke();
        }

        internal bool CanRequestHuntDeparture(out string reason)
            => huntDeparture.CanRequest(out reason);

        internal bool TryRequestHuntDeparture(IReadOnlyList<int> hunterIds, CancellationToken cancellationToken)
            => huntDeparture.TryRequest(hunterIds, cancellationToken);

        internal UniTask<SettlementDepartureCommandResult> DepartForHuntAsync(IReadOnlyList<int> hunterIds, PlayableHuntDestination destination, CancellationToken cancellationToken)
            => huntDeparture.ExecuteAsync(hunterIds, destination, cancellationToken);

        internal UniTask<HuntRetreatCommandResult> RequestRetreatAsync(HuntRetreatDecision decision, CancellationToken cancellationToken)
            => huntReturn.PrepareRetreatAsync(decision, cancellationToken);

        bool ISettlementDepartureRequestPort.RequestDeparture(IReadOnlyList<int> hunterIds)
        {
            if (disposed || !CampaignStarted || CurrentPhase != GamePhase.Settlement) return false;
            return TryDepartForHunt(hunterIds);
        }

        bool IPlayableHuntRetreatInput.IsReturnCheckpointLocked
            => disposed || !CampaignStarted || CurrentPhase != GamePhase.Hunt || IsReturnCheckpointLocked;

        HuntRetreatPreview IPlayableHuntRetreatInput.GetRetreatPreview()
        {
            if (disposed || !CampaignStarted || CurrentPhase != GamePhase.Hunt)
                return HuntRetreatPreview.Empty;

            HuntRetreatPreview preview = RetreatPreview;
            TimelineSystem timeline = SettlementManager?.Timeline;
            if (timeline == null)
                return preview.WithCalendar(HuntReturnCalendarPreview.Unavailable("营地时间线不可用，暂不能回营。"));
            return preview.WithCalendar(HuntReturnCalendarPreview.Create(timeline.Calendar, timeline.CurrentYear, timeline.CurrentSeasonIndex));
        }

        UniTask<HuntRetreatCommandResult> IPlayableHuntRetreatInput.RequestRetreatAsync(HuntRetreatDecision decision)
        {
            if (disposed || !CampaignStarted || CurrentPhase != GamePhase.Hunt)
                return UniTask.FromResult(HuntRetreatCommandResult.Failed("当前阶段不能回营。"));
            return RequestRetreatAsync(decision, ResolveLifetimeToken());
        }

        internal UniTask<bool> ApplyPendingReturnAsync(bool queueSettlementEvents, CancellationToken cancellationToken)
            => ApplyPendingReturnCoreAsync(queueSettlementEvents, cancellationToken);

        private async UniTask<bool> ApplyPendingReturnCoreAsync(bool queueSettlementEvents, CancellationToken cancellationToken)
            => (await huntReturn.ApplyPendingAsync(queueSettlementEvents, cancellationToken)).Succeeded;

        internal void QueueSettlementEvents(IReadOnlyList<SettlementEventWork> works, SettlementEventRestoreProjection projection = null, string restoredChainId = null)
            => settlementPhase.QueueCurrentEvents(works, projection, restoredChainId);

        internal bool TryCaptureCampaignPayload(bool includeActiveHunt, out string payload, out string reason)
            => persistenceCoordinator.TryCapture(includeActiveHunt, out payload, out reason);

        internal UniTask<bool> SaveCampaignAsync(bool includeActiveHunt, CancellationToken cancellationToken)
            => persistenceCoordinator.TrySaveAsync(includeActiveHunt, cancellationToken);

        internal CampaignSaveStatus SaveStatus => persistenceCoordinator.Status;

        internal UniTask<bool> RetryPendingSaveAsync(CancellationToken cancellationToken)
            => persistenceCoordinator.RetryPendingSaveAsync(CurrentPhase == GamePhase.Hunt, cancellationToken);

        internal bool TrySaveImmediate(string payload) => persistenceCoordinator.TrySaveImmediate(payload);

        internal void Adopt(string payload) => persistenceCoordinator.Adopt(payload);

        internal void Reset()
        {
            if (disposed) return;
            InvalidateDevLoad();
            startup.Invalidate();
            huntDeparture.Reset();
            huntReturn.Reset();
            encounterHandoff.Reset();
            persistenceCoordinator.Reset();
            startup.DeactivateRuntime();
            campaignRuntime.Reset();
        }

        internal void Dispose()
        {
            if (disposed) return;
            startup.Dispose();
            InvalidateDevLoad();
            disposed = true;
            huntDeparture.Reset();
            huntReturn.Reset();
            encounterHandoff.Reset();
            persistenceCoordinator.Reset();
            campaignRuntime.Dispose();
        }

        private bool TryCaptureCampaignSnapshot(bool includeActiveHunt, out CampaignSnapshot snapshot, out string reason)
        {
            if (includeActiveHunt)
            {
                if (!ActiveHuntSnapshotAdapter.TryCapture(SettlementManager?.Data, HuntManager, HuntActionSession, ActiveExpeditionId, out snapshot, out reason)) return false;
            }
            else
                snapshot = ActiveHuntSnapshotAdapter.CaptureSettlement(SettlementManager?.Data);
            reason = string.Empty;
            return snapshot != null;
        }

        private void RestorePreviousHuntPresentation(GamePhase previousPhase, IPlayableHuntRuntime previousHunt)
        {
            huntPhase.RestorePreviousPresentation(previousPhase, previousHunt);
            if (previousPhase == GamePhase.Settlement)
                settlementPhase.EnsurePresentation(SettlementManager);
        }

        private static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }

        internal CampaignStartupState StartupState => startup.State;
        internal bool WaitForEntrySelection => startup.WaitForEntrySelection;
        internal ICampaignPersistencePort PersistencePort => persistence;
        internal SettlementInstance SettlementData => CampaignStarted ? SettlementManager?.Data : null;
        internal IReadOnlyList<CraftRecipe> SettlementRecipes => CampaignStarted && SettlementManager?.Workshop?.AllRecipes != null ? SettlementManager.Workshop.AllRecipes : Array.Empty<CraftRecipe>();
        internal IReadOnlyList<HunterInstance> ActiveHuntHunters => HuntManager?.ActiveHunters != null ? HuntManager.ActiveHunters : Array.Empty<HunterInstance>();
        internal IPlayableHuntRuntime ActiveHuntRuntime => CampaignStarted && CurrentPhase is GamePhase.Hunt or GamePhase.BossFight ? HuntRuntime : null;
        internal IActionEnvironmentInstallerRegistry ActionEnvironmentInstallers => campaignRuntime?.ActionEnvironmentInstallers;
        internal ReactorRegistry SettlementActionReactors => CampaignStarted ? SettlementActionSession?.Reactors : null;
        internal ReactorRegistry CampaignActionReactors => CampaignStarted ? campaignRuntime?.ActionReactors : null;
        internal ReactorRegistry HuntActionReactors => HuntActionSession?.Reactors;
        internal IHuntExplorationPort ActiveHuntExplorationPort => CampaignStarted && CurrentPhase == GamePhase.Hunt && HuntRuntime?.Exploration?.IsActive == true ? HuntRuntime.Exploration.Port : null;
        internal PlayableCombatSession CombatSession => showdownPhase?.Current;
        internal BattleSetup PendingBattleSetup => encounterHandoff?.PendingSetup;
        internal EventSystem SettlementEvents => SettlementManager?.Events;
        internal UnityEngine.Transform HuntTabletopInteractionAnchor => huntPhase?.Visualizer?.TabletopInteractionAnchor;
        internal bool IsHuntActionSessionActive => HuntActionSession?.IsActive == true;
        internal bool IsHuntActionSessionRunning => HuntActionSession?.IsRunning == true;
        internal bool IsCampaignActionSessionActive => CampaignStarted && campaignRuntime?.IsActionSessionActive == true;
        internal bool IsSettlementActionSessionRunning => CampaignStarted && SettlementActionSession?.IsRunning == true;
        internal bool IsSettlementEventRestoreReady => CampaignStarted && settlementPhase?.IsEventRestoreReady == true;
        internal bool IsReturnCheckpointLocked => HuntActionSession?.IsReturnCheckpointLocked == true;
        internal HuntRetreatPreview RetreatPreview => HuntActionSession != null ? HuntActionSession.GetRetreatPreview() : HuntRetreatPreview.Empty;

        internal UniTask<bool> HasSaveAsync(CancellationToken cancellationToken = default) => startup.HasSaveAsync(cancellationToken);
        internal UniTask<bool> DeleteSaveAsync(CancellationToken cancellationToken = default) => startup.DeleteSaveAsync(cancellationToken);
        internal UniTask<CampaignStartupResult> StartNewAsync(CancellationToken cancellationToken = default) => startup.StartNewAsync(cancellationToken);
        internal UniTask<CampaignStartupResult> ContinueAsync(CancellationToken cancellationToken = default) => startup.ContinueAsync(cancellationToken);

        internal void EnsureSettlementPresentation() => settlementPhase?.EnsurePresentation(SettlementManager);
        internal void SetPlayableEventInput(IPlayableEventInput input)
        {
            playableEventInput = input;
            if (SettlementActionSession != null) SettlementActionSession.EventInput = input;
            if (HuntManager != null) HuntManager.EventInput = input;
        }

        internal void ClearPlayableEventInput(IPlayableEventInput input)
        {
            if (!ReferenceEquals(playableEventInput, input)) return;
            playableEventInput = null;
            if (SettlementActionSession != null && ReferenceEquals(SettlementActionSession.EventInput, input)) SettlementActionSession.EventInput = null;
            if (HuntManager != null && ReferenceEquals(HuntManager.EventInput, input)) HuntManager.EventInput = null;
        }

        internal void SetPlayableHuntDepartureInput(IPlayableHuntDepartureInput input) => playableHuntDepartureInput = input;

        internal void ClearPlayableHuntDepartureInput(IPlayableHuntDepartureInput input)
        {
            if (ReferenceEquals(playableHuntDepartureInput, input)) playableHuntDepartureInput = null;
        }

        internal void RequestHuntDeparture(IReadOnlyList<int> hunterIds)
        {
            if (!CampaignStarted) return;
            if (SettlementData?.PendingHuntReturn != null)
            {
                bindings.PresentDepartureBlockedNotice?.Invoke("请先完成上一场远征的回营结算，再重新发起出猎。");
                if (huntReturn.IsRecoveryInFlight != true) RetryPendingHuntReturnAsync().Forget();
                return;
            }
            if (!huntDeparture.CanRequest(out string reason))
            {
                bindings.PresentDepartureBlockedNotice?.Invoke(reason);
                return;
            }
            if (playableHuntDepartureInput != null)
            {
                bindings.ClearDepartureBlockedNotice?.Invoke();
                playableHuntDepartureInput.RequestDeparture(hunterIds);
                return;
            }
            huntDeparture.ExecuteAsync(hunterIds, null, ResolveLifetimeToken()).Forget();
        }

        internal async UniTask<SettlementDepartureCommandResult> DepartForHuntAsyncGuarded(IReadOnlyList<int> hunterIds, PlayableHuntDestination destination)
        {
            if (SettlementData?.PendingHuntReturn != null)
            {
                if (huntReturn.IsRecoveryInFlight != true) await RetryPendingHuntReturnAsync();
                return SettlementDepartureCommandResult.Failed("请先完成上一场远征的回营结算，再重新发起出猎。");
            }
            return await huntDeparture.ExecuteAsync(hunterIds, destination, ResolveLifetimeToken());
        }

        internal bool TryDepartForHunt(IReadOnlyList<int> hunterIds)
        {
            if (SettlementData?.PendingHuntReturn != null)
            {
                if (huntReturn.IsRecoveryInFlight != true) RetryPendingHuntReturnAsync().Forget();
                return false;
            }
            return huntDeparture.TryRequest(hunterIds, ResolveLifetimeToken());
        }

        internal void TransitionToPhase(GamePhase phase)
        {
            if (CurrentPhase == GamePhase.Hunt && phase == GamePhase.Settlement && huntReturn.IsPreparedExit != true && SettlementData?.PendingHuntReturn == null)
            {
                RequestRetreatAsync(HuntRetreatDecision.None, ResolveLifetimeToken()).Forget();
                return;
            }
            TransitionToPhaseAsync(phase, ResolveLifetimeToken()).Forget();
        }

        internal UniTask<CampaignPhaseTransitionResult> TransitionToPhaseAsync(GamePhase phase, CancellationToken cancellationToken)
        {
            if (!CampaignStarted) return UniTask.FromResult(CampaignPhaseTransitionResult.Failed(CurrentPhase, "战役入口尚未完成。"));
            if (campaignRuntime?.IsActionSessionActive != true) return UniTask.FromResult(CampaignPhaseTransitionResult.Failed(CurrentPhase, "战役玩法运行态尚未启动。"));
            return campaignRuntime.TransitionAsync(phase, cancellationToken);
        }

        internal UniTask<CampaignPhaseTransitionResult> TransitionToPhaseAsync(CampaignPhaseTransitionRequest request, CancellationToken cancellationToken)
        {
            if (!CampaignStarted) return UniTask.FromResult(CampaignPhaseTransitionResult.Failed(CurrentPhase, "战役入口尚未完成。"));
            if (campaignRuntime?.IsActionSessionActive != true) return UniTask.FromResult(CampaignPhaseTransitionResult.Failed(CurrentPhase, "战役玩法运行态尚未启动。"));
            return campaignRuntime.TransitionAsync(request, cancellationToken);
        }

        internal UniTask<CampaignEncounterStartResult> BeginEncounterAsync(CampaignEncounterRequest request, CancellationToken cancellationToken)
        {
            if (!CampaignStarted) return UniTask.FromResult(CampaignEncounterStartResult.Failed(request.EncounterId, "战役入口尚未完成。"));
            if (campaignRuntime?.IsActionSessionActive != true) return UniTask.FromResult(CampaignEncounterStartResult.Failed(request.EncounterId, "战役玩法运行态尚未启动。"));
            return encounterHandoff.ExecuteAsync(request, cancellationToken);
        }

        internal UniTask<CampaignRestartResult> RestartCampaignAsync(CancellationToken cancellationToken)
        {
            if (!CampaignStarted) return UniTask.FromResult(CampaignRestartResult.Failed("战役入口尚未完成。"));
            if (campaignRuntime?.IsActionSessionActive != true) return UniTask.FromResult(CampaignRestartResult.Failed("战役玩法运行态尚未启动。"));
            return campaignRuntime.RestartAsync(cancellationToken);
        }

        internal async UniTask<CampaignRestartResult> RestartFromActionAsync(CancellationToken cancellationToken)
        {
            if (huntDeparture.IsInFlight || huntReturn.IsRetreatInFlight || huntReturn.IsRecoveryInFlight || SettlementActionSession?.IsRunning == true || HuntActionSession?.IsRunning == true)
                return CampaignRestartResult.Failed("当前玩法流程仍在结算，请稍后重试。");
            CampaignRestartTransactionResult result = await restart.ExecuteAsync(persistenceCoordinator.StablePayload, cancellationToken);
            if (!result.Succeeded) return CampaignRestartResult.Failed(result.Reason);
            DisposeCombatSession();
            huntPhase.CleanupCurrentPresentation();
            huntDeparture.Reset();
            encounterHandoff.Reset();
            huntReturn.Reset();
            persistenceCoordinator.Adopt(result.StablePayload);
            bindings.ResetSettlementNotices?.Invoke();
            settlementPhase.EnsurePresentation(SettlementManager);
            QueueSettlementEvents(SettlementManager.OnEnterWorkItems());
            return CampaignRestartResult.Success();
        }

        internal void SetPendingBattleSetup(BattleSetup setup) => encounterHandoff.SetPendingSetup(setup);

        internal void Update() => showdownPhase?.Update();

        internal void HandleBossDefeated()
        {
            if (CurrentPhase == GamePhase.BossFight && showdownPhase.Current != null) showdownOutcome.HandleBossDefeated();
        }

        internal void HandleCampaignEncounterRequested(CampaignEncounterRequestedEvent evt)
            => BeginCampaignEncounterRequestedAsync(evt.Request).Forget();

        private async UniTaskVoid BeginCampaignEncounterRequestedAsync(CampaignEncounterRequest request)
        {
            CampaignEncounterStartResult result = await BeginEncounterAsync(request, ResolveLifetimeToken());
            if (!result.Succeeded) bindings.Warning?.Invoke($"无法开始遭遇 {request.EncounterId}：{result.Reason}");
        }

        internal void HandlePlayableEventEncounterRequested(PlayableEventEncounterRequestedEvent evt)
        {
            if (CurrentPhase == GamePhase.Settlement && SettlementActionSession?.IsActive == true)
            {
                var request = new CampaignEncounterRequest(SettlementActionSession.SessionId, string.IsNullOrWhiteSpace(evt.EncounterId) ? PlayableEncounterRuntime.DefaultEncounterId : evt.EncounterId, CampaignEncounterSourceKind.SettlementEvent, GamePhase.Settlement, UnityEngine.Vector2Int.zero, evt.SourceEventId, "settlement");
                BeginEncounterAsync(request, ResolveLifetimeToken()).Forget();
                return;
            }
            if (CurrentPhase != GamePhase.Hunt || HuntActionSession?.IsActive != true) return;
            var huntRequest = new CampaignEncounterRequest(HuntActionSession.SessionId, string.IsNullOrWhiteSpace(evt.EncounterId) ? PlayableEncounterRuntime.DefaultEncounterId : evt.EncounterId, CampaignEncounterSourceKind.HuntEvent, GamePhase.Hunt, HuntManager?.SquadPosition ?? UnityEngine.Vector2Int.zero, evt.SourceEventId, HuntManager?.BoundRoute?.DestinationId ?? string.Empty);
            BeginEncounterAsync(huntRequest, ResolveLifetimeToken()).Forget();
        }

        internal void HandleSettlementTransactionCommitted(SettlementTransactionCommittedEvent evt)
        {
            if (!CampaignStarted || CurrentPhase != GamePhase.Settlement || SettlementActionSession == null) return;
            SaveCampaignAsync(false, ResolveLifetimeToken()).Forget();
            if (evt.Kind == SettlementTransactionKind.Crafting) settlementPhase.RefreshCrafting();
            else settlementPhase.Refresh();
        }

        internal void HighlightCardPreview(int cardInstanceId) => showdownPhase.Current?.HighlightCardPreview(cardInstanceId);
        internal void ClearCardPreview() => showdownPhase.Current?.ClearCardPreview();

        internal HunterInstance DevAddHunter(string name)
        {
            HunterInstance hunter = SettlementManager?.DevAddHunter(name);
            settlementPhase?.Refresh();
            return hunter;
        }

        internal bool DevAddResource(string resourceName, int amount)
        {
            if (SettlementManager == null) return false;
            SettlementManager.DevAddResource(resourceName, amount);
            settlementPhase?.RefreshCards();
            return true;
        }

        internal void FlushOnApplicationQuit()
        {
            if (!CampaignStarted || SettlementManager?.Data == null) return;
            if (CurrentPhase == GamePhase.Settlement) TryCaptureCampaignPayload(false, out _, out _);
            else if (CurrentPhase == GamePhase.Hunt && HuntActionSession?.IsRunning != true) TryCaptureCampaignPayload(true, out _, out _);
            if (!string.IsNullOrWhiteSpace(persistenceCoordinator.StablePayload)) persistenceCoordinator.TrySaveImmediate(persistenceCoordinator.StablePayload);
        }

        internal async UniTaskVoid LoadSnapshotFromPersistenceAsync()
        {
            if (disposed || devLoadInFlight) return;
            devLoadInFlight = true;
            int generation = ++devLoadGeneration;
            bool completionSent = false;
            void Complete(bool succeeded)
            {
                if (completionSent) return;
                completionSent = true;
                bindings.SettlementLoadCompleted?.Invoke(succeeded);
            }
            try
            {
                CampaignSnapshot snapshot = await persistence.LoadAsync(ResolveLifetimeToken());
                if (!IsCurrentDevLoad(generation)) return;
                if (snapshot?.Settlement == null)
                {
                    bindings.Warning?.Invoke("DevLoad: 无存档文件");
                    Complete(false);
                    return;
                }
                if (huntDeparture.IsInFlight || huntReturn.IsRetreatInFlight || huntReturn.IsRecoveryInFlight || SettlementActionSession?.IsRunning == true || HuntActionSession?.IsRunning == true || campaignRuntime.IsActionSessionRunning)
                {
                    bindings.Warning?.Invoke("DevLoad: 当前流程仍在执行，已拒绝替换运行态。");
                    Complete(false);
                    return;
                }
                huntDeparture.Reset();
                bool restored = await RestoreSnapshotAsync(snapshot, true, ResolveLifetimeToken());
                if (!IsCurrentDevLoad(generation)) return;
                Complete(restored);
            }
            catch (OperationCanceledException)
            {
                if (!IsCurrentDevLoad(generation)) return;
                bindings.Warning?.Invoke("DevLoad 已取消。");
                Complete(false);
            }
            catch (Exception exception)
            {
                if (!IsCurrentDevLoad(generation)) return;
                bindings.Error?.Invoke($"DevLoad 失败：{exception.Message}");
                Complete(false);
            }
            finally
            {
                if (generation == devLoadGeneration)
                    devLoadInFlight = false;
            }
        }

        internal async UniTask<bool> RestoreSnapshotAsync(CampaignSnapshot snapshot, bool replaceCurrent, CancellationToken cancellationToken)
        {
            if (snapshot?.Settlement == null) return false;
            EnsureCampaignShell();
            if (snapshot.HasActiveHunt)
            {
                ActiveHuntRestoreResult huntResult = activeHuntRestore.Execute(snapshot);
                if (!string.IsNullOrWhiteSpace(huntResult.StablePayload)) persistenceCoordinator.Adopt(huntResult.StablePayload);
                if (!huntResult.Succeeded)
                {
                    bindings.Error?.Invoke($"活动狩猎恢复失败，已保留原存档：{huntResult.Reason}");
                    if (!replaceCurrent) ResetFailedStartup();
                    return false;
                }
                startup.ActivateRuntime();
                return true;
            }
            return replaceCurrent ? await ReplaceSettlementSnapshotAsync(snapshot, cancellationToken) : await StartSettlementSnapshotAsync(snapshot, cancellationToken);
        }

        private async UniTask<bool> StartSettlementSnapshotAsync(CampaignSnapshot snapshot, CancellationToken cancellationToken)
        {
            if (!campaignRuntime.TryPrepareSettlementRestore(snapshot.Settlement, out IPlayableSettlementRuntime candidate, out string reason))
            {
                bindings.Error?.Invoke(reason);
                return false;
            }
            if (!TryCreateSettlementPayload(candidate, out string payload, out reason))
            {
                campaignRuntime.ReleaseSettlement(candidate);
                bindings.Error?.Invoke(reason);
                return false;
            }
            if (!TryStart(GamePhase.Settlement, false, out reason, candidate, false))
            {
                bindings.Error?.Invoke(reason);
                return false;
            }
            if (!await CompletePublishedSettlementRestoreAsync(candidate.Data, payload, cancellationToken))
            {
                return false;
            }
            startup.ActivateRuntime();
            return true;
        }

        private async UniTask<bool> ReplaceSettlementSnapshotAsync(CampaignSnapshot snapshot, CancellationToken cancellationToken)
        {
            IPlayableSettlementRuntime previousSettlement = SettlementRuntime;
            GamePhase previousPhase = CurrentPhase;
            if (!campaignRuntime.TryPrepareSettlementRestore(snapshot.Settlement, out IPlayableSettlementRuntime candidate, out string reason))
            {
                bindings.Error?.Invoke($"DevLoad: 营地存档投影失败，已保留当前运行态：{reason}");
                return false;
            }
            if (!TryCreateSettlementPayload(candidate, out string payload, out reason))
            {
                campaignRuntime.ReleaseSettlement(candidate);
                bindings.Error?.Invoke($"DevLoad: 营地候选无法生成稳定快照，已保留当前运行态：{reason}");
                return false;
            }
            if (!campaignRuntime.TrySwapSettlement(previousSettlement, candidate, out reason))
            {
                campaignRuntime.ReleaseSettlement(candidate);
                bindings.Error?.Invoke($"DevLoad: 营地候选提交失败，已保留当前运行态：{reason}");
                return false;
            }
            if (!TryMoveRestoreToSettlement(previousPhase, candidate, previousSettlement, out reason))
            {
                bindings.Error?.Invoke(reason);
                return false;
            }
            if (!TryActivateSettlementTarget(out reason))
            {
                bindings.Error?.Invoke(reason);
                RollbackSettlementReplacement(candidate, previousSettlement, previousPhase);
                return false;
            }
            if (previousPhase == GamePhase.Hunt)
            {
                huntPhase.DeactivateCurrentActionSession();
                huntPhase.CleanupCurrentPresentation();
                ReleaseCurrentHuntRuntime();
            }
            else if (previousPhase == GamePhase.BossFight)
                DisposeCombatSession();
            bool restored = await CompletePublishedSettlementRestoreAsync(candidate.Data, payload, cancellationToken);
            if (previousSettlement != null) campaignRuntime.ReleaseSettlement(previousSettlement);
            bindings.Info?.Invoke($"DevLoad 完成，年份 {candidate.Data.CurrentYear}");
            return restored;
        }

        private void RollbackSettlementReplacement(IPlayableSettlementRuntime candidate, IPlayableSettlementRuntime previousSettlement, GamePhase previousPhase)
        {
            if (CurrentPhase != previousPhase)
            {
                try
                {
                    campaignRuntime.TransitionTo(previousPhase);
                }
                catch (Exception exception)
                {
                    if (CurrentPhase != previousPhase) throw new InvalidOperationException("营地恢复提交前失败，且无法恢复来源阶段。", exception);
                }
            }
            if (!campaignRuntime.TrySwapSettlement(candidate, previousSettlement, out string reason)) throw new InvalidOperationException($"营地恢复提交前失败，且无法恢复原运行世代：{reason}");
            campaignRuntime.ReleaseSettlement(candidate);
            if (previousPhase == GamePhase.Settlement && previousSettlement != null)
            {
                if (!settlementPhase.ActivateCurrentActionSession(out reason)) throw new InvalidOperationException($"原营地运行世代恢复后无法重新启动：{reason}");
                settlementPhase.EnsurePresentation(previousSettlement.Manager);
            }
        }

        private bool TryMoveRestoreToSettlement(GamePhase previousPhase, IPlayableSettlementRuntime candidate, IPlayableSettlementRuntime previousSettlement, out string reason)
        {
            reason = string.Empty;
            if (previousPhase == GamePhase.Settlement) return true;
            try
            {
                if (campaignRuntime.TransitionTo(GamePhase.Settlement)) return true;
                campaignRuntime.TrySwapSettlement(candidate, previousSettlement, out _);
                campaignRuntime.ReleaseSettlement(candidate);
                reason = "DevLoad: 无法切换到营地阶段，已恢复原营地管理器。";
                return false;
            }
            catch (Exception exception)
            {
                if (CurrentPhase == GamePhase.Settlement)
                {
                    bindings.Warning?.Invoke($"营地阶段已经切换，但阶段通知存在异常，将继续恢复权威运行态：{exception.Message}");
                    return true;
                }
                campaignRuntime.TrySwapSettlement(candidate, previousSettlement, out _);
                campaignRuntime.ReleaseSettlement(candidate);
                reason = $"DevLoad: 切换到营地阶段时发生异常，已恢复原营地管理器：{exception.Message}";
                return false;
            }
        }

        private async UniTask<bool> CompletePublishedSettlementRestoreAsync(SettlementInstance data, string payload, CancellationToken cancellationToken)
        {
            if (!ReferenceEquals(data, SettlementManager?.Data) || CurrentPhase != GamePhase.Settlement || SettlementActionSession?.IsActive != true) return false;
            ReleaseCurrentHuntRuntime();
            persistenceCoordinator.Adopt(payload);
            SettlementEventRestoreProjection projection = SettlementRuntime.CreateEventRestoreCandidate();
            SettlementRuntime.PublishEventRestore(projection);
            settlementPhase.EnsurePresentation(SettlementManager);
            settlementPhase.Refresh();
            startup.ActivateRuntime();
            if (data.PendingHuntReturn != null && !await ApplyPendingReturnCoreAsync(false, cancellationToken)) return false;
            SettlementEventRestorePlan plan = projection.Prepare();
            if (!plan.Succeeded)
            {
                bindings.Error?.Invoke($"读档后的营地事件恢复失败：{plan.FailureReason}");
                return false;
            }
            QueueSettlementEvents(plan.WorkItems, projection, plan.ChainId);
            return true;
        }

        private bool TryApplyPhaseTransition(GamePhase phase, out string reason)
        {
            if (CurrentPhase == GamePhase.Settlement && phase == GamePhase.Hunt) return Fail("营地出猎必须通过携带路线上下文的 Campaign 请求。", out reason);
            reason = string.Empty;
            if (!CampaignStarted) return Fail("战役入口尚未完成。", out reason);
            if (phase == CurrentPhase) return true;
            if (huntReturn.IsRecoveryInFlight) return Fail("上一场远征的回营保存与年度流程尚未完成", out reason);
            GamePhase previousPhase = CurrentPhase;
            if (previousPhase == GamePhase.Hunt && phase == GamePhase.Settlement && !huntReturn.IsPreparedExit && SettlementData?.PendingHuntReturn == null)
                return Fail("狩猎必须先通过 Hunt Runner 准备回营结算", out reason);
            bool preparedShowdown = false;
            if (phase == GamePhase.BossFight)
            {
                if (!TryPrepareCombatSession(out reason)) return false;
                preparedShowdown = true;
            }
            bool transitioned;
            try
            {
                transitioned = campaignRuntime.TransitionTo(phase);
            }
            catch (Exception exception)
            {
                if (CurrentPhase == phase)
                {
                    transitioned = true;
                    bindings.Warning?.Invoke($"阶段 FSM 已提交到 {phase}，但阶段通知存在异常，将继续完成权威运行态：{exception.Message}");
                }
                else
                {
                    if (preparedShowdown) DisposeCombatSession();
                    throw;
                }
            }
            if (!transitioned)
            {
                if (preparedShowdown) DisposeCombatSession();
                return Fail($"无法从 {previousPhase} 切换到 {phase}", out reason);
            }
            if (preparedShowdown && !TryStartPreparedCombatSession(previousPhase, out reason)) return false;
            if (phase == GamePhase.Settlement && !TryActivateSettlementTarget(out reason))
            {
                if (CurrentPhase == phase && !TryRestorePhase(previousPhase, out string rollbackReason))
                    throw new InvalidOperationException($"营地运行态激活失败，且无法恢复来源阶段：{rollbackReason}");
                return false;
            }
            if (previousPhase == GamePhase.BossFight)
            {
                showdownOutcome.ApplyCommittedLoot();
                DisposeCombatSession();
            }
            if (previousPhase == GamePhase.Settlement) settlementPhase.DeactivateCurrentActionSession();
            if (previousPhase == GamePhase.Hunt)
            {
                if (phase == GamePhase.Settlement && huntReturn.IsPreparedExit && !CommitPreparedHuntExit(out reason))
                {
                    settlementPhase.DeactivateCurrentActionSession();
                    if (!TryRestorePhase(previousPhase, out string rollbackReason))
                        throw new InvalidOperationException($"狩猎退出提交失败，且无法恢复来源阶段：{rollbackReason}");
                    return false;
                }
                huntPhase.DeactivateCurrentActionSession();
            }
            if (phase == GamePhase.Settlement) CompleteSettlementEntry();
            else EnterPhase(phase);
            return true;
        }

        private void EnterPhase(GamePhase phase)
        {
            if (phase == GamePhase.Settlement)
            {
                if (!TryActivateSettlementTarget(out string reason)) throw new InvalidOperationException(reason);
                CompleteSettlementEntry();
            }
            bindings.Info?.Invoke($"进入{phase}阶段");
        }

        private bool TryActivateSettlementTarget(out string reason)
        {
            reason = string.Empty;
            IPlayableSettlementRuntime created = null;
            try
            {
                if (SettlementRuntime == null)
                {
                    if (!campaignRuntime.TryPrepareNewSettlement(out created, out reason)) return false;
                    if (!campaignRuntime.TrySwapSettlement(null, created, out reason))
                    {
                        campaignRuntime.ReleaseSettlement(created);
                        return false;
                    }
                }
                if (!settlementPhase.ActivateCurrentActionSession(out reason))
                {
                    ReleaseUnactivatedSettlement(created);
                    return false;
                }
                settlementPhase.EnsurePresentation(SettlementManager);
                return true;
            }
            catch (Exception exception)
            {
                settlementPhase.DeactivateCurrentActionSession();
                ReleaseUnactivatedSettlement(created);
                reason = $"营地运行态激活异常：{exception.Message}";
                return false;
            }
        }

        private void ReleaseUnactivatedSettlement(IPlayableSettlementRuntime created)
        {
            if (created == null) return;
            if (!campaignRuntime.TrySwapSettlement(created, null, out _)) return;
            campaignRuntime.ReleaseSettlement(created);
        }

        private bool TryRestorePhase(GamePhase phase, out string reason)
        {
            reason = string.Empty;
            try
            {
                if (campaignRuntime.TransitionTo(phase) || CurrentPhase == phase) return true;
                reason = $"无法恢复到 {phase}";
                return false;
            }
            catch (Exception exception)
            {
                if (CurrentPhase == phase) return true;
                reason = exception.Message;
                return false;
            }
        }

        private void CompleteSettlementEntry()
        {
            settlementPhase.EnsurePresentation(SettlementManager);
            HuntRecord record = SettlementManager.Data.PendingHuntReturn;
            if (record != null) ApplyPendingReturnCoreAsync(true, ResolveLifetimeToken()).Forget();
            else
            {
                QueueSettlementEvents(SettlementManager.OnEnterWorkItems());
                SaveCampaignAsync(false, ResolveLifetimeToken()).Forget();
            }
        }

        private bool CommitPreparedHuntExit(out string reason)
        {
            IPlayableHuntRuntime current = HuntRuntime;
            if (current == null) return Fail("已准备的狩猎退出缺少当前运行态。", out reason);
            if (!campaignRuntime.TrySwapHunt(current, null, out reason)) return false;
            huntReturn.CompletePreparedExit();
            campaignRuntime.ReleaseHunt(current);
            return true;
        }

        private void ReleaseCurrentHuntRuntime()
        {
            IPlayableHuntRuntime current = HuntRuntime;
            if (current == null) return;
            if (!campaignRuntime.TrySwapHunt(current, null, out string reason)) throw new InvalidOperationException(reason);
            campaignRuntime.ReleaseHunt(current);
        }

        private bool TryPrepareCombatSession(out string reason)
        {
            reason = string.Empty;
            if (showdownPhase.Current?.IsActive == true) return true;
            if (bindings.TryCreateCombatConfiguration == null || !bindings.TryCreateCombatConfiguration(out PlayableCombatSessionConfiguration configuration, out reason)) return false;
            if (showdownPhase.TryPrepare(configuration, out reason)) return true;
            bindings.Error?.Invoke(reason);
            return false;
        }

        private bool TryStartPreparedCombatSession(GamePhase previousPhase, out string reason)
        {
            try
            {
                StartPreparedCombatSession();
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                DisposeCombatSession();
                if (CurrentPhase == GamePhase.BossFight && !campaignRuntime.TransitionTo(previousPhase)) throw new InvalidOperationException("决战启动失败，且无法恢复来源阶段。", exception);
                reason = $"决战运行态启动异常：{exception.Message}";
                return false;
            }
        }

        private void StartPreparedCombatSession()
        {
            IReadOnlyList<HunterInstance> hunters = encounterHandoff.ConsumePendingHunters(HuntManager?.ActiveHunters) ?? HuntManager?.ActiveHunters;
            showdownPhase.Start(hunters, SettlementManager?.HunterMgmt, () => showdownOutcome.CompleteDefeatedHuntAfterActionAsync().Forget());
        }

        private void DisposeCombatSession() => showdownPhase.DisposeCurrent();

        private void HandleHuntCompleted(HuntRecord record)
        {
            if (SettlementManager?.HunterMgmt == null) throw new InvalidOperationException("营地猎人管理器未初始化，无法提交狩猎成长。");
            PlayableHunterAdvancementAdapter.ApplyAfterHunt(HuntManager.ActiveHunters, SettlementManager.HunterMgmt);
            SettlementManager.Data.PendingHuntReturn = record;
            TransitionToPhase(GamePhase.Settlement);
        }

        private void OnHuntCheckpointCommitted(IPlayableHuntRuntime source)
        {
            if (!ReferenceEquals(HuntRuntime, source)) return;
            if (CurrentPhase != GamePhase.Hunt || HuntActionSession?.IsActive != true || HuntActionSession.IsRunning) return;
            SaveCampaignAsync(true, ResolveLifetimeToken()).Forget();
        }

        private async UniTask<bool> RetryPendingHuntReturnAsync()
        {
            SettlementHuntReturnCommandResult result = await huntReturn.ApplyPendingAsync(true, ResolveLifetimeToken());
            return result.Succeeded;
        }

        private bool CanDepartAfterSettlementEventRestore(out string reason)
        {
            if (settlementPhase.IsEventRestoreReady)
            {
                reason = string.Empty;
                return true;
            }
            reason = settlementPhase.EventRestoreFailureReason;
            if (string.IsNullOrWhiteSpace(reason)) reason = "请先完成读档后的营地事件恢复。";
            return false;
        }

        private void EnsureCampaignShell()
            => campaignRuntime.EnsureGameplayRuntime(new InventionActionEffectInstaller(() => SettlementManager?.Data, () => SettlementManager?.Inventions?.AllInventions));

        private static CampaignRestartPayload PrepareCampaignRestartPayload(IPlayableSettlementRuntime settlement)
        {
            settlement.Manager.EnsureStartingConditions();
            CampaignSnapshot snapshot = ActiveHuntSnapshotAdapter.CaptureSettlement(settlement.Data);
            return SaveLoadSystem.TryCreatePayload(snapshot, out string payload, out string reason) ? CampaignRestartPayload.Success(payload) : CampaignRestartPayload.Failed(reason);
        }

        private static bool TryCreateSettlementPayload(IPlayableSettlementRuntime settlement, out string payload, out string reason)
            => SaveLoadSystem.TryCreatePayload(ActiveHuntSnapshotAdapter.CaptureSettlement(settlement.Data), out payload, out reason);

        private CancellationToken ResolveLifetimeToken() => bindings.ResolveLifetimeToken?.Invoke() ?? default;

        private bool IsCurrentDevLoad(int generation) => !disposed && generation == devLoadGeneration;

        private void InvalidateDevLoad()
        {
            ++devLoadGeneration;
            devLoadInFlight = false;
        }

        GamePhase ICampaignPhaseTransitionHost.CurrentPhase => CurrentPhase;
        bool ICampaignPhaseTransitionHost.TryApplyPhaseTransition(GamePhase targetPhase, out string reason)
            => TryApplyPhaseTransition(targetPhase, out reason);
        bool ICampaignPhaseTransitionHost.TryBeginEncounter(CampaignEncounterRequest request, out string reason)
            => encounterHandoff.TryBegin(request, out reason);

        UniTask<CampaignRestartResult> ICampaignRestartHost.RestartCampaignFromActionAsync(CancellationToken cancellationToken) => RestartFromActionAsync(cancellationToken);

        bool ICampaignPhaseTransitionRequestHost.TryApplyPhaseTransition(CampaignPhaseTransitionRequest request, out string reason)
        {
            if (!request.IsValid)
                return Fail("狩猎阶段切换缺少有效路线上下文。", out reason);
            if (request.TargetPhase != GamePhase.Hunt)
                return ((ICampaignPhaseTransitionHost)this).TryApplyPhaseTransition(request.TargetPhase, out reason);
            if (!request.HasHuntContext)
                return Fail("进入狩猎阶段必须携带已准备的路线上下文。", out reason);
            if (CurrentPhase != GamePhase.Settlement)
                return Fail("只有营地阶段可以提交狩猎入场请求。", out reason);
            return huntDeparture.TryCommitHuntEntry(request.HuntContext, out reason);
        }

        bool ICampaignStartupTransactionHost.TryStartCampaignRuntime(GamePhase startPhase, bool queueSettlementEvents, out string reason, IPlayableSettlementRuntime preparedSettlement, bool activateOnSuccess) => TryStart(startPhase, queueSettlementEvents, out reason, preparedSettlement, activateOnSuccess);
        void ICampaignStartupTransactionHost.ResetFailedCampaignStartupRuntime() => ResetFailedStartup();
        UniTask<bool> ICampaignStartupTransactionHost.RestoreSnapshotAsync(CampaignSnapshot snapshot, CancellationToken cancellationToken) => RestoreSnapshotAsync(snapshot, false, cancellationToken);

        GamePhase ICampaignHuntReturnHost.CurrentPhase => CurrentPhase;
        IPlayableHuntRuntime ICampaignHuntReturnHost.HuntRuntime => HuntRuntime;
        IPlayableSettlementRuntime ICampaignHuntReturnHost.SettlementRuntime => SettlementRuntime;
        PlayableHuntActionSession ICampaignHuntReturnHost.HuntActionSession => HuntActionSession;
        PlayableSettlementActionSession ICampaignHuntReturnHost.SettlementActionSession => SettlementActionSession;
        UniTask<bool> ICampaignHuntReturnHost.SaveCampaignAsync(bool includeActiveHunt, CancellationToken cancellationToken) => SaveCampaignAsync(includeActiveHunt, cancellationToken);
        UniTask<CampaignPhaseTransitionResult> ICampaignHuntReturnHost.TransitionToSettlementAsync() => campaignRuntime.TransitionAsync(GamePhase.Settlement);
        SettlementEventRestoreProjection ICampaignHuntReturnHost.CreateEventRestoreCandidate() => SettlementRuntime?.CreateEventRestoreCandidate();
        void ICampaignHuntReturnHost.PublishEventRestore(SettlementEventRestoreProjection projection) => SettlementRuntime?.PublishEventRestore(projection);
        bool ICampaignHuntReturnHost.TryClearAppliedReturnCheckpoint(SettlementInstance settlement, HuntRecord record, out string reason) => PlayableCampaignLoopContract.TryClearAppliedReturnCheckpoint(settlement, record, out reason);
        UniTask<bool> ICampaignHuntReturnHost.ResolveSettlementEventsAsync(IPlayableSettlementRuntime runtime, PlayableSettlementActionSession session, SettlementEventRestorePlan plan, SettlementEventRestoreProjection projection) => settlementPhase.ResolveEventsAsync(runtime, session, plan.WorkItems, projection, plan.ChainId);

        bool ICampaignHuntDepartureHost.CampaignStarted => CampaignStarted;
        GamePhase ICampaignHuntDepartureHost.CurrentPhase => CurrentPhase;
        IPlayableCampaignRuntime ICampaignHuntDepartureHost.CampaignRuntime => campaignRuntime;
        IPlayableSettlementRuntime ICampaignHuntDepartureHost.SettlementRuntime => SettlementRuntime;
        IPlayableHuntRuntime ICampaignHuntDepartureHost.HuntRuntime => HuntRuntime;
        IPlayableHuntPhasePort ICampaignHuntDepartureHost.HuntPhase => huntPhase;
        PlayableSettlementActionSession ICampaignHuntDepartureHost.SettlementActionSession => SettlementActionSession;
        IPlayableEventInput ICampaignHuntDepartureHost.EventInput => playableEventInput;
        bool ICampaignHuntDepartureHost.IsHuntReturnRecoveryInFlight => IsHuntReturnRecoveryInFlight;
        bool ICampaignHuntDepartureHost.TryCanDepartAfterEventRestore(out string reason) => CanDepartAfterSettlementEventRestore(out reason);
        UniTask<CampaignPhaseTransitionResult> ICampaignHuntDepartureHost.RequestHuntTransitionAsync(CampaignHuntEntryContext context, CancellationToken cancellationToken) => campaignRuntime.TransitionAsync(CampaignPhaseTransitionRequest.ForHunt(context), cancellationToken);
        void ICampaignHuntDepartureHost.PublishHuntDeparted(IReadOnlyList<int> hunterIds) => EventBus.Publish(new HuntDepartedEvent { HunterIds = hunterIds.ToArray() });
        void ICampaignHuntDepartureHost.ClearDepartureBlockedNotice() => bindings.ClearDepartureBlockedNotice?.Invoke();
        void ICampaignHuntDepartureHost.CommitHuntCheckpoint(IPlayableHuntRuntime runtime) => OnHuntCheckpointCommitted(runtime);

        GamePhase ICampaignShowdownOutcomeHost.CurrentPhase => CurrentPhase;
        PlayableCombatSession ICampaignShowdownOutcomeHost.ShowdownSession => showdownPhase?.Current;
        HuntManager ICampaignShowdownOutcomeHost.HuntManager => HuntManager;
        SettlementInstance ICampaignShowdownOutcomeHost.SettlementData => SettlementManager?.Data;
        void ICampaignShowdownOutcomeHost.ApplyBossFightLoot() => ApplyBossFightLoot();
        void ICampaignShowdownOutcomeHost.RequestSettlementTransition() => TransitionToPhase(GamePhase.Settlement);

        GamePhase ICampaignEncounterHandoffHost.CurrentPhase => CurrentPhase;
        IPlayableCampaignRuntime ICampaignEncounterHandoffHost.CampaignRuntime => campaignRuntime;
        IPlayableHuntRuntime ICampaignEncounterHandoffHost.HuntRuntime => HuntRuntime;
        PlayableHuntActionSession ICampaignEncounterHandoffHost.HuntActionSession => HuntActionSession;
        PlayableSettlementActionSession ICampaignEncounterHandoffHost.SettlementActionSession => SettlementActionSession;
        SettlementManager ICampaignEncounterHandoffHost.SettlementManager => SettlementManager;
        HuntManager ICampaignEncounterHandoffHost.HuntManager => HuntManager;
        CampaignPersistenceCoordinator ICampaignEncounterHandoffHost.Persistence => persistenceCoordinator;
        bool ICampaignEncounterHandoffHost.TryApplyBossFightTransition(out string reason) => ((ICampaignPhaseTransitionHost)this).TryApplyPhaseTransition(GamePhase.BossFight, out reason);
        UniTask<CampaignEncounterStartResult> ICampaignEncounterHandoffHost.RunEncounterActionAsync(CampaignEncounterRequest request, CancellationToken cancellationToken) => campaignRuntime.BeginEncounterAsync(request, cancellationToken);

        private void ApplyBossFightLoot()
        {
            if (SettlementManager == null || showdownPhase?.Current == null) return;
            var loot = showdownPhase.Current.GetAndClearLoot();
            if (loot.Count == 0) return;
            foreach ((string resource, int amount) in loot)
            {
                string resourceId = PlayableSettlementItemRegistry.ResolveContentId(resource);
                int oldAmount = SettlementManager.Data.GetResource(resourceId);
                SettlementManager.Data.AddResource(resourceId, amount);
                if (SettlementManager.Data.PendingHuntReturn != null)
                    for (int i = 0; i < amount; i++) SettlementManager.Data.PendingHuntReturn.CollectedResources.Add(resourceId);
                EventBus.Publish(new ResourceChangedEvent { ResourceName = resourceId, OldAmount = oldAmount, NewAmount = SettlementManager.Data.GetResource(resourceId) });
            }
        }
    }
}
