using System.Collections.Generic;
using System.Reflection;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableWorkshopConstructionServiceTests
    {
        [Test]
        public void TryBuild_SpendsResourcesAndPersistsWorkshopFlag()
        {
            var settlement = new SettlementInstance();
            settlement.UnlockInvention("工具");
            settlement.AddResource("碎石", 2);
            settlement.AddResource("柔软器官", 1);
            ItemData stone = CreateItem("碎石");
            ItemData organ = CreateItem("柔软器官");
            InventionData invention = CreateInvention("工具");
            PlayableWorkshopDefinition definition = CreateDefinition(stone, organ, invention);
            var service = new PlayableWorkshopConstructionService(() => settlement);

            try
            {
                bool built = service.TryBuild(definition, out string reason);

                Assert.That(built, Is.True, reason);
                Assert.That(settlement.IsWorkshopBuilt("armor_workshop"), Is.True);
                Assert.That(settlement.GetResource("碎石"), Is.EqualTo(1));
                Assert.That(settlement.GetResource("柔软器官"), Is.Zero);
                Assert.That(service.TryBuild(definition, out string duplicateReason), Is.False);
                Assert.That(duplicateReason, Is.EqualTo("工坊已建造"));
                Assert.That(settlement.GetResource("碎石"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(stone);
                Object.DestroyImmediate(organ);
                Object.DestroyImmediate(invention);
            }
        }

        [Test]
        public void WorkshopSystem_HidesRecipeUntilRequiredWorkshopExists()
        {
            var settlement = new SettlementInstance();
            settlement.UnlockInvention("工具");
            ItemData output = CreateItem("韧膜胸甲");
            InventionData invention = CreateInvention("工具");
            var recipe = new CraftRecipe { recipeName = "缝制韧膜胸甲", outputItem = output, requiredInvention = invention, requiredWorkshopId = "armor_workshop" };
            var system = new WorkshopSystem(settlement, new InventionSystem(settlement)) { AllRecipes = new List<CraftRecipe> { recipe } };

            try
            {
                Assert.That(system.IsRecipeUnlocked(recipe), Is.False);
                Assert.That(system.GetAvailableRecipes(), Is.Empty);

                settlement.BuildWorkshop("armor_workshop");

                Assert.That(system.IsRecipeUnlocked(recipe), Is.True);
                Assert.That(system.GetAvailableRecipes(), Has.Count.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(output);
                Object.DestroyImmediate(invention);
            }
        }

        [Test]
        public void SettlementWorkshopFlags_SurviveJsonRoundTripAndOldSaveDefaults()
        {
            var settlement = new SettlementInstance();
            settlement.BuildWorkshop("armor_workshop");

            SettlementInstance restored = JsonUtility.FromJson<SettlementInstance>(JsonUtility.ToJson(settlement));
            SettlementInstance oldSave = JsonUtility.FromJson<SettlementInstance>("{\"CurrentYear\":3}");

            Assert.That(restored.IsWorkshopBuilt("armor_workshop"), Is.True);
            Assert.That(oldSave.IsWorkshopBuilt("armor_workshop"), Is.False);
            Assert.DoesNotThrow(() => oldSave.BuildWorkshop("armor_workshop"));
            Assert.That(oldSave.IsWorkshopBuilt("armor_workshop"), Is.True);
        }

        private static ItemData CreateItem(string itemName)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = itemName;
            return item;
        }

        private static InventionData CreateInvention(string inventionName)
        {
            InventionData invention = ScriptableObject.CreateInstance<InventionData>();
            invention.inventionName = inventionName;
            return invention;
        }

        private static PlayableWorkshopDefinition CreateDefinition(ItemData stone, ItemData organ, InventionData invention)
        {
            var definition = new PlayableWorkshopDefinition();
            SetField(definition, "workshopId", "armor_workshop");
            SetField(definition, "displayName", "护甲工坊");
            SetField(definition, "requiredInvention", invention);
            SetField(definition, "costs", new List<PlayableWorkshopCost> { CreateCost(stone), CreateCost(organ) });
            return definition;
        }

        private static PlayableWorkshopCost CreateCost(ItemData item)
        {
            var cost = new PlayableWorkshopCost();
            SetField(cost, "item", item);
            SetField(cost, "amount", 1);
            return cost;
        }

        private static void SetField<TValue>(object target, string fieldName, TValue value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
        }
    }
}
