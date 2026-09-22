using System;
using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Settlement
{
    public sealed class WorkshopConstructionDefinition
    {
        public string Id { get; }
        public string RequiredInventionId { get; }
        public IReadOnlyList<ResourceCost> Costs { get; }

        public WorkshopConstructionDefinition(string id, string requiredInventionId, IReadOnlyList<ResourceCost> costs)
        {
            Id = id ?? string.Empty;
            RequiredInventionId = requiredInventionId ?? string.Empty;
            Costs = costs ?? Array.Empty<ResourceCost>();
        }
    }

    public sealed class WorkshopConstructionPlan
    {
        public string WorkshopId { get; }
        public IReadOnlyList<ResourceCost> Costs { get; }

        public WorkshopConstructionPlan(string workshopId, IReadOnlyList<ResourceCost> costs)
        {
            WorkshopId = workshopId;
            Costs = costs;
        }
    }

    public static class WorkshopConstructionRules
    {
        public static bool TryCreatePlan(WorkshopConstructionDefinition definition, Func<string, bool> isBuilt, Func<string, bool> isInventionUnlocked, Func<string, int> getResource, out WorkshopConstructionPlan plan, out string reason)
        {
            plan = null;
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                reason = "工坊配置无效";
                return false;
            }
            if (isBuilt(definition.Id))
            {
                reason = "工坊已建造";
                return false;
            }
            if (!string.IsNullOrEmpty(definition.RequiredInventionId) && !isInventionUnlocked(definition.RequiredInventionId))
            {
                reason = $"需先解锁：{definition.RequiredInventionId}";
                return false;
            }

            var aggregatedCosts = new Dictionary<string, int>();
            foreach (ResourceCost cost in definition.Costs)
            {
                if (string.IsNullOrWhiteSpace(cost.ResourceId) || cost.Amount <= 0)
                {
                    reason = "工坊成本配置无效";
                    return false;
                }
                aggregatedCosts.TryGetValue(cost.ResourceId, out int amount);
                if (amount > int.MaxValue - cost.Amount)
                {
                    reason = "工坊成本配置无效";
                    return false;
                }
                aggregatedCosts[cost.ResourceId] = amount + cost.Amount;
            }

            var costs = new List<ResourceCost>();
            foreach (KeyValuePair<string, int> cost in aggregatedCosts)
            {
                int have = getResource(cost.Key);
                if (have < cost.Value)
                {
                    reason = $"资源不足：{cost.Key} 需要 {cost.Value}，当前 {have}";
                    return false;
                }
                costs.Add(new ResourceCost(cost.Key, cost.Value));
            }

            plan = new WorkshopConstructionPlan(definition.Id, costs);
            reason = string.Empty;
            return true;
        }
    }
}
