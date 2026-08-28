using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;

namespace Core
{
    internal interface ICampaignHuntReturnHost
    {
        GamePhase CurrentPhase { get; }
        IPlayableHuntRuntime HuntRuntime { get; }
        IPlayableSettlementRuntime SettlementRuntime { get; }
        PlayableHuntActionSession HuntActionSession { get; }
        PlayableSettlementActionSession SettlementActionSession { get; }
        UniTask<bool> SaveCampaignAsync(bool includeActiveHunt, CancellationToken cancellationToken);
        UniTask<CampaignPhaseTransitionResult> TransitionToSettlementAsync();
        SettlementEventRestoreProjection CreateEventRestoreCandidate();
        void PublishEventRestore(SettlementEventRestoreProjection projection);
        bool TryClearAppliedReturnCheckpoint(SettlementInstance settlement, HuntRecord record, out string reason);
        UniTask<bool> ResolveSettlementEventsAsync(IPlayableSettlementRuntime runtime, PlayableSettlementActionSession session, SettlementEventRestorePlan plan, SettlementEventRestoreProjection projection);
    }

    internal sealed class CampaignHuntReturnTransaction
    {
        private readonly ICampaignHuntReturnHost host;
        private int retreatOperationSequence;
        private int recoveryOperationSequence;
        private bool retreatInFlight;
        private bool recoveryInFlight;
        private bool preparedExit;

        public CampaignHuntReturnTransaction(ICampaignHuntReturnHost host)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public bool IsRetreatInFlight => retreatInFlight;
        public bool IsRecoveryInFlight => recoveryInFlight;
        public bool IsPreparedExit => preparedExit;
        public HuntRecord PendingRecord => host.SettlementRuntime?.Manager?.Data?.PendingHuntReturn;

        public void CompletePreparedExit() => preparedExit = false;

        public void Reset()
        {
            retreatOperationSequence++;
            recoveryOperationSequence++;
            retreatInFlight = false;
            recoveryInFlight = false;
            preparedExit = false;
        }

        public async UniTask<HuntRetreatCommandResult> PrepareRetreatAsync(HuntRetreatDecision decision, CancellationToken cancellationToken)
        {
            if (retreatInFlight) return HuntRetreatCommandResult.Failed("回营流程正在处理中。");
            if (host.CurrentPhase != GamePhase.Hunt || host.HuntActionSession == null || !host.HuntActionSession.IsActive)
                return HuntRetreatCommandResult.Failed("当前不在有效的狩猎阶段。");
            if (host.HuntActionSession.IsRunning) return HuntRetreatCommandResult.Failed("请先完成当前狩猎流程。");
            if (host.HuntRuntime?.Manager == null || host.SettlementRuntime?.Manager?.Data == null || host.SettlementRuntime.Manager.HunterMgmt == null)
                return HuntRetreatCommandResult.Failed("狩猎结算依赖尚未准备完成。");

            if (preparedExit && PendingRecord != null)
            {
                HuntRecord pendingRecord = PendingRecord;
                int retrySequence = ++retreatOperationSequence;
                retreatInFlight = true;
                try
                {
                    CampaignPhaseTransitionResult retryTransition = await host.TransitionToSettlementAsync();
                    if (retrySequence != retreatOperationSequence) return HuntRetreatCommandResult.Failed("回营重试期间权威运行世代已经变化，请在当前阶段重试。");
                    return retryTransition.Succeeded ? HuntRetreatCommandResult.Success(pendingRecord) : HuntRetreatCommandResult.Failed(retryTransition.Reason);
                }
                finally
                {
                    if (retrySequence == retreatOperationSequence) retreatInFlight = false;
                }
            }

            IPlayableHuntRuntime sourceHunt = host.HuntRuntime;
            IPlayableSettlementRuntime sourceSettlement = host.SettlementRuntime;
            int sequence = ++retreatOperationSequence;
            retreatInFlight = true;
            bool pendingCheckpointDurable = false;
            bool rollbackNeedsPendingRestore = false;
            bool pendingAssigned = false;
            HuntRecord preparedRecord = null;
            try
            {
                HuntRetreatCommandResult retreat = await sourceHunt.ActionSession.PrepareRetreatAsync(sourceSettlement.Manager.Data.CurrentYear, decision, cancellationToken);
                if (!retreat.Succeeded) return retreat;
                if (!IsCurrent(sequence, sourceHunt, sourceSettlement)) return HuntRetreatCommandResult.Failed("回营准备期间权威运行世代已经变化，请在当前阶段重试。");

                sourceSettlement.Manager.Data.PendingHuntReturn = retreat.Record;
                preparedRecord = retreat.Record;
                pendingAssigned = true;
                if (!await host.SaveCampaignAsync(false, cancellationToken))
                {
                    if (sequence == retreatOperationSequence && ReferenceEquals(host.SettlementRuntime, sourceSettlement)) sourceSettlement.Manager.Data.PendingHuntReturn = null;
                    return HuntRetreatCommandResult.Failed("无法建立可靠的回营检查点，请留在狩猎阶段重试。");
                }
                pendingCheckpointDurable = true;
                if (!IsCurrent(sequence, sourceHunt, sourceSettlement))
                    return HuntRetreatCommandResult.Failed("保存回营检查点期间权威运行世代已经变化。");

                sourceHunt.ActionSession.SetReturnCheckpointLock(true);
                preparedExit = true;
                CampaignPhaseTransitionResult transition = await host.TransitionToSettlementAsync();
                if (transition.Succeeded) return retreat;
                if (!IsCurrent(sequence, sourceHunt, sourceSettlement))
                    return HuntRetreatCommandResult.Failed("阶段切换失败期间权威运行世代已经变化，请保留回营检查点并重试。");

                preparedExit = false;
                sourceSettlement.Manager.Data.PendingHuntReturn = null;
                rollbackNeedsPendingRestore = true;
                if (!await host.SaveCampaignAsync(true, cancellationToken))
                {
                    if (sequence != retreatOperationSequence || !ReferenceEquals(host.SettlementRuntime, sourceSettlement))
                        return HuntRetreatCommandResult.Failed("阶段回滚期间权威运行世代已经变化，请保留回营检查点并重试。");
                    preparedExit = true;
                    sourceSettlement.Manager.Data.PendingHuntReturn = retreat.Record;
                    rollbackNeedsPendingRestore = false;
                    return HuntRetreatCommandResult.Failed("阶段切换被拒绝，且回营检查点尚未安全撤销；请直接重试回营。");
                }
                rollbackNeedsPendingRestore = false;
                pendingCheckpointDurable = false;
                if (ReferenceEquals(host.HuntRuntime, sourceHunt)) sourceHunt.ActionSession.SetReturnCheckpointLock(false);
                return HuntRetreatCommandResult.Failed(transition.Reason);
            }
            catch (OperationCanceledException)
            {
                if (sequence == retreatOperationSequence)
                {
                    if (rollbackNeedsPendingRestore)
                    {
                        sourceSettlement.Manager.Data.PendingHuntReturn = preparedRecord;
                        preparedExit = true;
                    }
                    else if (pendingAssigned && !pendingCheckpointDurable) sourceSettlement.Manager.Data.PendingHuntReturn = null;
                }
                return HuntRetreatCommandResult.Failed("回营流程已取消。");
            }
            catch (Exception)
            {
                if (sequence == retreatOperationSequence)
                {
                    if (rollbackNeedsPendingRestore)
                    {
                        sourceSettlement.Manager.Data.PendingHuntReturn = preparedRecord;
                        preparedExit = true;
                    }
                    else if (pendingAssigned && !pendingCheckpointDurable) sourceSettlement.Manager.Data.PendingHuntReturn = null;
                }
                return HuntRetreatCommandResult.Failed("回营结算失败，请保留当前狩猎并重试。");
            }
            finally
            {
                if (sequence == retreatOperationSequence) retreatInFlight = false;
            }
        }

        public async UniTask<SettlementHuntReturnCommandResult> ApplyPendingAsync(bool queueSettlementEvents, CancellationToken cancellationToken)
        {
            HuntRecord record = PendingRecord;
            PlayableSettlementActionSession session = host.SettlementActionSession;
            IPlayableSettlementRuntime runtime = host.SettlementRuntime;
            if (record == null) return SettlementHuntReturnCommandResult.Failed("没有待完成的远征回营记录。");
            if (session == null || !session.IsActive || runtime == null) return SettlementHuntReturnCommandResult.Failed("营地回营环境尚未准备完成。");
            if (recoveryInFlight) return SettlementHuntReturnCommandResult.Failed("回营结算正在处理中。");
            int sequence = ++recoveryOperationSequence;
            recoveryInFlight = true;
            bool checkpointCleared = false;
            bool checkpointSaveDurable = false;
            try
            {
                SettlementHuntReturnCommandResult result = await session.ApplyHuntReturnAsync(record, cancellationToken);
                if (!result.Succeeded) return result;
                if (!IsCurrentSettlement(sequence, runtime, session)) return SettlementHuntReturnCommandResult.Failed("回营结算期间权威运行世代已经变化。");
                if (!await host.SaveCampaignAsync(false, cancellationToken)) return SettlementHuntReturnCommandResult.Failed("回营结果尚未可靠保存，已保留待恢复记录。");

                SettlementEventRestoreProjection projection = null;
                SettlementEventRestorePlan restorePlan = default;
                if (queueSettlementEvents)
                {
                    projection = host.CreateEventRestoreCandidate();
                    if (projection == null) return SettlementHuntReturnCommandResult.Failed("营地事件恢复投影尚未准备完成。");
                    restorePlan = projection.Prepare();
                    if (!restorePlan.Succeeded)
                    {
                        host.PublishEventRestore(projection);
                        return SettlementHuntReturnCommandResult.Failed(restorePlan.FailureReason);
                    }
                }
                if (!IsCurrentSettlement(sequence, runtime, session)) return SettlementHuntReturnCommandResult.Failed("回营投影期间权威运行世代已经变化。");
                if (!host.TryClearAppliedReturnCheckpoint(runtime.Manager.Data, record, out string checkpointReason))
                    return SettlementHuntReturnCommandResult.Failed(checkpointReason);
                checkpointCleared = true;
                if (!await host.SaveCampaignAsync(false, cancellationToken))
                {
                    if (sequence == recoveryOperationSequence && ReferenceEquals(host.SettlementRuntime, runtime)) runtime.Manager.Data.PendingHuntReturn = record;
                    checkpointCleared = false;
                    return SettlementHuntReturnCommandResult.Failed("回营检查点尚未清除，请重试后再出猎。");
                }
                checkpointSaveDurable = true;
                checkpointCleared = false;
                if (host.SettlementRuntime?.Manager?.Data?.PendingHuntReturn != null) return SettlementHuntReturnCommandResult.Failed("回营检查点清除状态未生效。");
                if (!IsCurrentSettlement(sequence, runtime, session)) return SettlementHuntReturnCommandResult.Failed("回营检查点保存后权威运行世代已经变化。");
                if (!queueSettlementEvents) return result;

                host.PublishEventRestore(projection);
                await host.ResolveSettlementEventsAsync(runtime, session, restorePlan, projection);
                return result;
            }
            catch (OperationCanceledException)
            {
                if (checkpointCleared && !checkpointSaveDurable && sequence == recoveryOperationSequence && ReferenceEquals(host.SettlementRuntime, runtime)) runtime.Manager.Data.PendingHuntReturn = record;
                return SettlementHuntReturnCommandResult.Failed("回营流程已取消。");
            }
            catch (Exception)
            {
                if (checkpointCleared && !checkpointSaveDurable && sequence == recoveryOperationSequence && ReferenceEquals(host.SettlementRuntime, runtime)) runtime.Manager.Data.PendingHuntReturn = record;
                return SettlementHuntReturnCommandResult.Failed("回营结算失败，请保留待恢复记录并重试。");
            }
            finally
            {
                if (sequence == recoveryOperationSequence) recoveryInFlight = false;
            }
        }

        private bool IsCurrent(int sequence, IPlayableHuntRuntime hunt, IPlayableSettlementRuntime settlement)
            => sequence == retreatOperationSequence && ReferenceEquals(host.HuntRuntime, hunt) && ReferenceEquals(host.SettlementRuntime, settlement);

        private bool IsCurrentSettlement(int sequence, IPlayableSettlementRuntime settlement, PlayableSettlementActionSession session)
            => sequence == recoveryOperationSequence && ReferenceEquals(host.SettlementRuntime, settlement) && ReferenceEquals(host.SettlementActionSession, session);
    }
}
