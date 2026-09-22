using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class WorkshopConstructionRulesTests
    {
        [Test]
        public void TryCreatePlan_AggregatesDuplicateCosts()
        {
            var definition = new WorkshopConstructionDefinition("armor_workshop", "工具", new[] { new ResourceCost("碎石", 1), new ResourceCost("碎石", 2) });

            bool result = WorkshopConstructionRules.TryCreatePlan(definition, _ => false, _ => true, _ => 3, out WorkshopConstructionPlan plan, out string reason);

            Assert.That(result, Is.True);
            Assert.That(reason, Is.Empty);
            Assert.That(plan.Costs, Has.Count.EqualTo(1));
            Assert.That(plan.Costs[0].Amount, Is.EqualTo(3));
        }

        [Test]
        public void TryCreatePlan_RequiresInventionAndRejectsBuiltWorkshop()
        {
            var definition = new WorkshopConstructionDefinition("armor_workshop", "工具", new ResourceCost[0]);

            Assert.That(WorkshopConstructionRules.TryCreatePlan(definition, _ => false, _ => false, _ => 0, out _, out string inventionReason), Is.False);
            Assert.That(inventionReason, Does.Contain("工具"));
            Assert.That(WorkshopConstructionRules.TryCreatePlan(definition, _ => true, _ => true, _ => 0, out _, out string builtReason), Is.False);
            Assert.That(builtReason, Is.EqualTo("工坊已建造"));
        }

        [Test]
        public void TryCreatePlan_RejectsInvalidOrMissingCosts()
        {
            var invalid = new WorkshopConstructionDefinition("armor_workshop", "工具", new[] { new ResourceCost("碎石", 0) });
            var missing = new WorkshopConstructionDefinition("armor_workshop", "工具", new[] { new ResourceCost("碎石", 2) });
            var overflow = new WorkshopConstructionDefinition("armor_workshop", "工具", new[] { new ResourceCost("碎石", int.MaxValue), new ResourceCost("碎石", 1) });

            Assert.That(WorkshopConstructionRules.TryCreatePlan(invalid, _ => false, _ => true, _ => 2, out _, out string invalidReason), Is.False);
            Assert.That(invalidReason, Is.EqualTo("工坊成本配置无效"));
            Assert.That(WorkshopConstructionRules.TryCreatePlan(missing, _ => false, _ => true, _ => 1, out _, out string missingReason), Is.False);
            Assert.That(missingReason, Does.Contain("需要 2"));
            Assert.That(WorkshopConstructionRules.TryCreatePlan(overflow, _ => false, _ => true, _ => int.MaxValue, out _, out string overflowReason), Is.False);
            Assert.That(overflowReason, Is.EqualTo("工坊成本配置无效"));
        }
    }
}
