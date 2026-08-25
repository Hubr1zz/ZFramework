using System.Collections.Generic;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableEquipmentRulesTests
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
        public void CanEquip_RejectsSecondArmorCoveringSamePart()
        {
            var hunter = new HunterInstance(null, 1);
            ItemData equipped = CreateArmor("旧胸甲", body: 1);
            ItemData candidate = CreateArmor("新胸甲", body: 2);
            hunter.Equipment.Add(new ItemInstance(equipped));

            bool accepted = PlayableEquipmentRules.CanEquip(hunter, candidate, out string reason);

            Assert.That(accepted, Is.False);
            Assert.That(reason, Is.EqualTo("对应部位已经装备防具"));
        }

        [Test]
        public void CanEquip_AcceptsArmorCoveringDifferentPart()
        {
            var hunter = new HunterInstance(null, 1);
            hunter.Equipment.Add(new ItemInstance(CreateArmor("胸甲", body: 1)));

            bool accepted = PlayableEquipmentRules.CanEquip(hunter, CreateArmor("臂甲", arms: 1), out string reason);

            Assert.That(accepted, Is.True);
            Assert.That(reason, Is.Empty);
        }

        [Test]
        public void PlayableArmorContent_IsDiscoverableCraftableAndMitigatesDamage()
        {
            PlayableSettlementContentExtensions.Extend(CreateBaseHuntResources(), new List<CraftRecipe>(), new[] { CreateToolsInvention() }, out List<ItemData> items, out List<CraftRecipe> recipes);
            ItemData armor = items.Find(item => item != null && item.itemName == "韧膜胸甲");
            CraftRecipe recipe = recipes.Find(candidate => candidate != null && candidate.outputItem == armor);

            Assert.That(armor, Is.Not.Null);
            Assert.That(recipe, Is.Not.Null);
            Assert.That(armor.armorStats.armorBody, Is.EqualTo(1));

            var hunter = new HunterInstance(null, 1);
            hunter.Equipment.Add(new ItemInstance(armor));
            var stats = new GameplayBase.CombatSystem.CharacterCombatStats();
            PlayableHunterInjuryAdapter.Apply(hunter, stats);

            HunterDamageResult result = stats.ApplyDamage(HunterBodyPart.Torso, 1, new FirstRandom());

            Assert.That(result.ArmorPrevented, Is.EqualTo(1));
            Assert.That(result.HealthLost, Is.Zero);
        }

        [Test]
        public void ContentExtensions_RegisterItemsReferencedOnlyByRecipe()
        {
            ItemData output = CreateArmor("仅配方引用的护甲", body: 1);
            var recipe = new CraftRecipe { recipeName = "测试配方", outputItem = output };

            PlayableSettlementContentExtensions.Extend(CreateBaseHuntResources(), new[] { recipe }, new[] { CreateToolsInvention() }, out List<ItemData> items, out _);

            Assert.That(items, Does.Contain(output));
        }

        private ItemData CreateArmor(string name, int head = 0, int body = 0, int arms = 0, int legs = 0)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = name;
            item.itemType = ItemType.Armor;
            item.armorStats = new ArmorStats { armorHead = head, armorBody = body, armorArms = arms, armorLegs = legs };
            createdObjects.Add(item);
            return item;
        }

        private List<ItemData> CreateBaseHuntResources()
        {
            return new List<ItemData>
            {
                CreateResource("broken_stone", "碎石"),
                CreateResource("mushroom_flesh", "蘑菇肉"),
                CreateResource("soft_organ", "柔软器官")
            };
        }

        private ItemData CreateResource(string id, string itemName)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.name = id;
            item.ConfigureContentId(id);
            item.itemName = itemName;
            item.itemType = ItemType.Resource;
            createdObjects.Add(item);
            return item;
        }

        private InventionData CreateToolsInvention()
        {
            InventionData invention = ScriptableObject.CreateInstance<InventionData>();
            invention.name = "tools";
            invention.ConfigureContentId("tools");
            invention.inventionName = "工具";
            createdObjects.Add(invention);
            return invention;
        }

        private sealed class FirstRandom : HuntingInDarkness.GameCore.Foundation.IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
