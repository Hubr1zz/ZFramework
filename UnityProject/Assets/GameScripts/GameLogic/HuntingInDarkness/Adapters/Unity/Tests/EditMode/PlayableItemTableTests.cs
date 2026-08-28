using System.Collections.Generic;
using System.Reflection;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using NUnit.Framework;
using UnityEngine;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableItemTableTests
    {
        private readonly List<ItemData> createdItems = new();

        [TearDown]
        public void TearDown()
        {
            foreach (ItemData item in createdItems)
                if (item != null)
                    Object.DestroyImmediate(item);
            createdItems.Clear();
        }

        [Test]
        public void Build_MapsStableItemAndNormalizesKeywords()
        {
            var record = new ItemTableRecord
            {
                id = "stone_charm",
                itemName = "石符",
                itemType = "Consumable",
                tags = new List<string> { "Stone", "Rare" },
                keywords = new List<string> { " Ritual ", "ritual" },
                stackLimit = 0,
                huntNoise = 0,
                consumableEffect = "RecoverBodyPart",
                consumableEffectAmount = 1
            };

            List<ItemData> items = Build(record);

            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(items[0].name, Is.EqualTo("stone_charm"));
            Assert.That(items[0].tags, Is.EquivalentTo(new[] { ItemTag.Stone, ItemTag.Rare }));
            Assert.That(items[0].keywords, Is.EqualTo(new[] { "ritual" }));
            Assert.That(items[0].stackLimit, Is.EqualTo(1));
            Assert.That(items[0].HuntNoise, Is.Zero);
            Assert.That(items[0].ConsumableEffect, Is.EqualTo(ConsumableEffectKind.RecoverBodyPart));
            Assert.That(items[0].ConsumableEffectAmount, Is.EqualTo(1));
        }

        [Test]
        public void Build_RejectsEveryAmbiguousDuplicate()
        {
            var errors = new List<string>();
            var records = new[]
            {
                new ItemTableRecord { id = "duplicate", itemName = "甲", itemType = "Resource" },
                new ItemTableRecord { id = "duplicate", itemName = "乙", itemType = "Resource" },
                new ItemTableRecord { id = "unique-a", itemName = "同名", itemType = "Resource" },
                new ItemTableRecord { id = "unique-b", itemName = "同名", itemType = "Resource" }
            };

            List<ItemData> items = PlayableItemTableRuntime.Build(records, errors.Add);
            createdItems.AddRange(items);

            Assert.That(items, Is.Empty);
            Assert.That(errors, Has.Count.EqualTo(2));
        }

        [Test]
        public void RuntimeTable_ProvidesResourceContent()
        {
            ItemData item = null;
            foreach (ItemData candidate in PlayableItemTableRuntime.GetItems(PlayableContentSourceTestAssets.LoadBundle().ItemsTable))
                if (candidate != null && candidate.name == "black_salt")
                    item = candidate;

            Assert.That(item, Is.Not.Null);
            Assert.That(item.itemName, Is.EqualTo("黑盐"));
            Assert.That(item.itemType, Is.EqualTo(ItemType.Resource));
            Assert.That(item.keywords, Does.Contain("ritual"));
        }

        [Test]
        public void RuntimeTable_ProvidesQuietingSaltWard()
        {
            ItemData item = null;
            foreach (ItemData candidate in PlayableItemTableRuntime.GetItems(PlayableContentSourceTestAssets.LoadBundle().ItemsTable))
                if (candidate != null && candidate.ContentId == "salt_ward")
                    item = candidate;

            Assert.That(item, Is.Not.Null);
            Assert.That(item.HuntNoise, Is.EqualTo(-1));
        }

        [Test]
        public void RuntimeTable_ProvidesRecoveringMushroomFleshPoultice()
        {
            ItemData item = FindRuntimeItem("mushroom_flesh_poultice");

            Assert.That(item, Is.Not.Null);
            Assert.That(item.itemType, Is.EqualTo(ItemType.Consumable));
            Assert.That(item.ConsumableEffect, Is.EqualTo(ConsumableEffectKind.RecoverBodyPart));
            Assert.That(item.ConsumableEffectAmount, Is.EqualTo(1));
            Assert.That(item.HuntNoise, Is.Zero);
        }

        [Test]
        public void RuntimeTable_ProvidesDistinctStarterHuntLoadoutChoices()
        {
            ItemData edge = FindRuntimeItem("salt_crystal_edge");
            ItemData wrap = FindRuntimeItem("fungal_hush_wrap");

            Assert.That(edge, Is.Not.Null);
            Assert.That(edge.itemType, Is.EqualTo(ItemType.Weapon));
            Assert.That(edge.weaponStats.power, Is.EqualTo(2));
            Assert.That(edge.HuntNoise, Is.EqualTo(1));
            Assert.That(wrap, Is.Not.Null);
            Assert.That(wrap.itemType, Is.EqualTo(ItemType.Armor));
            Assert.That(wrap.armorStats.armorBody, Is.EqualTo(1));
            Assert.That(wrap.armorStats.armorArms, Is.EqualTo(1));
            Assert.That(wrap.HuntNoise, Is.EqualTo(-1));
        }

        [Test]
        public void RuntimeTable_ProvidesEchoMaterialWeaponAndWatcherArmor()
        {
            ItemData sinew = FindRuntimeItem("echo_sinew");
            ItemData spear = FindRuntimeItem("echo_hook_spear");
            ItemData mantle = FindRuntimeItem("stonewatch_mantle");

            Assert.That(sinew, Is.Not.Null);
            Assert.That(sinew.itemType, Is.EqualTo(ItemType.Resource));
            Assert.That(sinew.keywords, Does.Contain("echo"));
            Assert.That(spear, Is.Not.Null);
            Assert.That(spear.weaponStats.range, Is.EqualTo(2));
            Assert.That(spear.keywords, Is.SupersetOf(new[] { "echo", "reach" }));
            Assert.That(mantle, Is.Not.Null);
            Assert.That(mantle.armorStats.armorLegs, Is.EqualTo(1));
            Assert.That(mantle.HuntNoise, Is.EqualTo(-1));
            Assert.That(mantle.keywords, Does.Contain("quiet"));
        }

        [Test]
        public void RuntimeTable_ProvidesStoneForestMaterialsAndCraftableGear()
        {
            string[] materialIds =
            {
                "viscous_sap", "glowing_worm", "hair", "white_hair", "dust_mite", "carapace",
                "bone", "earthworm", "metal_fragment", "bulbous_root", "ancient_stone_chip"
            };
            foreach (string materialId in materialIds)
            {
                ItemData material = FindRuntimeItem(materialId);
                Assert.That(material, Is.Not.Null, materialId);
                Assert.That(material.itemType, Is.EqualTo(ItemType.Resource), materialId);
            }

            ItemData blade = FindRuntimeItem("bone_saw_blade");
            ItemData bracer = FindRuntimeItem("carapace_bracer");
            ItemData hood = FindRuntimeItem("mite_hush_hood");
            ItemData greaves = FindRuntimeItem("rootstep_greaves");
            ItemData knife = FindRuntimeItem("rust_hook_knife");
            ItemData maul = FindRuntimeItem("relic_maul");
            Assert.That(blade, Is.Not.Null);
            Assert.That(blade.itemType, Is.EqualTo(ItemType.Weapon));
            Assert.That(blade.weaponStats.speed, Is.EqualTo(2));
            Assert.That(bracer, Is.Not.Null);
            Assert.That(bracer.itemType, Is.EqualTo(ItemType.Armor));
            Assert.That(bracer.armorStats.armorArms, Is.EqualTo(1));
            Assert.That(hood.armorStats.armorHead, Is.EqualTo(1));
            Assert.That(hood.HuntNoise, Is.EqualTo(-1));
            Assert.That(greaves.armorStats.armorLegs, Is.EqualTo(1));
            Assert.That(greaves.HuntNoise, Is.EqualTo(-1));
            Assert.That(knife.weaponStats.accuracy, Is.EqualTo(1));
            Assert.That(knife.keywords, Does.Contain("serrated"));
            Assert.That(maul.weaponStats.power, Is.EqualTo(3));
            Assert.That(maul.keywords, Does.Contain("relic"));
        }

        [Test]
        public void SettlementPlan_RejectsItemWithoutExplicitStableId()
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.name = "legacy_fallback";
            item.itemName = "旧物品";
            createdItems.Add(item);
            System.Type planType = typeof(PlayableSettlementContentCatalog).Assembly.GetType("HuntingInDarkness.Settlement.PlayableSettlementContentPlan");
            MethodInfo validateMethod = planType.GetMethod("ValidateContent", BindingFlags.Static | BindingFlags.NonPublic);
            object[] arguments = { new List<ItemData> { item }, new List<InventionData>(), new List<CraftRecipe>(), new List<EventData>(), new List<HunterData>(), new List<HunterData>(), null, null };

            bool valid = (bool)validateMethod.Invoke(null, arguments);

            Assert.That(valid, Is.False);
            Assert.That((string)arguments[7], Does.Contain("显式稳定 ContentId"));
        }

        private List<ItemData> Build(params ItemTableRecord[] records)
        {
            List<ItemData> items = PlayableItemTableRuntime.Build(records);
            createdItems.AddRange(items);
            return items;
        }

        private static ItemData FindRuntimeItem(string contentId)
        {
            foreach (ItemData item in PlayableItemTableRuntime.GetItems(PlayableContentSourceTestAssets.LoadBundle().ItemsTable))
                if (item != null && item.ContentId == contentId)
                    return item;
            return null;
        }
    }
}
