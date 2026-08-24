using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;

namespace HuntingInDarkness.ActionFlow.Campaign
{
    public readonly struct CampaignHuntEntryResult
    {
        public CampaignHuntEntryResult(bool succeeded, string reason)
        {
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public static CampaignHuntEntryResult Success() => new(true, string.Empty);
        public static CampaignHuntEntryResult Failed(string reason) => new(false, reason);
    }

    /// <summary>组合根共享的战役循环门禁；出发名册是一次性令牌，回营检查点按稳定记录 ID 清理。</summary>
    public static class PlayableCampaignLoopContract
    {
        public static bool TryResolveDepartureRoster(SettlementInstance settlement, out List<HunterInstance> hunters, out string reason)
        {
            hunters = new List<HunterInstance>();
            if (settlement == null)
            {
                reason = "营地数据尚未准备完成。";
                return false;
            }
            if (settlement.DeparturePreparedYear != settlement.CurrentYear || string.IsNullOrWhiteSpace(settlement.DeparturePreparationToken) || !string.Equals(settlement.DeparturePreparationToken, settlement.RuntimeDeparturePreparationToken, StringComparison.Ordinal))
            {
                reason = "出发名册不是当前营地流程提交的一次性令牌。";
                return false;
            }
            IReadOnlyList<int> hunterIds = settlement.DepartingHunterIds;
            if (!DepartureRules.CanDepart(hunterIds, out reason)) return false;

            var uniqueIds = new HashSet<int>();
            foreach (int hunterId in hunterIds)
            {
                if (!uniqueIds.Add(hunterId))
                {
                    reason = "已提交的出发小队包含重复猎人。";
                    hunters.Clear();
                    return false;
                }
                HunterInstance hunter = settlement.GetHunter(hunterId);
                if (hunter == null || !hunter.IsAvailable)
                {
                    reason = "已提交的出发小队包含无法出战的猎人。";
                    hunters.Clear();
                    return false;
                }
                hunters.Add(hunter);
            }

            reason = string.Empty;
            return true;
        }

        internal static void CommitDepartureRoster(SettlementInstance settlement, IReadOnlyList<int> hunterIds)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            string token = Guid.NewGuid().ToString("N");
            settlement.DepartingHunterIds = hunterIds != null ? new List<int>(hunterIds) : new List<int>();
            settlement.DeparturePreparedYear = settlement.CurrentYear;
            settlement.DeparturePreparationToken = token;
            settlement.RuntimeDeparturePreparationToken = token;
        }

        public static bool TryEnterHunt(SettlementInstance settlement, Func<bool> transition, Func<IReadOnlyList<HunterInstance>, CampaignHuntEntryResult> initializeHunt, Action rollbackTransition, out string reason)
        {
            if (transition == null || initializeHunt == null || rollbackTransition == null)
            {
                reason = "战役阶段切换端口尚未完整配置。";
                return false;
            }
            if (!TryResolveDepartureRoster(settlement, out List<HunterInstance> hunters, out reason)) return false;

            bool transitioned;
            try
            {
                transitioned = transition();
            }
            catch (Exception exception)
            {
                reason = TryRollback(rollbackTransition, $"阶段切换异常：{exception.Message}");
                return false;
            }
            if (!transitioned)
            {
                reason = TryRollback(rollbackTransition, "阶段状态机拒绝进入狩猎。");
                return false;
            }

            CampaignHuntEntryResult initialized;
            try
            {
                initialized = initializeHunt(hunters);
            }
            catch (Exception exception)
            {
                initialized = CampaignHuntEntryResult.Failed($"狩猎运行环境初始化异常：{exception.Message}");
            }
            if (!initialized.Succeeded)
            {
                reason = TryRollback(rollbackTransition, initialized.Reason);
                return false;
            }

            ConsumeDepartureRoster(settlement);
            reason = string.Empty;
            return true;
        }

        private static string TryRollback(Action rollbackTransition, string reason)
        {
            try
            {
                rollbackTransition();
                return reason ?? string.Empty;
            }
            catch (Exception rollbackException)
            {
                return $"{reason}；阶段回滚异常：{rollbackException.Message}";
            }
        }

        public static void ConsumeDepartureRoster(SettlementInstance settlement)
        {
            if (settlement == null) return;
            settlement.DepartingHunterIds ??= new List<int>();
            settlement.DepartingHunterIds.Clear();
            settlement.DeparturePreparedYear = 0;
            settlement.DeparturePreparationToken = string.Empty;
            settlement.RuntimeDeparturePreparationToken = string.Empty;
        }

        public static bool TryClearAppliedReturnCheckpoint(SettlementInstance settlement, HuntRecord appliedRecord, out string reason)
        {
            if (settlement == null || appliedRecord == null || string.IsNullOrWhiteSpace(appliedRecord.RecordId))
            {
                reason = "缺少已提交的远征归来记录，无法清理回营检查点。";
                return false;
            }
            HuntRecord pendingRecord = settlement.PendingHuntReturn;
            if (pendingRecord != null && !string.Equals(pendingRecord.RecordId, appliedRecord.RecordId, StringComparison.Ordinal))
            {
                reason = "待清理的回营检查点与已提交远征记录不一致。";
                return false;
            }

            settlement.PendingHuntReturn = null;
            ConsumeDepartureRoster(settlement);
            reason = string.Empty;
            return true;
        }
    }
}
