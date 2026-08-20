using System;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;

namespace HuntingInDarkness.Settlement
{
    /// <summary>把营地资源库存与纯 GameCore 分部位恢复规则组合为原子治疗。</summary>
    public sealed class PlayableHunterRecoveryService
    {
        private static readonly HunterBodyPart[] bodyParts = { HunterBodyPart.Head, HunterBodyPart.Torso, HunterBodyPart.Arms, HunterBodyPart.Legs };
        private readonly Func<SettlementInstance> settlementProvider;
        private readonly ItemData costItem;
        private readonly int configuredCost;
        private readonly int recoveryAmount;

        public string CostItemName => costItem != null ? costItem.itemName : string.Empty;
        public int Cost => configuredCost;
        public int RecoveryAmount => recoveryAmount;

        public PlayableHunterRecoveryService(Func<SettlementInstance> settlementProvider, ItemData costItem, int configuredCost, int recoveryAmount)
        {
            this.settlementProvider = settlementProvider;
            this.costItem = costItem;
            this.configuredCost = Math.Max(0, configuredCost);
            this.recoveryAmount = Math.Max(1, recoveryAmount);
        }

        public bool HasRecoverableHunter()
        {
            SettlementInstance settlement = settlementProvider?.Invoke();
            if (settlement == null) return false;

            foreach (HunterInstance hunter in settlement.GetAvailableHunters())
                foreach (HunterBodyPart bodyPart in bodyParts)
                    if (HunterRecoveryRules.CanRecover(hunter, bodyPart, out _))
                        return true;
            return false;
        }

        public bool CanTreat(HunterInstance hunter, HunterBodyPart bodyPart, out string reason)
        {
            SettlementInstance settlement = settlementProvider?.Invoke();
            if (settlement == null)
            {
                reason = "营地伤势系统尚未准备好。";
                return false;
            }
            if (!HunterRecoveryRules.CanRecover(hunter, bodyPart, out reason)) return false;
            if (configuredCost == 0)
            {
                reason = string.Empty;
                return true;
            }
            if (costItem == null)
            {
                reason = "休养成本尚未配置。";
                return false;
            }
            if (settlement.GetResource(costItem) < configuredCost)
            {
                reason = $"缺少 {costItem.itemName}。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TryTreat(HunterInstance hunter, HunterBodyPart bodyPart, out HunterRecoveryResult result, out string reason)
        {
            result = default;
            if (!CanTreat(hunter, bodyPart, out reason)) return false;

            SettlementInstance settlement = settlementProvider();
            if (configuredCost > 0 && !settlement.SpendResource(costItem, configuredCost))
            {
                reason = "休养所需物资已经发生变化。";
                return false;
            }

            if (HunterRecoveryRules.TryRecover(hunter, bodyPart, recoveryAmount, out result, out reason)) return true;

            if (configuredCost > 0)
                settlement.AddResource(costItem, configuredCost);
            return false;
        }
    }
}
