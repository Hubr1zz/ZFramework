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
        public void Build_ResolvesRequiredInventionByStableContentId()
        {
            ItemData salt = CreateItem("black_salt", "黑盐", ItemType.Resource);
            ItemData ward = CreateItem("salt_ward", "盐纹护符", ItemType.Armor);
            InventionData tools = ScriptableObject.CreateInstance<InventionData>();
            tools.name = "ToolsAsset";
            tools.ConfigureContentId("tools");
            tools.inventionName = "工具";
            createdObjects.Add(tools);
            var record = new CraftRecipeTableRecord
            {
                id = "stable-invention-recipe",
                recipeName = "稳定发明配方",
                ingredients = new List<RecipeIngredientTableRecord> { new() { itemId = "black_salt" } },
                outputItemId = "salt_ward",
                requiredInventionId = "tools"
            };

            List<CraftRecipe> recipes = PlayableCraftRecipeTableRuntime.Build(new[] { record }, new[] { salt, ward }, new[] { tools });

            Assert.That(recipes, Has.Count.EqualTo(1));
            Assert.That(recipes[0].requiredInvention, Is.SameAs(tools));
        }

        [Test]
        public async Task RuntimeRecipe_CommitsThroughSettlementActionQueue()
        {
            List<ItemData> items = CreateRuntimeRecipeItems();
            InventionData tools = CreateInvention("tools", "工具");
            IReadOnlyList<CraftRecipe> recipes = PlayableCraftRecipeTableRuntime.GetRecipes(items, new[] { tools });
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

        [Test]
        public async Task RuntimeStarterRecipe_CombinesHuntMaterialsAndCommitsEquipment()
        {
            List<ItemData> items = CreateRuntimeRecipeItems();
            ItemData mushroom = items.Find(item => item.ContentId == "mushroom_flesh");
            ItemData sap = items.Find(item => item.ContentId == "viscous_sap");
            ItemData organ = items.Find(item => item.ContentId == "soft_organ");
            InventionData tools = CreateInvention("tools", "工具");
            IReadOnlyList<CraftRecipe> recipes = PlayableCraftRecipeTableRuntime.GetRecipes(items, new[] { tools });
            CraftRecipe recipe = FindRecipe(recipes, "编制菌绒裹衣");
            var settlement = new SettlementInstance();
            settlement.AddResource(mushroom, 1);
            settlement.AddResource(sap, 1);
            settlement.AddResource(organ, 1);
            settlement.UnlockInvention(tools.ContentId);
            settlement.BuildWorkshop(recipe.requiredWorkshopId);
            var workshop = new WorkshopSystem(settlement, new InventionSystem(settlement)) { AllRecipes = new List<CraftRecipe>(recipes) };
            using var session = new PlayableSettlementActionSession(settlement, new EmptyWeaponTrainingContent(), workshopSystem: workshop);

            SettlementCraftCommandResult result = await session.CraftAsync(recipe);

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(settlement.GetResource("mushroom_flesh"), Is.Zero);
            Assert.That(settlement.GetResource("soft_organ"), Is.Zero);
            Assert.That(settlement.GetStoredEquipment("fungal_hush_wrap"), Is.EqualTo(1));
        }

        [Test]
        public async Task RuntimeStoneForestArmor_RequiresBuiltWorkshopAndCommitsEquipment()
        {
            List<ItemData> items = CreateRuntimeRecipeItems();
            ItemData whiteHair = items.Find(item => item.ContentId == "white_hair");
            ItemData dustMite = items.Find(item => item.ContentId == "dust_mite");
            ItemData sap = items.Find(item => item.ContentId == "viscous_sap");
            InventionData tools = CreateInvention("tools", "工具");
            IReadOnlyList<CraftRecipe> recipes = PlayableCraftRecipeTableRuntime.GetRecipes(items, new[] { tools });
            CraftRecipe recipe = FindRecipe(recipes, "缝制尘螨静息兜帽");
            var settlement = new SettlementInstance();
            settlement.AddResource(whiteHair, 1);
            settlement.AddResource(dustMite, 2);
            settlement.AddResource(sap, 1);
            settlement.UnlockInvention(tools.ContentId);
            var workshop = new WorkshopSystem(settlement, new InventionSystem(settlement)) { AllRecipes = new List<CraftRecipe>(recipes) };
            using var session = new PlayableSettlementActionSession(settlement, new EmptyWeaponTrainingContent(), workshopSystem: workshop);

            SettlementCraftCommandResult blocked = await session.CraftAsync(recipe);
            settlement.BuildWorkshop("armor_workshop");
            SettlementCraftCommandResult result = await session.CraftAsync(recipe);

            Assert.That(blocked.Succeeded, Is.False);
            Assert.That(blocked.Reason, Does.Contain("armor_workshop"));
            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(settlement.GetResource("white_hair"), Is.Zero);
            Assert.That(settlement.GetResource("dust_mite"), Is.Zero);
            Assert.That(settlement.GetResource("viscous_sap"), Is.Zero);
            Assert.That(settlement.GetStoredEquipment("mite_hush_hood"), Is.EqualTo(1));
        }

        [Test]
        public async Task RuntimeEchoRecipe_ConnectsHuntMaterialToCraftedWeapon()
        {
            List<ItemData> items = CreateRuntimeRecipeItems();
            ItemData sinew = items.Find(item => item.ContentId == "echo_sinew");
            ItemData stone = items.Find(item => item.ContentId == "broken_stone");
            InventionData tools = CreateInvention("tools", "工具");
            IReadOnlyList<CraftRecipe> recipes = PlayableCraftRecipeTableRuntime.GetRecipes(items, new[] { tools });
            CraftRecipe recipe = FindRecipe(recipes, "绑制回声钩矛");
            var settlement = new SettlementInstance();
            settlement.AddResource(sinew, 1);
            settlement.AddResource(stone, 1);
            settlement.UnlockInvention(tools.ContentId);
            var workshop = new WorkshopSystem(settlement, new InventionSystem(settlement)) { AllRecipes = new List<CraftRecipe>(recipes) };
            using var session = new PlayableSettlementActionSession(settlement, new EmptyWeaponTrainingContent(), workshopSystem: workshop);

            SettlementCraftCommandResult result = await session.CraftAsync(recipe);

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(settlement.GetResource("echo_sinew"), Is.Zero);
            Assert.That(settlement.GetResource("broken_stone"), Is.Zero);
            Assert.That(settlement.GetStoredEquipment("echo_hook_spear"), Is.EqualTo(1));
        }

        private List<ItemData> CreateRuntimeRecipeItems()
        {
            var items = new List<ItemData>(PlayableItemTableRuntime.GetItems())
            {
                CreateItem("broken_stone", "碎石", ItemType.Resource),
                CreateItem("mushroom_flesh", "蘑菇肉", ItemType.Resource),
                CreateItem("soft_organ", "柔软器官", ItemType.Resource)
            };
            return items;
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

        private InventionData CreateInvention(string id, string inventionName)
        {
            InventionData invention = ScriptableObject.CreateInstance<InventionData>();
            invention.name = id;
            invention.ConfigureContentId(id);
            invention.inventionName = inventionName;
            createdObjects.Add(invention);
            return invention;
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
