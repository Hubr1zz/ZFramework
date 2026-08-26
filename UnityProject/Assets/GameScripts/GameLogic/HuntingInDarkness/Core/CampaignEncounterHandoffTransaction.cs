using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.CombatSystem;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;

namespace Core
{
    internal interface ICampaignEncounterHandoffHost
    {
        GamePhase CurrentPhase { get; }
        IPlayableCampaignRuntime CampaignRuntime { get; }
        IPlayableHuntRuntime HuntRuntime { get; }
        PlayableHuntActionSession HuntActionSession { get; }
        PlayableSettlementActionSession SettlementActionSession { get; }
        SettlementManager SettlementManager { get; }
        HuntManager HuntManager { get; }
        CampaignPersistenceCoordinator Persistence { get; }
        bool TryApplyBossFightTransition(out string reason);
        UniTask<CampaignEncounterStartResult> RunEncounterActionAsync(CampaignEncounterRequest request, CancellationToken cancellationToken);
    }

    internal sealed class CampaignEncounterHandoffTransaction
    {
        private readonly ICampaignEncounterHandoffHost host;
        private int operationSequence;
        private bool inFlight;
        private bool rollbackFailed;
        private BattleSetup pendingSetup;
        private IReadOnlyList<HunterInstance> pendingHunters;

        internal CampaignEncounterHandoffTransaction(ICampaignEncounterHandoffHost host)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
        }

        internal bool RollbackFailed => rollbackFailed;
        internal BattleSetup PendingSetup => pendingSetup;

        internal void SetPendingSetup(BattleSetup setup) => pendingSetup = setup;

        internal IReadOnlyList<HunterInstance> ConsumePendingHunters(IReadOnlyList<HunterInstance> fallback)
        {
            IReadOnlyList<HunterInstance> result = pendingHunters ?? fallback;
            pendingHunters = null;
            return result;
        }

        internal bool TryBegin(CampaignEncounterRequest request, out string reason)
        {
            rollbackFailed = false;
            if (host.CurrentPhase != request.SourcePhase)
            {
                reason = "遭遇请求的来源阶段已经结束";
                return false;
            }
            bool sourceSessionMatches = request.SourceKind switch
            {
                CampaignEncounterSourceKind.HuntBossTile or CampaignEncounterSourceKind.HuntEvent => host.HuntActionSession?.IsActive == true && host.HuntActionSession.SessionId == request.SourceSessionId,
                CampaignEncounterSourceKind.SettlementEvent => host.SettlementActionSession?.IsActive == true && host.SettlementActionSession.SessionId == request.SourceSessionId,
                _ => false
            };
            if (!sourceSessionMatches)
            {
                reason = "遭遇请求不属于当前阶段会话";
                return false;
            }
            if (!PlayableEncounterRuntime.TryCreateSetup(request.EncounterId, out BattleSetup setup, out reason)) return false;
            bool huntEncounter = request.SourceKind is CampaignEncounterSourceKind.HuntBossTile or CampaignEncounterSourceKind.HuntEvent;
            string previousStablePayload = host.Persistence?.StablePayload;
            if (huntEncounter)
            {
                if (!TryCreateEncounterHandoffPayload(request.EncounterId, out string handoffPayload, out string normalHuntPayload, out reason)) return false;
                if (string.IsNullOrWhiteSpace(previousStablePayload)) previousStablePayload = normalHuntPayload;
                if (!host.Persistence.TrySaveImmediate(handoffPayload))
                {
                    reason = "无法建立可靠的遭遇交接检查点。";
                    return false;
                }
            }

            BattleSetup previousSetup = pendingSetup;
            IReadOnlyList<HunterInstance> previousHunters = pendingHunters;
            pendingSetup = setup;
            pendingHunters = request.SourceKind == CampaignEncounterSourceKind.SettlementEvent ? host.SettlementManager?.Data.GetAvailableHunters() : host.HuntManager?.ActiveHunters;
            try
            {
                if (host.TryApplyBossFightTransition(out reason)) return true;
            }
            catch (Exception exception)
            {
                if (host.CurrentPhase == GamePhase.BossFight)
                {
                    reason = string.Empty;
                    return true;
                }
                reason = $"遭遇阶段切换异常：{exception.Message}";
            }
            return RollbackFailedTransition(huntEncounter, previousStablePayload, previousSetup, previousHunters, ref reason);
        }

        internal async UniTask<CampaignEncounterStartResult> ExecuteAsync(CampaignEncounterRequest request, CancellationToken cancellationToken)
        {
            if (inFlight) return CampaignEncounterStartResult.Failed(request.EncounterId, "已有遭遇交接事务正在执行。");
            inFlight = true;
            int sequence = operationSequence;
            try
            {
                CampaignEncounterStartResult result = await host.RunEncounterActionAsync(request, cancellationToken);
                if (sequence != operationSequence) return CampaignEncounterStartResult.Failed(request.EncounterId, "遭遇事务世代已变化。");
                if (!result.Succeeded && !rollbackFailed) ReleaseHuntLock(request);
                return result;
            }
            catch (Exception exception)
            {
                if (sequence == operationSequence && !rollbackFailed) ReleaseHuntLock(request);
                return CampaignEncounterStartResult.Failed(request.EncounterId, $"遭遇事务异常：{exception.Message}");
            }
            finally
            {
                if (sequence == operationSequence)
                    inFlight = false;
            }
        }

        private bool RollbackFailedTransition(bool huntEncounter, string previousStablePayload, BattleSetup previousSetup, IReadOnlyList<HunterInstance> previousHunters, ref string reason)
        {
            pendingSetup = previousSetup;
            pendingHunters = previousHunters;
            if (huntEncounter && !host.Persistence.TrySaveImmediate(previousStablePayload))
            {
                rollbackFailed = true;
                reason = "遭遇阶段切换失败，且交接检查点尚未安全撤销；请直接重试遭遇。";
            }
            return false;
        }

        internal void Reset()
        {
            operationSequence++;
            inFlight = false;
            rollbackFailed = false;
            pendingSetup = null;
            pendingHunters = null;
        }

        private bool TryCreateEncounterHandoffPayload(string encounterId, out string payload, out string normalHuntPayload, out string reason)
        {
            if (!ActiveHuntSnapshotAdapter.TryCapture(host.SettlementManager?.Data, host.HuntManager, host.HuntActionSession, host.HuntRuntime?.ExpeditionId, out CampaignSnapshot snapshot, out reason, true))
            {
                payload = string.Empty;
                normalHuntPayload = string.Empty;
                return false;
            }
            if (!SaveLoadSystem.TryCreatePayload(snapshot, out normalHuntPayload, out reason))
            {
                payload = string.Empty;
                return false;
            }
            snapshot.ActiveHunt.EncounterHandoffPending = true;
            snapshot.ActiveHunt.EncounterId = encounterId?.Trim() ?? string.Empty;
            return SaveLoadSystem.TryCreatePayload(snapshot, out payload, out reason);
        }

        private void ReleaseHuntLock(CampaignEncounterRequest request)
        {
            if ((request.SourceKind is CampaignEncounterSourceKind.HuntEvent or CampaignEncounterSourceKind.HuntBossTile) && host.HuntActionSession?.SessionId == request.SourceSessionId)
                host.HuntActionSession.ReleaseEncounterHandoffLock();
        }
    }
}
