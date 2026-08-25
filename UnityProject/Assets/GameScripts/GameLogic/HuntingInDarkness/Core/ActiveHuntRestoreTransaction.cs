using System;
using GameplayBase;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;

namespace Core
{
    internal readonly struct ActiveHuntRestoreResult
    {
        private ActiveHuntRestoreResult(bool succeeded, string stablePayload, string reason)
        {
            Succeeded = succeeded;
            StablePayload = stablePayload ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string StablePayload { get; }
        public string Reason { get; }
        public static ActiveHuntRestoreResult Success(string stablePayload) => new(true, stablePayload, string.Empty);
        public static ActiveHuntRestoreResult Failed(string reason, string stablePayload = "") => new(false, stablePayload, reason);
    }

    internal delegate bool TryActivateRestoredHunt(PlayableHuntEventOccurrenceStore occurrences, out string reason);

    /// <summary>原子准备、发布或回滚活动狩猎运行世代；场景表现通过组合根回调激活。</summary>
    internal sealed class ActiveHuntRestoreTransaction
    {
        private readonly IPlayableCampaignRuntime runtime;
        private readonly Func<IPlayableEventInput> eventInputProvider;
        private readonly TryActivateRestoredHunt activateHunt;
        private readonly Action deactivateHunt;
        private readonly Action cleanupHuntPresentation;
        private readonly Action<GamePhase, IPlayableHuntRuntime> restorePreviousPresentation;
        private readonly Action<string> reportWarning;

        public ActiveHuntRestoreTransaction(IPlayableCampaignRuntime runtime, Func<IPlayableEventInput> eventInputProvider, TryActivateRestoredHunt activateHunt, Action deactivateHunt, Action cleanupHuntPresentation, Action<GamePhase, IPlayableHuntRuntime> restorePreviousPresentation, Action<string> reportWarning = null)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.eventInputProvider = eventInputProvider ?? throw new ArgumentNullException(nameof(eventInputProvider));
            this.activateHunt = activateHunt ?? throw new ArgumentNullException(nameof(activateHunt));
            this.deactivateHunt = deactivateHunt ?? throw new ArgumentNullException(nameof(deactivateHunt));
            this.cleanupHuntPresentation = cleanupHuntPresentation ?? throw new ArgumentNullException(nameof(cleanupHuntPresentation));
            this.restorePreviousPresentation = restorePreviousPresentation ?? throw new ArgumentNullException(nameof(restorePreviousPresentation));
            this.reportWarning = reportWarning;
        }

        public ActiveHuntRestoreResult Execute(CampaignSnapshot campaign)
        {
            bool initialStartup = !runtime.IsStarted;
            ActiveHuntSnapshot active = campaign?.ActiveHunt;
            if (active == null) return ActiveHuntRestoreResult.Failed("存档不包含活动狩猎快照。");
            if (active.EncounterHandoffPending) return ActiveHuntRestoreResult.Failed($"存档停留在尚未支持恢复的遭遇交接：{active.EncounterId}");

            PlayableHuntDestinationRuntime.RuntimeState previousDestinationState = PlayableHuntDestinationRuntime.CaptureState();
            if (!PlayableHuntDestinationRuntime.TryResolveRouteForRestore(active.DestinationId, active.Year, active.ContentBundleId, out PlayableHuntRoutePlan restoredRoute, out string reason))
                return ActiveHuntRestoreResult.Failed(reason);

            IPlayableSettlementRuntime previousSettlement = runtime.Settlement;
            IPlayableHuntRuntime previousHunt = runtime.Hunt;
            GamePhase previousPhase = runtime.CurrentPhase;
            if (!runtime.TryPrepareSettlementRestore(campaign.Settlement, out IPlayableSettlementRuntime candidateSettlement, out reason))
            {
                PlayableHuntDestinationRuntime.RestoreState(previousDestinationState);
                return ActiveHuntRestoreResult.Failed(reason);
            }

            CampaignSnapshot candidateCampaign = CreateCandidateCampaign(campaign, candidateSettlement);
            if (!runtime.TryPrepareHuntRestore(candidateSettlement, active.ExpeditionId, out IPlayableHuntRuntime candidateHunt, out reason))
                return RejectPreparedSettlement(candidateSettlement, previousDestinationState, reason);

            if (!TryPrepareHunt(candidateCampaign, candidateHunt, restoredRoute, out PlayableHuntEventOccurrenceStore restoredOccurrences, out string candidatePayload, out reason))
                return RejectCandidates(candidateSettlement, candidateHunt, previousDestinationState, reason);
            if (!PlayableHuntDestinationRuntime.TryCommitRoute(restoredRoute, out reason))
                return RejectCandidates(candidateSettlement, candidateHunt, previousDestinationState, reason);

            PlayableHuntDestinationRuntime.RuntimeState candidateDestinationState = PlayableHuntDestinationRuntime.CaptureState();
            if (!runtime.TrySwapSettlement(previousSettlement, candidateSettlement, out reason))
                return RejectCandidates(candidateSettlement, candidateHunt, previousDestinationState, reason);
            if (!runtime.TrySwapHunt(previousHunt, candidateHunt, out reason))
            {
                runtime.TrySwapSettlement(candidateSettlement, previousSettlement, out _);
                return RejectCandidates(candidateSettlement, candidateHunt, previousDestinationState, reason);
            }

            candidateSettlement.PublishEventRestore(candidateSettlement.CreateEventRestoreCandidate());
            if (!TryEnterHuntPhase(initialStartup, candidateSettlement, previousSettlement, candidateHunt, previousHunt, previousDestinationState, out reason))
                return ActiveHuntRestoreResult.Failed(reason);

            if (activateHunt(restoredOccurrences, out reason))
            {
                ReleaseRetiredRuntime(previousSettlement, previousHunt);
                return ActiveHuntRestoreResult.Success(candidatePayload);
            }

            deactivateHunt();
            if (initialStartup)
            {
                runtime.TrySwapHunt(candidateHunt, null, out _);
                runtime.TrySwapSettlement(candidateSettlement, null, out _);
                runtime.ReleaseHunt(candidateHunt);
                runtime.ReleaseSettlement(candidateSettlement);
                runtime.Reset();
                cleanupHuntPresentation();
                return ActiveHuntRestoreResult.Failed(reason);
            }

            if (!TryRollbackPhase(previousPhase, out string rollbackReason))
            {
                if (previousSettlement != null)
                    runtime.ReleaseSettlement(previousSettlement);
                if (previousHunt != null)
                    runtime.ReleaseHunt(previousHunt);
                PlayableHuntDestinationRuntime.RestoreState(candidateDestinationState);
                return ActiveHuntRestoreResult.Failed($"{reason}；阶段回滚失败：{rollbackReason}", candidatePayload);
            }

            RestoreGenerations(candidateSettlement, previousSettlement, candidateHunt, previousHunt, previousDestinationState);
            runtime.ReleaseHunt(candidateHunt);
            runtime.ReleaseSettlement(candidateSettlement);
            restorePreviousPresentation(previousPhase, previousHunt);
            return ActiveHuntRestoreResult.Failed(reason);
        }

        private bool TryPrepareHunt(CampaignSnapshot candidateCampaign, IPlayableHuntRuntime candidateHunt, PlayableHuntRoutePlan restoredRoute, out PlayableHuntEventOccurrenceStore restoredOccurrences, out string candidatePayload, out string reason)
        {
            restoredOccurrences = null;
            candidatePayload = string.Empty;
            HuntManager candidateManager = candidateHunt.Manager;
            if (!candidateManager.TryBindContent(restoredRoute, out reason)) return false;
            candidateManager.EventInput = eventInputProvider();
            if (!ActiveHuntSnapshotAdapter.TryRestore(candidateCampaign, candidateManager, out PlayableHuntRuntimeState runtimeState, out restoredOccurrences, out reason)) return false;
            if (!candidateManager.TryRestore(runtimeState, out reason)) return false;
            return SaveLoadSystem.TryCreatePayload(candidateCampaign, out candidatePayload, out reason);
        }

        private bool TryEnterHuntPhase(bool initialStartup, IPlayableSettlementRuntime candidateSettlement, IPlayableSettlementRuntime previousSettlement, IPlayableHuntRuntime candidateHunt, IPlayableHuntRuntime previousHunt, PlayableHuntDestinationRuntime.RuntimeState previousDestinationState, out string reason)
        {
            reason = string.Empty;
            try
            {
                if (initialStartup)
                    runtime.Start(GamePhase.Hunt);
                else if (runtime.CurrentPhase != GamePhase.Hunt && !runtime.TransitionTo(GamePhase.Hunt))
                {
                    reason = "无法切换到活动狩猎恢复阶段。";
                    RollbackPublishedCandidates(candidateSettlement, previousSettlement, candidateHunt, previousHunt, previousDestinationState);
                    return false;
                }
            }
            catch (Exception exception)
            {
                if (runtime.CurrentPhase != GamePhase.Hunt)
                {
                    reason = $"切换到活动狩猎恢复阶段时发生异常：{exception.Message}";
                    RollbackPublishedCandidates(candidateSettlement, previousSettlement, candidateHunt, previousHunt, previousDestinationState);
                    if (initialStartup)
                        runtime.Reset();
                    return false;
                }
                reportWarning?.Invoke($"活动狩猎阶段已经切换，但阶段通知存在异常，将继续恢复权威运行态：{exception.Message}");
            }
            return true;
        }

        private ActiveHuntRestoreResult RejectPreparedSettlement(IPlayableSettlementRuntime candidateSettlement, PlayableHuntDestinationRuntime.RuntimeState destinationState, string reason)
        {
            runtime.ReleaseSettlement(candidateSettlement);
            PlayableHuntDestinationRuntime.RestoreState(destinationState);
            return ActiveHuntRestoreResult.Failed(reason);
        }

        private ActiveHuntRestoreResult RejectCandidates(IPlayableSettlementRuntime candidateSettlement, IPlayableHuntRuntime candidateHunt, PlayableHuntDestinationRuntime.RuntimeState destinationState, string reason)
        {
            runtime.ReleaseHunt(candidateHunt);
            return RejectPreparedSettlement(candidateSettlement, destinationState, reason);
        }

        private void RollbackPublishedCandidates(IPlayableSettlementRuntime candidateSettlement, IPlayableSettlementRuntime previousSettlement, IPlayableHuntRuntime candidateHunt, IPlayableHuntRuntime previousHunt, PlayableHuntDestinationRuntime.RuntimeState destinationState)
        {
            RestoreGenerations(candidateSettlement, previousSettlement, candidateHunt, previousHunt, destinationState);
            runtime.ReleaseHunt(candidateHunt);
            runtime.ReleaseSettlement(candidateSettlement);
        }

        private void RestoreGenerations(IPlayableSettlementRuntime currentSettlement, IPlayableSettlementRuntime previousSettlement, IPlayableHuntRuntime currentHunt, IPlayableHuntRuntime previousHunt, PlayableHuntDestinationRuntime.RuntimeState destinationState)
        {
            if (!runtime.TrySwapHunt(currentHunt, previousHunt, out string huntReason))
                throw new InvalidOperationException($"恢复狩猎运行世代失败：{huntReason}");
            if (!runtime.TrySwapSettlement(currentSettlement, previousSettlement, out string settlementReason))
                throw new InvalidOperationException($"恢复营地运行世代失败：{settlementReason}");
            PlayableHuntDestinationRuntime.RestoreState(destinationState);
        }

        private bool TryRollbackPhase(GamePhase targetPhase, out string reason)
        {
            if (runtime.CurrentPhase == targetPhase)
            {
                reason = string.Empty;
                return true;
            }
            try
            {
                if (runtime.TransitionTo(targetPhase) && runtime.CurrentPhase == targetPhase)
                {
                    reason = string.Empty;
                    return true;
                }
                reason = $"阶段仍停留在 {runtime.CurrentPhase}";
                return false;
            }
            catch (Exception exception)
            {
                if (runtime.CurrentPhase != targetPhase)
                {
                    reason = exception.Message;
                    return false;
                }
                reportWarning?.Invoke($"阶段已经回滚到 {targetPhase}，但阶段通知存在异常：{exception.Message}");
                reason = string.Empty;
                return true;
            }
        }

        private void ReleaseRetiredRuntime(IPlayableSettlementRuntime previousSettlement, IPlayableHuntRuntime previousHunt)
        {
            try
            {
                if (previousSettlement != null)
                    runtime.ReleaseSettlement(previousSettlement);
            }
            catch (Exception exception)
            {
                reportWarning?.Invoke($"退役旧营地 ActionSession 时发生异常，活动狩猎恢复结果仍然有效：{exception.Message}");
            }
            try
            {
                if (previousHunt != null)
                    runtime.ReleaseHunt(previousHunt);
            }
            catch (Exception exception)
            {
                reportWarning?.Invoke($"退役旧狩猎运行世代时发生异常，活动狩猎恢复结果仍然有效：{exception.Message}");
            }
        }

        private static CampaignSnapshot CreateCandidateCampaign(CampaignSnapshot campaign, IPlayableSettlementRuntime settlement)
        {
            return new CampaignSnapshot
            {
                CampaignSchemaVersion = campaign.CampaignSchemaVersion,
                Settlement = settlement.Data,
                HasActiveHuntState = campaign.HasActiveHuntState,
                ActiveHunt = campaign.ActiveHunt
            };
        }
    }
}
