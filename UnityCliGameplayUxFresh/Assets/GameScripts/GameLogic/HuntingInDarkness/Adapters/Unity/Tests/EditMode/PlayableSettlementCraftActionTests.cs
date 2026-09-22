using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableSettlementCraftActionTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object createdObject in createdObjects)
                if (createdObject != null)
                    UnityEngine.Object.DestroyImmediate(createdObject);
            createdObjects.Clear();
        }

        [Test]
        public void LegacyRecipeWithoutContentId_UsesRecipeNameAsStableFallback()
        {
            CraftRecipe recipe = CreateRecipe("骨针制作", "骨", 2, "骨针", ItemType.Weapon);

            Assert.That(recipe.contentId, Is.Null.Or.Empty);
            Assert.That(recipe.ContentId, Is.EqualTo("骨针制作"));
        }

        [Test]
        public async Task CraftAsync_CommitsResourcesEquipmentAndFacts()
        {
            SettlementInstance settlement = CreateSettlement();
            CraftRecipe recipe = CreateRecipe("骨针制作", "骨", 2, "骨针", ItemType.Weapon);
            WorkshopSystem workshop = CreateWorkshop(settlement, recipe);
            var received = new List<string>();
            Action<ResourceChangedEvent> resourceHandler = evt => received.Add($"resource:{evt.OldAmount}->{evt.NewAmount}");
            Action<SettlementCraftedEvent> craftHandler = evt => received.Add($"crafted:{evt.OutputName}:{evt.OutputCount}");
            Action<SettlementTransactionCommittedEvent> commitHandler = evt => received.Add($"commit:{evt.Kind}");
            EventBus.Subscribe(resourceHandler);
            EventBus.Subscribe(craftHandler);
            EventBus.Subscribe(commitHandler);
            try
            {
                using PlayableSettlementActionSession session = CreateSession(settlement, workshop);

                SettlementCraftCommandResult result = await session.CraftAsync(recipe);

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(settlement.GetResource("骨"), Is.EqualTo(1));
                Assert.That(settlement.GetStoredEquipment("骨针"), Is.EqualTo(1));
                Assert.That(received, Is.EqualTo(new[] { "resource:3->1", "crafted:骨针:1", "commit:Crafting" }));
            }
            finally
            {
                EventBus.Unsubscribe(resourceHandler);
                EventBus.Unsubscribe(craftHandler);
                EventBus.Unsubscribe(commitHandler);
            }
        }

        [Test]
        public async Task CraftAsync_ConcurrentRequestsConsumeOnlyOneAvailableBatch()
        {
            SettlementInstance settlement = CreateSettlement();
            CraftRecipe recipe = CreateRecipe("骨针制作", "骨", 2, "骨针", ItemType.Weapon);
            WorkshopSystem workshop = CreateWorkshop(settlement, recipe);
            using PlayableSettlementActionSession session = CreateSession(settlement, workshop);

            Task<SettlementCraftCommandResult> first = session.CraftAsync(recipe).AsTask();
            Task<SettlementCraftCommandResult> second = session.CraftAsync(recipe).AsTask();
            SettlementCraftCommandResult[] results = await Task.WhenAll(first, second);

            Assert.That(Array.FindAll(results, result => result.Succeeded), Has.Length.EqualTo(1));
            Assert.That(settlement.GetResource("骨"), Is.EqualTo(1));
            Assert.That(settlement.GetStoredEquipment("骨针"), Is.EqualTo(1));
        }

        [Test]
        public async Task CraftAsync_MixedInventoryConsumesStoredWeaponAndResourceAtomically()
        {
            SettlementInstance settlement = CreateSettlement();
            ItemData salt = CreateItem("black_salt", ItemType.Resource);
            ItemData knife = CreateItem("stone_knife", ItemType.Weapon);
            ItemData edge = CreateItem("salt_crystal_edge", ItemType.Weapon);
            CraftRecipe recipe = CreateMixedRecipe(salt, knife, edge);
            settlement.AddResource(salt, 1);
            settlement.AddStoredItem(knife, 1);
            WorkshopSystem workshop = CreateWorkshop(settlement, recipe);
            var received = new List<string>();
            Action<ResourceChangedEvent> resourceHandler = evt => received.Add($"resource:{evt.ResourceName}:{evt.OldAmount}->{evt.NewAmount}");
            Action<SettlementCraftedEvent> craftHandler = evt => received.Add($"crafted:{evt.RecipeId}:{evt.OutputName}");
            EventBus.Subscribe(resourceHandler);
            EventBus.Subscribe(craftHandler);
            try
            {
                using PlayableSettlementActionSession session = CreateSession(settlement, workshop);

                SettlementCraftCommandResult result = await session.CraftAsync(recipe);

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(result.RecipeId, Is.EqualTo("shape_salt_crystal_edge"));
                Assert.That(settlement.GetResource(salt), Is.Zero);
                Assert.That(settlement.GetStoredItem(knife), Is.Zero);
                Assert.That(settlement.GetStoredItem(edge), Is.EqualTo(1));
                Assert.That(received, Is.EqualTo(new[] { "resource:black_salt:1->0", "crafted:shape_salt_crystal_edge:salt_crystal_edge" }));
            }
            finally
            {
                EventBus.Unsubscribe(resourceHandler);
                EventBus.Unsubscribe(craftHandler);
            }
        }

        [Test]
        public async Task CraftAsync_MissingStoredIngredientLeavesResourceUntouched()
        {
            SettlementInstance settlement = CreateSettlement();
            ItemData salt = CreateItem("black_salt", ItemType.Resource);
            ItemData knife = CreateItem("stone_knife", ItemType.Weapon);
            ItemData edge = CreateItem("salt_crystal_edge", ItemType.Weapon);
            CraftRecipe recipe = CreateMixedRecipe(salt, knife, edge);
            settlement.AddResource(salt, 1);
            WorkshopSystem workshop = CreateWorkshop(settlement, recipe);
            using PlayableSettlementActionSession session = CreateSession(settlement, workshop);

            SettlementCraftCommandResult result = await session.CraftAsync(recipe);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Does.Contain("仓库物品不足"));
            Assert.That(settlement.GetResource(salt), Is.EqualTo(1));
            Assert.That(settlement.GetStoredItem(knife), Is.Zero);
            Assert.That(settlement.GetStoredItem(edge), Is.Zero);
        }

        [Test]
        public async Task CraftAsync_ConcurrentMixedRequestsCommitOnlyOnce()
        {
            SettlementInstance settlement = CreateSettlement();
            ItemData salt = CreateItem("black_salt", ItemType.Resource);
            ItemData knife = CreateItem("stone_knife", ItemType.Weapon);
            ItemData edge = CreateItem("salt_crystal_edge", ItemType.Weapon);
            CraftRecipe recipe = CreateMixedRecipe(salt, knife, edge);
            settlement.AddResource(salt, 1);
            settlement.AddStoredItem(knife, 1);
            WorkshopSystem workshop = CreateWorkshop(settlement, recipe);
            using PlayableSettlementActionSession session = CreateSession(settlement, workshop);

            SettlementCraftCommandResult[] results = await Task.WhenAll(session.CraftAsync(recipe).AsTask(), session.CraftAsync(recipe).AsTask());

            Assert.That(Array.FindAll(results, result => result.Succeeded), Has.Length.EqualTo(1));
            Assert.That(settlement.GetResource(salt), Is.Zero);
            Assert.That(settlement.GetStoredItem(knife), Is.Zero);
            Assert.That(settlement.GetStoredItem(edge), Is.EqualTo(1));
        }

        [Test]
        public async Task CraftAsync_PreventedActionLeavesStateUntouched()
        {
            SettlementInstance settlement = CreateSettlement();
            CraftRecipe recipe = CreateRecipe("骨针制作", "骨", 2, "骨针", ItemType.Weapon);
            WorkshopSystem workshop = CreateWorkshop(settlement, recipe);
            using PlayableSettlementActionSession session = CreateSession(settlement, workshop);
            session.Reactors.RegisterGlobal(new PreventCraftReactor());

            SettlementCraftCommandResult result = await session.CraftAsync(recipe);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(settlement.GetResource("骨"), Is.EqualTo(3));
            Assert.That(settlement.GetStoredEquipment("骨针"), Is.Zero);
        }

        [Test]
        public async Task CraftAsync_UnregisteredRecipeCannotChangeState()
        {
            SettlementInstance settlement = CreateSettlement();
            CraftRecipe allowed = CreateRecipe("登记配方", "骨", 2, "骨针", ItemType.Weapon);
            CraftRecipe foreign = CreateRecipe("外来配方", "骨", 1, "神秘装备", ItemType.Weapon);
            WorkshopSystem workshop = CreateWorkshop(settlement, allowed);
            using PlayableSettlementActionSession session = CreateSession(settlement, workshop);

            SettlementCraftCommandResult result = await session.CraftAsync(foreign);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(settlement.GetResource("骨"), Is.EqualTo(3));
            Assert.That(settlement.GetStoredEquipment("神秘装备"), Is.Zero);
        }

        private static SettlementInstance CreateSettlement()
        {
            var settlement = new SettlementInstance();
            settlement.AddResource("骨", 3);
            settlement.BuildWorkshop("骨工坊");
            return settlement;
        }

        private WorkshopSystem CreateWorkshop(SettlementInstance settlement, CraftRecipe recipe)
        {
            var workshop = new WorkshopSystem(settlement, new InventionSystem(settlement));
            workshop.AllRecipes.Add(recipe);
            return workshop;
        }

        private CraftRecipe CreateRecipe(string recipeName, string ingredientName, int ingredientCount, string outputName, ItemType outputType)
        {
            ItemData ingredient = CreateItem(ingredientName, ItemType.Resource);
            ItemData output = CreateItem(outputName, outputType);
            return new CraftRecipe
            {
                recipeName = recipeName,
                requiredWorkshopId = "骨工坊",
                ingredients = new List<RecipeIngredient> { new() { item = ingredient, count = ingredientCount } },
                outputItem = output,
                outputCount = 1
            };
        }

        private static CraftRecipe CreateMixedRecipe(ItemData salt, ItemData knife, ItemData edge)
        {
            return new CraftRecipe
            {
                contentId = "shape_salt_crystal_edge",
                recipeName = "打磨盐晶短刃",
                requiredWorkshopId = "骨工坊",
                ingredients = new List<RecipeIngredient>
                {
                    new() { item = salt, count = 1 },
                    new() { item = knife, count = 1 }
                },
                outputItem = edge,
                outputCount = 1
            };
        }

        private ItemData CreateItem(string itemName, ItemType itemType)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.name = itemName;
            item.itemName = itemName;
            item.itemType = itemType;
            createdObjects.Add(item);
            return item;
        }

        private static PlayableSettlementActionSession CreateSession(SettlementInstance settlement, WorkshopSystem workshop)
        {
            return new PlayableSettlementActionSession(settlement, new EmptyWeaponTrainingContent(), workshopSystem: workshop);
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

        private sealed class PreventCraftReactor : GameActionReactor<CraftSettlementRecipeAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(CraftSettlementRecipeAction action, ReactionContext context, ReactionResponse response) => response.Prevent("制作被覆盖效果阻止");
        }
    }
}
