using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableItemIdentityMigrationTests
    {
        private readonly List<Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            PlayableSettlementItemRegistry.Configure(null);
            foreach (Object createdObject in createdObjects)
                if (createdObject != null)
                    Object.DestroyImmediate(createdObject);
            createdObjects.Clear();
        }

        [Test]
        public void MigratePersistentState_ConvertsAndMergesLegacyKeysIdempotently()
        {
            ItemData salt = CreateItem("black_salt", "黑盐", ItemType.Resource);
            ItemData ward = CreateItem("salt_ward", "盐纹护符", ItemType.Armor);
            PlayableSettlementItemRegistry.Configure(new[] { salt, ward });
            var hunter = new HunterInstance(null, 31) { EquippedItemNames = new List<string> { "盐纹护符" } };
            var settlement = new SettlementInstance
            {
                Resources = new List<ResourceEntry>
                {
                    new() { Key = "黑盐", Value = 2 },
                    new() { Key = "black_salt", Value = 3 },
                    new() { Key = "unknown_pack_item", Value = 4 }
                },
                EquipmentStorage = new List<ResourceEntry> { new() { Key = "盐纹护符", Value = 1 } },
                Hunters = new List<HunterInstance> { hunter }
            };

            bool firstMigration = PlayableSettlementItemRegistry.MigratePersistentState(settlement);
            PlayableSettlementItemRegistry.RestoreEquipment(settlement);
            bool secondMigration = PlayableSettlementItemRegistry.MigratePersistentState(settlement);
            PlayableSettlementItemRegistry.RestoreEquipment(settlement);

            Assert.That(firstMigration, Is.True);
            Assert.That(secondMigration, Is.False);
            Assert.That(settlement.ItemIdentitySchemaVersion, Is.EqualTo(PlayableSettlementItemRegistry.CurrentIdentitySchemaVersion));
            Assert.That(settlement.GetResource("black_salt"), Is.EqualTo(5));
            Assert.That(settlement.GetResource("黑盐"), Is.Zero);
            Assert.That(settlement.GetResource("unknown_pack_item"), Is.EqualTo(4));
            Assert.That(settlement.GetStoredEquipment("salt_ward"), Is.EqualTo(1));
            Assert.That(hunter.EquippedItemIds, Is.EqualTo(new[] { "salt_ward" }));
            Assert.That(hunter.EquippedItemNames, Is.Empty);
            Assert.That(hunter.Equipment, Has.Count.EqualTo(1));
            Assert.That(hunter.Equipment[0].Data, Is.SameAs(ward));
        }

        [Test]
        public void MigratePersistentState_NewIdsWinWhenBothEquipmentListsExist()
        {
            ItemData ward = CreateItem("salt_ward", "盐纹护符", ItemType.Armor);
            PlayableSettlementItemRegistry.Configure(new[] { ward });
            var hunter = new HunterInstance(null, 32)
            {
                EquippedItemIds = new List<string> { "salt_ward" },
                EquippedItemNames = new List<string> { "盐纹护符" }
            };
            var settlement = new SettlementInstance { Hunters = new List<HunterInstance> { hunter } };

            PlayableSettlementItemRegistry.RestoreEquipment(settlement);

            Assert.That(hunter.EquippedItemIds, Is.EqualTo(new[] { "salt_ward" }));
            Assert.That(hunter.EquippedItemNames, Is.Empty);
            Assert.That(hunter.Equipment, Has.Count.EqualTo(1));
        }

        [Test]
        public void MigratePersistentState_UpgradesLegacyJsonSnapshot()
        {
            ItemData stone = CreateItem("broken_stone", "碎石", ItemType.Resource);
            ItemData ward = CreateItem("salt_ward", "盐纹护符", ItemType.Armor);
            PlayableSettlementItemRegistry.Configure(new[] { stone, ward });
            var legacyHunter = new HunterInstance(null, 33) { EquippedItemNames = new List<string> { "盐纹护符" } };
            var legacy = new SettlementInstance
            {
                Resources = new List<ResourceEntry> { new() { Key = "碎石", Value = 2 } },
                Hunters = new List<HunterInstance> { legacyHunter }
            };
            string json = JsonUtility.ToJson(legacy);
            SettlementInstance loaded = JsonUtility.FromJson<SettlementInstance>(json);

            PlayableSettlementItemRegistry.RestoreEquipment(loaded);

            Assert.That(loaded.GetResource("broken_stone"), Is.EqualTo(2));
            Assert.That(loaded.Hunters[0].EquippedItemIds, Is.EqualTo(new[] { "salt_ward" }));
            Assert.That(loaded.Hunters[0].Equipment, Has.Count.EqualTo(1));
        }

        [Test]
        public void MigratePersistentState_DoesNotDowngradeFutureSchema()
        {
            var hunter = new HunterInstance(null, 34)
            {
                EquippedItemIds = null,
                EquippedItemNames = new List<string> { "未来装备" }
            };
            var settlement = new SettlementInstance
            {
                ItemIdentitySchemaVersion = 99,
                Resources = new List<ResourceEntry> { new() { Key = " 黑盐 ", Value = -3 } },
                Hunters = new List<HunterInstance> { hunter }
            };

            bool changed = PlayableSettlementItemRegistry.MigratePersistentState(settlement);
            PlayableSettlementItemRegistry.RestoreEquipment(settlement);

            Assert.That(changed, Is.False);
            Assert.That(settlement.ItemIdentitySchemaVersion, Is.EqualTo(99));
            Assert.That(settlement.Resources[0].Key, Is.EqualTo(" 黑盐 "));
            Assert.That(settlement.Resources[0].Value, Is.EqualTo(-3));
            Assert.That(hunter.EquippedItemIds, Is.Null);
            Assert.That(hunter.EquippedItemNames, Is.EqualTo(new[] { "未来装备" }));
        }

        [Test]
        public void Configure_RejectsEveryItemWithAnAmbiguousIdentity()
        {
            ItemData first = CreateItem("shared_id", "甲", ItemType.Resource);
            ItemData second = CreateItem("shared_id", "乙", ItemType.Resource);

            PlayableSettlementItemRegistry.Configure(new[] { first, second });

            Assert.That(PlayableSettlementItemRegistry.Items, Is.Empty);
            Assert.That(PlayableSettlementItemRegistry.TryGet("shared_id", out _), Is.False);
        }

        [Test]
        public void Configure_RejectsCrossNamespaceAliasCollisions()
        {
            ItemData first = CreateItem("first_id", "second_id", ItemType.Resource);
            ItemData second = CreateItem("second_id", "第二物品", ItemType.Resource);

            PlayableSettlementItemRegistry.Configure(new[] { first, second });

            Assert.That(PlayableSettlementItemRegistry.Items, Is.Empty);
            Assert.That(PlayableSettlementItemRegistry.TryGet("second_id", out _), Is.False);
        }

        [Test]
        public void Configure_RejectsRepeatedReferenceWithoutThrowing()
        {
            ItemData item = CreateItem("repeated_item", "重复物品", ItemType.Resource);

            Assert.DoesNotThrow(() => PlayableSettlementItemRegistry.Configure(new[] { item, item }));

            Assert.That(PlayableSettlementItemRegistry.Items, Is.Empty);
            Assert.That(PlayableSettlementItemRegistry.TryGet("repeated_item", out _), Is.False);
        }

        private ItemData CreateItem(string contentId, string itemName, ItemType itemType)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.name = contentId;
            item.ConfigureContentId(contentId);
            item.itemName = itemName;
            item.itemType = itemType;
            createdObjects.Add(item);
            return item;
        }
    }
}
