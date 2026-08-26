using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Hunt;

namespace Core
{
    internal interface ICampaignHuntDepartureHost
    {
        bool CampaignStarted { get; }
        GamePhase CurrentPhase { get; }
        IPlayableCampaignRuntime CampaignRuntime { get; }
        IPlayableSettlementRuntime SettlementRuntime { get; }
        IPlayableHuntRuntime HuntRuntime { get; }
        IPlayableHuntPhasePort HuntPhase { get; }
        PlayableSettlementActionSession SettlementActionSession { get; }
        IPlayableEventInput EventInput { get; }
        bool IsHuntReturnRecoveryInFlight { get; }
        bool TryCanDepartAfterEventRestore(out string reason);
        UniTask<CampaignPhaseTransitionResult> RequestHuntTransitionAsync(CampaignHuntEntryContext context, CancellationToken cancellationToken);
        void PublishHuntDeparted(IReadOnlyList<int> hunterIds);
        void ClearDepartureBlockedNotice();
        void CommitHuntCheckpoint(IPlayableHuntRuntime runtime);
    }

    internal sealed class CampaignHuntDepartureTransaction
    {
        private readonly ICampaignHuntDepartureHost host;
        private int operationSequence;
        private bool inFlight;
        private bool entryCommitted;
        private bool departureEventPublished;
        private IPlayableSettlementRuntime sourceSettlement;
        private PlayableSettlementActionSession sourceSession;
        private CampaignHuntEntryContext activeContext;
        private SettlementDepartureCommandResult preparedDeparture;

        internal CampaignHuntDepartureTransaction(ICampaignHuntDepartureHost host)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
        }

        internal bool IsInFlight => inFlight;

        internal bool CanRequest(out string reason)
        {
            if (!host.CampaignStarted)
            {
                reason = "战役入口尚未完成。";
                return false;
            }
            if (host.IsHuntReturnRecoveryInFlight || host.SettlementRuntime?.Data?.PendingHuntReturn != null)
            {
                reason = "请先完成上一场远征的回营结算，再重新发起出猎。";
                return false;
            }
            if (inFlight)
            {
                reason = "出猎流程正在处理中。";
                return false;
            }
            PlayableSettlementActionSession session = host.SettlementActionSession;
            if (host.CurrentPhase != GamePhase.Settlement || session?.IsActive != true)
            {
                reason = "当前不在营地阶段。";
                return false;
            }
            if (!host.TryCanDepartAfterEventRestore(out reason)) return false;
            if (session.IsRunning)
            {
                reason = "请先完成当前营地流程。";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        internal bool TryRequest(IReadOnlyList<int> hunterIds, CancellationToken cancellationToken)
        {
            if (!CanRequest(out _)) return false;
            if (!DepartureRules.CanDepart(hunterIds, out _)) return false;
            SettlementInstance settlement = host.SettlementRuntime?.Data;
            if (settlement == null || !PlayableHuntDestinationRuntime.CanSelectForDeparture(null, settlement.CurrentYear, out _)) return false;
            ExecuteAsync(hunterIds, null, cancellationToken).Forget();
            return true;
        }

        internal async UniTask<SettlementDepartureCommandResult> ExecuteAsync(IReadOnlyList<int> hunterIds, PlayableHuntDestination destination, CancellationToken cancellationToken)
        {
            if (!CanRequest(out string gateReason)) return SettlementDepartureCommandResult.Failed(gateReason);

            IPlayableSettlementRuntime settlement = host.SettlementRuntime;
            PlayableSettlementActionSession session = host.SettlementActionSession;
            SettlementInstance data = settlement?.Data;
            if (data == null) return SettlementDepartureCommandResult.Failed("营地权威数据尚未准备完成。");
            if (!PlayableHuntDestinationRuntime.TryResolveRouteForDeparture(destination, data.CurrentYear, out PlayableHuntRoutePlan routePlan, out string routeReason))
                return SettlementDepartureCommandResult.Failed(routeReason);

            int sequence = ++operationSequence;
            inFlight = true;
            entryCommitted = false;
            departureEventPublished = false;
            sourceSettlement = settlement;
            sourceSession = session;
            activeContext = default;
            preparedDeparture = default;
            try
            {
                SettlementDepartureCommandResult departure = await session.PrepareDepartureAsync(hunterIds, cancellationToken);
                if (!departure.Succeeded) return departure;
                if (!IsCurrent(sequence, settlement, session)) return SettlementDepartureCommandResult.Failed("出猎准备期间权威营地世代已经变化，请重新发起出猎。");

                var context = new CampaignHuntEntryContext(routePlan, data.CurrentYear, data.RuntimeDeparturePreparationToken);
                if (!context.IsValid) return SettlementDepartureCommandResult.Failed("营地出猎准备未生成有效的路线上下文。");
                activeContext = context;
                preparedDeparture = departure;

                CampaignPhaseTransitionResult transition = await host.RequestHuntTransitionAsync(context, cancellationToken);
                if (sequence != operationSequence) return SettlementDepartureCommandResult.Failed("出猎切换期间战役运行世代已经变化。");
                if (!entryCommitted) return SettlementDepartureCommandResult.Failed(transition.Succeeded ? "狩猎入场事务未完成权威提交。" : transition.Reason);

                PublishCommittedDeparture();
                return departure;
            }
            catch (OperationCanceledException)
            {
                if (sequence == operationSequence && entryCommitted)
                {
                    PublishCommittedDeparture();
                    return preparedDeparture;
                }
                return SettlementDepartureCommandResult.Failed("出猎流程已取消。");
            }
            catch (Exception)
            {
                if (sequence == operationSequence && entryCommitted)
                {
                    PublishCommittedDeparture();
                    return preparedDeparture;
                }
                return SettlementDepartureCommandResult.Failed("出猎流程失败，请留在营地重试。");
            }
            finally
            {
                if (sequence == operationSequence) ClearOperation();
            }
        }

        internal bool TryCommitHuntEntry(CampaignHuntEntryContext context, out string reason)
        {
            reason = string.Empty;
            if (!inFlight || sourceSettlement == null || sourceSession == null)
            {
                reason = "狩猎入场请求不属于当前出猎事务。";
                return false;
            }
            if (!MatchesActiveContext(context))
            {
                reason = "狩猎入场请求与当前出猎准备不匹配。";
                return false;
            }
            if (!ReferenceEquals(host.SettlementRuntime, sourceSettlement) || !ReferenceEquals(host.SettlementActionSession, sourceSession))
            {
                reason = "狩猎入场前权威营地世代已经变化。";
                return false;
            }
            if (host.CurrentPhase != GamePhase.Settlement)
            {
                reason = "只有营地阶段可以提交狩猎入场请求。";
                return false;
            }
            if (host.IsHuntReturnRecoveryInFlight || sourceSettlement.Data.PendingHuntReturn != null)
            {
                reason = "上一场远征的回营结算尚未完成。";
                return false;
            }
            if (!host.TryCanDepartAfterEventRestore(out reason)) return false;
            if (sourceSession.IsRunning)
            {
                reason = "营地流程尚未完成。";
                return false;
            }

            GamePhase previousPhase = host.CurrentPhase;
            SettlementInstance expectedSettlement = sourceSettlement.Data;
            if (!TryValidateHuntEntryContext(context, expectedSettlement, out reason)) return false;
            bool entered = PlayableCampaignLoopContract.TryEnterHunt(expectedSettlement,
                () => host.CampaignRuntime.TransitionTo(GamePhase.Hunt),
                roster => TryPublishHuntRuntime(roster, false, context, expectedSettlement, out string entryReason) ? CampaignHuntEntryResult.Success() : CampaignHuntEntryResult.Failed(entryReason),
                () =>
                {
                    host.HuntPhase.DeactivateCurrentActionSession();
                    if (host.CampaignRuntime.CurrentPhase == GamePhase.Hunt)
                        host.CampaignRuntime.TransitionTo(previousPhase);
                },
                out reason);
            if (!entered) return false;

            entryCommitted = true;
            sourceSettlement.DeactivateActionSession();
            return true;
        }

        internal bool TryStartDevelopmentHunt(out string reason)
            => TryPublishHuntRuntime(null, true, default, null, out reason);

        internal void Reset()
        {
            operationSequence++;
            ClearOperation();
        }

        private bool TryPublishHuntRuntime(IReadOnlyList<HunterInstance> committedRoster, bool allowDevelopmentFallback, CampaignHuntEntryContext context, SettlementInstance expectedSettlement, out string reason)
        {
            if (context.IsValid && host.CurrentPhase != GamePhase.Hunt)
            {
                reason = "阶段状态机未保持在狩猎阶段。";
                return false;
            }
            if (context.IsValid && !TryValidateHuntEntryContext(context, expectedSettlement, out reason)) return false;

            SettlementInstance settlementData = host.SettlementRuntime?.Data;
            List<HunterInstance> hunters = committedRoster != null ? new List<HunterInstance>(committedRoster) : null;
            if (hunters == null && !PlayableCampaignLoopContract.TryResolveDepartureRoster(settlementData, out hunters, out string rosterReason))
            {
                if (!allowDevelopmentFallback)
                {
                    reason = rosterReason;
                    return false;
                }
                if (!PlayableCampaignLoopContract.TryResolveDevelopmentRoster(settlementData, out hunters, out reason)) return false;
            }

            PlayableHuntDestinationRuntime.RuntimeState previousDestinationState = default;
            bool routeCommitted = false;
            if (context.IsValid) previousDestinationState = PlayableHuntDestinationRuntime.CaptureState();
            IPlayableHuntRuntime previousHunt = host.HuntRuntime;
            IPlayableHuntRuntime candidateHunt = null;
            try
            {
                var startPlan = new PlayableHuntStartPlan(hunters, settlementData?.CurrentYear ?? 1, context.IsValid ? context.RoutePlan : null, host.EventInput);
                if (!host.HuntPhase.TryPrepareInitialized(host.SettlementRuntime, startPlan, out candidateHunt, out reason)) return false;
                if (context.IsValid && host.CurrentPhase != GamePhase.Hunt)
                {
                    reason = "阶段状态机未保持在狩猎阶段。";
                    return ReleaseFailedHuntCandidate(candidateHunt);
                }
                if (context.IsValid && !TryValidateHuntEntryContext(context, expectedSettlement, out reason))
                    return ReleaseFailedHuntCandidate(candidateHunt);
                if (context.IsValid && !PlayableHuntDestinationRuntime.TryCommitRoute(context.RoutePlan, out reason))
                    return ReleaseFailedHuntCandidate(candidateHunt);
                routeCommitted = context.IsValid;
                if (!host.CampaignRuntime.TrySwapHunt(previousHunt, candidateHunt, out reason))
                {
                    if (routeCommitted) PlayableHuntDestinationRuntime.RestoreState(previousDestinationState);
                    return ReleaseFailedHuntCandidate(candidateHunt);
                }
                if (!host.HuntPhase.TryStartCurrentPresentationAndSession(null, out reason))
                {
                    if (routeCommitted) PlayableHuntDestinationRuntime.RestoreState(previousDestinationState);
                    host.HuntPhase.CleanupCurrentPresentation();
                    host.CampaignRuntime.TrySwapHunt(candidateHunt, previousHunt, out _);
                    host.CampaignRuntime.ReleaseHunt(candidateHunt);
                    return false;
                }
                if (previousHunt != null) host.CampaignRuntime.ReleaseHunt(previousHunt);
                host.CommitHuntCheckpoint(candidateHunt);
                return true;
            }
            catch (Exception exception)
            {
                if (routeCommitted) PlayableHuntDestinationRuntime.RestoreState(previousDestinationState);
                if (ReferenceEquals(host.HuntRuntime, candidateHunt)) host.HuntPhase.CleanupCurrentPresentation();
                if (ReferenceEquals(host.HuntRuntime, candidateHunt)) host.CampaignRuntime.TrySwapHunt(candidateHunt, previousHunt, out _);
                if (candidateHunt != null && !ReferenceEquals(host.HuntRuntime, candidateHunt)) host.CampaignRuntime.ReleaseHunt(candidateHunt);
                reason = $"狩猎运行环境初始化失败：{exception.Message}";
                return false;
            }
        }

        private bool TryValidateHuntEntryContext(CampaignHuntEntryContext context, SettlementInstance expectedSettlement, out string reason)
        {
            if (!context.IsValid)
            {
                reason = "狩猎入场上下文无效。";
                return false;
            }
            if (expectedSettlement == null || !ReferenceEquals(expectedSettlement, host.SettlementRuntime?.Data))
            {
                reason = "营地运行世代已变化，拒绝提交过期狩猎入场请求。";
                return false;
            }
            if (context.Year != expectedSettlement.CurrentYear)
            {
                reason = "狩猎入场年份已过期。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(expectedSettlement.RuntimeDeparturePreparationToken) || !string.Equals(context.DeparturePreparationToken, expectedSettlement.RuntimeDeparturePreparationToken, StringComparison.Ordinal))
            {
                reason = "狩猎入场准备令牌已过期。";
                return false;
            }
            if (!string.Equals(expectedSettlement.DeparturePreparationToken, expectedSettlement.RuntimeDeparturePreparationToken, StringComparison.Ordinal))
            {
                reason = "狩猎入场持久准备令牌与当前运行世代不匹配。";
                return false;
            }
            if (!PlayableHuntDestinationRuntime.TryResolveRouteForDeparture(context.RoutePlan.Destination, context.Year, out PlayableHuntRoutePlan resolvedRoute, out reason) || !ReferenceEquals(resolvedRoute, context.RoutePlan) || !ReferenceEquals(context.RoutePlan.Owner, PlayableHuntContentRuntime.CurrentBundle))
            {
                reason = string.IsNullOrWhiteSpace(reason) ? "狩猎路线已失效或不属于当前内容 Bundle。" : reason;
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private bool MatchesActiveContext(CampaignHuntEntryContext context)
            => context.IsValid && activeContext.IsValid && ReferenceEquals(context.RoutePlan, activeContext.RoutePlan) && context.Year == activeContext.Year && string.Equals(context.DeparturePreparationToken, activeContext.DeparturePreparationToken, StringComparison.Ordinal);

        private bool IsCurrent(int sequence, IPlayableSettlementRuntime settlement, PlayableSettlementActionSession session)
            => sequence == operationSequence && ReferenceEquals(host.SettlementRuntime, settlement) && ReferenceEquals(host.SettlementActionSession, session);

        private bool ReleaseFailedHuntCandidate(IPlayableHuntRuntime candidate)
        {
            host.CampaignRuntime.ReleaseHunt(candidate);
            return false;
        }

        private void PublishCommittedDeparture()
        {
            if (departureEventPublished || !entryCommitted || !preparedDeparture.Succeeded) return;
            departureEventPublished = true;
            host.PublishHuntDeparted(preparedDeparture.HunterIds);
            host.ClearDepartureBlockedNotice();
        }

        private void ClearOperation()
        {
            inFlight = false;
            entryCommitted = false;
            departureEventPublished = false;
            sourceSettlement = null;
            sourceSession = null;
            activeContext = default;
            preparedDeparture = default;
        }
    }
}
