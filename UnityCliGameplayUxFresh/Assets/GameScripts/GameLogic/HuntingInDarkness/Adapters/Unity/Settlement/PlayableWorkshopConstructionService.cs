using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;

namespace HuntingInDarkness.Settlement
{
    public sealed class PlayableWorkshopConstructionService
    {
        private readonly Func<SettlementInstance> getSettlement;

        public PlayableWorkshopConstructionService(Func<SettlementInstance> getSettlement)
        {
            this.getSettlement = getSettlement;
        }

        public bool CanBuild(PlayableWorkshopDefinition definition, out string reason)
        {
            return TryCreatePlan(getSettlement?.Invoke(), definition, out _, out reason);
        }

        public bool TryBuild(PlayableWorkshopDefinition definition, out string reason)
        {
            SettlementInstance settlement = getSettlement?.Invoke();
            if (!TryCreatePlan(settlement, definition, out WorkshopConstructionPlan plan, out reason)) return false;
            var spentCosts = new List<ResourceCost>();
            foreach (ResourceCost cost in plan.Costs)
            {
                if (settlement.SpendResource(cost.ResourceId, cost.Amount))
                {
                    spentCosts.Add(cost);
                    continue;
                }
                foreach (ResourceCost spentCost in spentCosts)
                    settlement.AddResource(spentCost.ResourceId, spentCost.Amount);
                reason = $"建造提交失败：{cost.ResourceId}";
                return false;
            }

            settlement.BuildWorkshop(plan.WorkshopId);
            reason = string.Empty;
            return true;
        }

        private static bool TryCreatePlan(SettlementInstance settlement, PlayableWorkshopDefinition definition, out WorkshopConstructionPlan plan, out string reason)
        {
            if (settlement == null)
            {
                plan = null;
                reason = "营地尚未就绪";
                return false;
            }
            return WorkshopConstructionRules.TryCreatePlan(ToDefinition(definition), settlement.IsWorkshopBuilt, settlement.IsInventionUnlocked, settlement.GetResource, out plan, out reason);
        }

        private static WorkshopConstructionDefinition ToDefinition(PlayableWorkshopDefinition definition)
        {
            if (definition == null) return null;

            var costs = new List<ResourceCost>();
            foreach (PlayableWorkshopCost cost in definition.Costs)
                if (cost?.Item != null)
                    costs.Add(new ResourceCost(cost.Item.ContentId, cost.Amount));
            string requiredInventionId = definition.RequiredInvention != null ? definition.RequiredInvention.ContentId : string.Empty;
            return new WorkshopConstructionDefinition(definition.WorkshopId, requiredInventionId, costs);
        }
    }
}
