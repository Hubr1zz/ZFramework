using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class SettlementProgressionRulesTests
    {
        [Test]
        public void CanUnlock_AcceptsSatisfiedCost()
        {
            var definition = new InventionDefinition("工具", new string[0], new string[0], new[] { new ResourceCost("碎石", 1) });

            bool result = InventionRules.CanUnlock(definition, _ => false, resource => resource == "碎石" ? 2 : 0, out string reason);

            Assert.That(result, Is.True);
            Assert.That(reason, Is.Empty);
        }

        [Test]
        public void CanUnlock_RejectsMissingPrerequisite()
        {
            var definition = new InventionDefinition("加工", new[] { "工具" }, new string[0], new ResourceCost[0]);

            bool result = InventionRules.CanUnlock(definition, _ => false, _ => 0, out string reason);

            Assert.That(result, Is.False);
            Assert.That(reason, Does.Contain("工具"));
        }

        [Test]
        public void CanUnlock_AggregatesDuplicateCostsAndRejectsNegativeCost()
        {
            var duplicateCosts = new InventionDefinition("工具", new string[0], new string[0], new[] { new ResourceCost("碎石", 1), new ResourceCost("碎石", 1) });
            var negativeCost = new InventionDefinition("异常发明", new string[0], new string[0], new[] { new ResourceCost("碎石", -1) });

            Assert.That(InventionRules.CanUnlock(duplicateCosts, _ => false, _ => 1, out string duplicateReason), Is.False);
            Assert.That(duplicateReason, Does.Contain("需要 2"));
            Assert.That(InventionRules.CanUnlock(negativeCost, _ => false, _ => 1, out string negativeReason), Is.False);
            Assert.That(negativeReason, Is.EqualTo("发明成本配置无效"));
        }

        [Test]
        public void CanCraft_RequiresInventionAndMaterials()
        {
            var recipe = new CraftRecipeDefinition("打磨石器", "工具", false, new[] { new ResourceCost("碎石", 1) }, "石质工具", 1);

            Assert.That(WorkshopRules.CanCraft(recipe, _ => false, _ => 2, out string lockedReason), Is.False);
            Assert.That(lockedReason, Is.EqualTo("配方未解锁"));
            Assert.That(WorkshopRules.CanCraft(recipe, _ => true, _ => 0, out string missingReason), Is.False);
            Assert.That(missingReason, Does.Contain("碎石"));
            Assert.That(WorkshopRules.CanCraft(recipe, _ => true, _ => 1, out string availableReason), Is.True);
            Assert.That(availableReason, Is.Empty);
        }

        [Test]
        public void CanCraft_AggregatesDuplicateIngredients()
        {
            var recipe = new CraftRecipeDefinition("打磨石器", "工具", false, new[] { new ResourceCost("碎石", 1), new ResourceCost("碎石", 1) }, "石质工具", 1);

            bool result = WorkshopRules.CanCraft(recipe, _ => true, _ => 1, out string reason);

            Assert.That(result, Is.False);
            Assert.That(reason, Does.Contain("需要 2"));
        }

        [Test]
        public void CanCraft_RejectsInvalidAmounts()
        {
            var negativeIngredient = new CraftRecipeDefinition("异常配方", "", false, new[] { new ResourceCost("碎石", -1) }, "石质工具", 1);
            var zeroOutput = new CraftRecipeDefinition("空产出", "", false, new ResourceCost[0], "石质工具", 0);

            Assert.That(WorkshopRules.CanCraft(negativeIngredient, _ => true, _ => 1, out string ingredientReason), Is.False);
            Assert.That(ingredientReason, Is.EqualTo("配方材料配置无效"));
            Assert.That(WorkshopRules.CanCraft(zeroOutput, _ => true, _ => 1, out string outputReason), Is.False);
            Assert.That(outputReason, Is.EqualTo("产出数量必须大于0"));
        }
    }
}
