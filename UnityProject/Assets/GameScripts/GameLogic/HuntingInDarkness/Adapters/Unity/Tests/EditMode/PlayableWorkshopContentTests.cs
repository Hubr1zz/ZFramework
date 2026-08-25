using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableWorkshopContentTests
    {
        [Test]
        public void WorkshopCatalog_ProvidesBuildableArmorWorkshopAndGatedRecipe()
        {
            PlayableWorkshopCatalog catalog = Resources.Load<PlayableWorkshopCatalog>("HuntingInDarkness/PlayableWorkshopCatalog");
            PlayableSettlementContentExtension[] extensions = Resources.LoadAll<PlayableSettlementContentExtension>("HuntingInDarkness/SettlementExtensions");

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Workshops, Has.Count.EqualTo(1));
            PlayableWorkshopDefinition workshop = catalog.Workshops[0];
            Assert.That(workshop.WorkshopId, Is.EqualTo("armor_workshop"));
            Assert.That(workshop.DisplayName, Is.EqualTo("护甲工坊"));
            Assert.That(workshop.RequiredInvention, Is.Not.Null);
            Assert.That(workshop.RequiredInvention.inventionName, Is.EqualTo("工具"));
            Assert.That(workshop.Costs, Has.Count.EqualTo(2));
            Assert.That(workshop.Costs.All(cost => cost.Item != null && cost.Amount > 0), Is.True);
            Assert.That(extensions.SelectMany(extension => extension.Recipes).Any(recipe => recipe.requiredWorkshopId == workshop.WorkshopId && recipe.outputItem != null), Is.True);
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
    }
}
