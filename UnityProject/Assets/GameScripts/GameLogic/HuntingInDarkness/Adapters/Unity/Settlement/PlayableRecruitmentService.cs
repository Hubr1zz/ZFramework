using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;

namespace HuntingInDarkness.Settlement
{
    /// <summary>把可配置猎人模板与营地存档模型适配为一次原子招募。</summary>
    public sealed class PlayableRecruitmentService
    {
        private readonly Func<SettlementInstance> settlementProvider;
        private readonly Func<HunterManagementSystem> managementProvider;
        private readonly List<HunterData> templates = new();
        private readonly ItemData costItem;
        private readonly int configuredCost;
        private readonly int maximumLivingHunters;

        public IReadOnlyList<HunterData> Templates => templates;
        public string CostItemName => costItem != null ? costItem.itemName : string.Empty;

        public PlayableRecruitmentService(Func<SettlementInstance> settlementProvider, Func<HunterManagementSystem> managementProvider, IEnumerable<HunterData> recruitmentTemplates, ItemData costItem, int configuredCost, int maximumLivingHunters)
        {
            this.settlementProvider = settlementProvider;
            this.managementProvider = managementProvider;
            this.costItem = costItem;
            this.configuredCost = Math.Max(0, configuredCost);
            this.maximumLivingHunters = Math.Max(1, maximumLivingHunters);
            if (recruitmentTemplates == null) return;

            foreach (HunterData template in recruitmentTemplates)
                if (template != null)
                    templates.Add(template);
        }

        public int GetCurrentCost()
        {
            SettlementInstance settlement = settlementProvider?.Invoke();
            return RecruitmentRules.GetCost(settlement?.GetAvailableHunters().Count ?? 0, configuredCost);
        }

        public bool CanRecruit(out string reason)
        {
            SettlementInstance settlement = settlementProvider?.Invoke();
            if (settlement == null || managementProvider?.Invoke() == null)
            {
                reason = "营地招募系统尚未准备好。";
                return false;
            }
            if (templates.Count == 0)
            {
                reason = "没有可用的新猎人模板。";
                return false;
            }

            int availableCount = settlement.GetAvailableHunters().Count;
            int cost = RecruitmentRules.GetCost(availableCount, configuredCost);
            if (cost > 0 && costItem == null)
            {
                reason = "招募成本尚未配置。";
                return false;
            }

            int availableResource = costItem != null ? settlement.GetResource(costItem.itemName) : 0;
            return RecruitmentRules.CanRecruit(settlement.CurrentYear, settlement.LastRecruitmentYear, availableCount, maximumLivingHunters, availableResource, configuredCost, out reason);
        }

        public bool TryRecruit(HunterData template, string requestedName, out HunterInstance hunter, out string reason)
        {
            hunter = null;
            if (!CanRecruit(out reason)) return false;
            if (template == null || !templates.Contains(template))
            {
                reason = "请选择一名愿意靠近营火的陌生人。";
                return false;
            }

            SettlementInstance settlement = settlementProvider();
            var existingNames = new List<string>();
            foreach (HunterInstance existingHunter in settlement.Hunters)
                if (existingHunter != null)
                    existingNames.Add(existingHunter.Name);
            if (!RecruitmentRules.TryNormalizeName(requestedName, existingNames, out string normalizedName, out reason)) return false;

            int cost = RecruitmentRules.GetCost(settlement.GetAvailableHunters().Count, configuredCost);
            if (cost > 0 && !settlement.SpendResource(costItem.itemName, cost))
            {
                reason = "招募所需物资已经发生变化。";
                return false;
            }

            hunter = managementProvider().Recruit(template, normalizedName);
            if (hunter == null)
            {
                if (cost > 0)
                    settlement.AddResource(costItem.itemName, cost);
                reason = "新人没有抵达营地，物资已经返还。";
                return false;
            }

            PlayableSymptomRuntime.SynchronizeHunter(hunter);

            settlement.LastRecruitmentYear = settlement.CurrentYear;
            settlement.Timeline ??= new List<AnnalEntry>();
            settlement.Timeline.Add(new AnnalEntry
            {
                Year = settlement.CurrentYear,
                EventId = $"recruit:{hunter.InstanceId}",
                EventName = $"{hunter.Name} 加入营地",
                IsCompleted = true,
                EntryType = TimelineEntryType.PlayerAdded
            });
            reason = string.Empty;
            return true;
        }
    }
}
