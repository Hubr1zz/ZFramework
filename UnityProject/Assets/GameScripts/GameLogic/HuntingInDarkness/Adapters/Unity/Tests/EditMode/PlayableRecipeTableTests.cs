using System.Collections.Generic;
using System.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableRecipeTableTests
    {
        private readonly List<Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object createdObject in createdObjects)
                if (createdObject != null)
                    Object.DestroyImmediate(createdObject);
            createdObjects.Clear();
        }

        [Test]
        public void Build_ResolvesStableItemIdsAndCombinesIngredients()
        {
            ItemData salt = CreateItem("black_salt", "黑盐", ItemType.Resource);
            ItemData ward = CreateItem("salt_ward", "盐纹护符", ItemType.Armor);
            var record = new CraftRecipeTableRecord
            {
                id = "carve_salt_ward",
                recipeName = "刻制盐纹护符",
                ingredients = new List<RecipeIngredientTableRecord>
                {
                    new() { itemId = "black_salt", count = 1 },
                    new() { itemId = "black_salt", count = 2 }
                },
                outputItemId = "salt_ward",
                outputCount = 1
            };

            List<CraftRecipe> recipes = PlayableCraftRecipeTableRuntime.Build(new[] { record }, new[] { salt, ward });

            Assert.That(recipes, Has.Count.EqualTo(1));
            Assert.That(recipes[0].ingredients, Has.Count.EqualTo(1));
            Assert.That(recipes[0].ingredients[0].item, Is.SameAs(salt));
            Assert.That(recipes[0].ingredients[0].count, Is.EqualTo(3));
            Assert.That(recipes[0].outputItem, Is.SameAs(ward));
        }

        [Test]
        public void Build_RejectsUnknownReferencesAndAmbiguousRecords()
        {
            ItemData salt = CreateItem("black_salt", "黑盐", ItemType.Resource);
            var errors = new List<string>();
            var records = new[]
            {
                new CraftRecipeTableRecord { id = "duplicate", recipeName = "甲", ingredients = new List<RecipeIngredientTableRecord> { new() { itemId = "black_salt" } }, outputItemId = "missing" },
                new CraftRecipeTableRecord { id = "duplicate", recipeName = "乙", ingredients = new List<RecipeIngredientTableRecord> { new() { itemId = "black_salt" } }, outputItemId = "missing" },
                new CraftRecipeTableRecord { id = "unknown-output", recipeName = "未知产物", ingredients = new List<RecipeIngredientTableRecord> { new() { itemId = "black_salt" } }, outputItemId = "missing" }
            };

            List<CraftRecipe> recipes = PlayableCraftRecipeTableRuntime.Build(records, new[] { salt }, reportError: errors.Add);

            Assert.That(recipes, Is.Empty);
            Assert.That(errors, Has.Count.EqualTo(2));
        }

        [Test]
        public void Build_AmbiguousItemIdAndUnsafeIngredientCountsAreRejected()
        {
            ItemData firstSalt = CreateItem("black_salt", "黑盐甲", ItemType.Resource);
            ItemData secondSalt = CreateItem("black_salt", "黑盐乙", ItemType.Resource);
            ItemData thirdSalt = CreateItem("black_salt", "黑盐丙", ItemType.Resource);
            ItemData ward = CreateItem("salt_ward", "盐纹护符", ItemType.Armor);
            var ambiguous = new CraftRecipeTableRecord
            {
                id = "ambiguous",
                recipeName = "歧义配方",
                ingredients = new List<RecipeIngredientTableRecord> { new() { itemId = "black_salt" } },
                outputItemId = "salt_ward"
            };
            var overflow = new CraftRecipeTableRecord
            {
                id = "overflow",
                recipeName = "溢出配方",
                ingredients = new List<RecipeIngredientTableRecord>
                {
                    new() { itemId = "salt_ward", count = int.MaxValue },
                    new() { itemId = "salt_ward", count = 1 }
                },
                outputItemId = "salt_ward"
            };

            List<CraftRecipe> recipes = PlayableCraftRecipeTableRuntime.Build(new[] { ambiguous, overflow }, new[] { firstSalt, secondSalt, thirdSalt, ward });

            Assert.That(recipes, Is.Empty);
        }

        [Test]
        public async Task RuntimeRecipe_CommitsThroughSettlementActionQueue()
        {
            IReadOnlyList<ItemData> items = PlayableItemTableRuntime.GetItems();
            IReadOnlyList<CraftRecipe> recipes = PlayableCraftRecipeTableRuntime.GetRecipes(items, null);
            CraftRecipe recipe = FindRecipe(recipes, "刻制盐纹护符");
            var settlement = new SettlementInstance();
            settlement.AddResource(recipe.ingredients[0].item, 1);
            var workshop = new WorkshopSystem(settlement, new InventionSystem(settlement)) { AllRecipes = new List<CraftRecipe>(recipes) };
            using var session = new PlayableSettlementActionSession(settlement, new EmptyWeaponTrainingContent(), workshopSystem: workshop);

            SettlementCraftCommandResult result = await session.CraftAsync(recipe);

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(settlement.GetResource("black_salt"), Is.Zero);
            Assert.That(settlement.GetStoredEquipment("salt_ward"), Is.EqualTo(1));
        }

        private ItemData CreateItem(string id, string itemName, ItemType itemType)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.name = id;
            item.itemName = itemName;
            item.itemType = itemType;
            createdObjects.Add(item);
            return item;
        }

        private static CraftRecipe FindRecipe(IReadOnlyList<CraftRecipe> recipes, string recipeName)
        {
            foreach (CraftRecipe recipe in recipes)
                if (recipe != null && recipe.recipeName == recipeName)
                    return recipe;
            return null;
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
