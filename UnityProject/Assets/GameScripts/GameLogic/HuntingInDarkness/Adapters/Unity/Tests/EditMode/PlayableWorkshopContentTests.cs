using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableWorkshopContentTests
    {
        [Test]
        public void WorkshopCatalog_ProvidesBuildableArmorAndMedicalWorkshopsAndGatedRecipes()
        {
            PlayableWorkshopCatalog catalog = Resources.Load<PlayableWorkshopCatalog>("HuntingInDarkness/PlayableWorkshopCatalog");
            PlayableSettlementContentExtension[] extensions = Resources.LoadAll<PlayableSettlementContentExtension>("HuntingInDarkness/SettlementExtensions");

            Assert.That(catalog, Is.Not.Null);
            PlayableWorkshopDefinition armorWorkshop = catalog.Workshops.Single(workshop => workshop.WorkshopId == "armor_workshop");
            PlayableWorkshopDefinition medicalWorkshop = catalog.Workshops.Single(workshop => workshop.WorkshopId == "medical_workshop");
            Assert.That(armorWorkshop.DisplayName, Is.EqualTo("护甲工坊"));
            Assert.That(medicalWorkshop.DisplayName, Is.EqualTo("药剂工坊"));
            Assert.That(medicalWorkshop.RequiredInvention, Is.Not.Null);
            Assert.That(medicalWorkshop.RequiredInvention.ContentId, Is.EqualTo("tools"));
            Assert.That(medicalWorkshop.Costs.Select(cost => (cost.Item.ContentId, cost.Amount)), Is.EquivalentTo(new[] { ("broken_stone", 1), ("mushroom_flesh", 1) }));

            CraftRecipe medicineRecipe = extensions.SelectMany(extension => extension.Recipes).Single(recipe => recipe.recipeName == "培制药用菌肉");
            Assert.That(medicineRecipe.requiredWorkshopId, Is.EqualTo("medical_workshop"));
            Assert.That(medicineRecipe.requiredInvention.ContentId, Is.EqualTo("tools"));
            Assert.That(medicineRecipe.unlockedByMaterial, Is.False);
            Assert.That(medicineRecipe.ingredients.Single().item.ContentId, Is.EqualTo("soft_organ"));
            Assert.That(medicineRecipe.ingredients.Single().count, Is.EqualTo(1));
            Assert.That(medicineRecipe.outputItem.ContentId, Is.EqualTo("mushroom_flesh"));
            Assert.That(medicineRecipe.outputCount, Is.EqualTo(1));

            List<ItemData> items = catalog.Workshops.SelectMany(workshop => workshop.Costs.Select(cost => cost.Item))
                .Concat(extensions.SelectMany(extension => extension.Recipes.SelectMany(recipe => recipe.ingredients.Select(ingredient => ingredient.item).Concat(new[] { recipe.outputItem }))))
                .Where(item => item != null).Distinct().ToList();
            List<InventionData> inventions = catalog.Workshops.Select(workshop => workshop.RequiredInvention)
                .Concat(extensions.SelectMany(extension => extension.Recipes.Select(recipe => recipe.requiredInvention)))
                .Where(invention => invention != null).Distinct().ToList();
            List<CraftRecipe> recipes = extensions.SelectMany(extension => extension.Recipes).ToList();
            Assert.That(catalog.TryValidateAgainst(items, inventions, recipes, out string reason), Is.True, reason);
        }

        [Test]
        public async Task MedicalWorkshop_CraftRequiresConstructionAndConsumesSoftOrgan()
        {
            PlayableWorkshopCatalog catalog = Resources.Load<PlayableWorkshopCatalog>("HuntingInDarkness/PlayableWorkshopCatalog");
            PlayableSettlementContentExtension extension = Resources.LoadAll<PlayableSettlementContentExtension>("HuntingInDarkness/SettlementExtensions").Single(item => item.name == "BasicMedicine");
            PlayableWorkshopDefinition workshopDefinition = catalog.Workshops.Single(workshop => workshop.WorkshopId == "medical_workshop");
            CraftRecipe recipe = extension.Recipes.Single(item => item.recipeName == "培制药用菌肉");
            SettlementInstance settlement = new();
            settlement.UnlockInvention("tools");
            settlement.AddResource("broken_stone", 1);
            settlement.AddResource("mushroom_flesh", 1);
            settlement.AddResource("soft_organ", 1);
            var workshop = new WorkshopSystem(settlement, new InventionSystem(settlement)) { AllRecipes = new List<CraftRecipe> { recipe } };
            using var session = new PlayableSettlementActionSession(settlement, new EmptyWeaponTrainingContent(), workshopSystem: workshop, workshopCatalog: catalog);

            SettlementCraftCommandResult beforeBuild = await session.CraftAsync(recipe);
            Assert.That(beforeBuild.Succeeded, Is.False);
            Assert.That(settlement.GetResource("soft_organ"), Is.EqualTo(1));
            Assert.That(settlement.GetResource("mushroom_flesh"), Is.EqualTo(1));

            SettlementWorkshopConstructionResult built = await session.BuildWorkshopAsync(workshopDefinition);
            Assert.That(built.Succeeded, Is.True, built.Reason);
            Assert.That(settlement.IsWorkshopBuilt("medical_workshop"), Is.True);
            Assert.That(settlement.GetResource("broken_stone"), Is.Zero);
            Assert.That(settlement.GetResource("mushroom_flesh"), Is.Zero);

            SettlementCraftCommandResult crafted = await session.CraftAsync(recipe);
            Assert.That(crafted.Succeeded, Is.True, crafted.Reason);
            Assert.That(settlement.GetResource("soft_organ"), Is.Zero);
            Assert.That(settlement.GetResource("mushroom_flesh"), Is.EqualTo(1));

            SettlementCraftCommandResult insufficient = await session.CraftAsync(recipe);
            Assert.That(insufficient.Succeeded, Is.False);
            Assert.That(settlement.GetResource("soft_organ"), Is.Zero);
            Assert.That(settlement.GetResource("mushroom_flesh"), Is.EqualTo(1));
        }

        [Test]
        public void WorkshopCatalog_RejectsRecipeForMissingWorkshop()
        {
            PlayableWorkshopCatalog catalog = ScriptableObject.CreateInstance<PlayableWorkshopCatalog>();
            var recipe = new CraftRecipe();
            recipe.recipeName = "无效配方";
            recipe.requiredWorkshopId = "missing_workshop";
            try
            {
                bool valid = catalog.TryValidateAgainst(Array.Empty<ItemData>(), Array.Empty<InventionData>(), new[] { recipe }, out string reason);

                Assert.That(valid, Is.False);
                Assert.That(reason, Does.Contain("missing_workshop"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void WorkshopCatalog_RejectsReferencesFromAnotherContentGeneration()
        {
            PlayableWorkshopCatalog catalog = ScriptableObject.CreateInstance<PlayableWorkshopCatalog>();
            var foreignItem = ScriptableObject.CreateInstance<ItemData>();
            var workshop = new PlayableWorkshopDefinition();
            var cost = new PlayableWorkshopCost();
            SetPrivateField(workshop, "workshopId", "foreign_workshop");
            SetPrivateField(workshop, "displayName", "外部工坊");
            SetPrivateField(cost, "item", foreignItem);
            SetPrivateField(workshop, "costs", new List<PlayableWorkshopCost> { cost });
            SetPrivateField(catalog, "workshops", new List<PlayableWorkshopDefinition> { workshop });
            try
            {
                bool valid = catalog.TryValidateAgainst(Array.Empty<ItemData>(), Array.Empty<InventionData>(), Array.Empty<CraftRecipe>(), out string reason);

                Assert.That(valid, Is.False);
                Assert.That(reason, Does.Contain("内容世代之外"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(foreignItem);
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field: {fieldName}");
            field.SetValue(target, value);
        }

        private sealed class EmptyWeaponTrainingContent : IWeaponTrainingContent
        {
            public string RequiredInventionId => string.Empty;
            public string CostResourceId => string.Empty;
            public int ResourceCost => 0;
            public int Experience => 0;

            public bool TryGetFamily(string masteryId, out WeaponMasteryFamilyDefinition family)
            {
                family = default;
                return false;
            }
        }
    }
}
