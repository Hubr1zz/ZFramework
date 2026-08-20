using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableSettlementEquipmentActionTests
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
        public async Task EquipItemAsync_CommitsStorageHunterAndFacts()
        {
            SettlementInstance settlement = CreateSettlement(out HunterInstance hunter);
            ItemData item = CreateItem("燧石短刀", ItemType.Weapon);
            settlement.AddStoredEquipment(item, 1);
            var received = new List<string>();
            Action<HunterEquipmentChangedEvent> equipmentHandler = evt => received.Add($"equipment:{evt.Equipped}:{evt.StoredCount}");
            Action<SettlementTransactionCommittedEvent> commitHandler = evt => received.Add($"commit:{evt.Kind}");
            EventBus.Subscribe(equipmentHandler);
            EventBus.Subscribe(commitHandler);
            try
            {
                using PlayableSettlementActionSession session = CreateSession(settlement, item);

                SettlementEquipmentCommandResult result = await session.EquipItemAsync(hunter.InstanceId, item);

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(settlement.GetStoredEquipment(item), Is.Zero);
                Assert.That(hunter.Equipment, Has.Count.EqualTo(1));
                Assert.That(hunter.Equipment[0].Data, Is.SameAs(item));
                Assert.That(hunter.EquippedItemIds, Is.EqualTo(new[] { item.ContentId }));
                Assert.That(received, Is.EqualTo(new[] { "equipment:True:0", "commit:Equipment" }));
            }
            finally
            {
                EventBus.Unsubscribe(equipmentHandler);
                EventBus.Unsubscribe(commitHandler);
            }
        }

        [Test]
        public async Task EquipItemAsync_ConcurrentRequestsConsumeOnlyAvailableCopy()
        {
            SettlementInstance settlement = CreateSettlement(out HunterInstance hunter);
            ItemData item = CreateItem("骨锤", ItemType.Weapon);
            settlement.AddStoredEquipment(item, 1);
            using PlayableSettlementActionSession session = CreateSession(settlement, item);

            Task<SettlementEquipmentCommandResult> first = session.EquipItemAsync(hunter.InstanceId, item).AsTask();
            Task<SettlementEquipmentCommandResult> second = session.EquipItemAsync(hunter.InstanceId, item).AsTask();
            SettlementEquipmentCommandResult[] results = await Task.WhenAll(first, second);

            Assert.That(Array.FindAll(results, result => result.Succeeded), Has.Length.EqualTo(1));
            Assert.That(hunter.Equipment, Has.Count.EqualTo(1));
            Assert.That(settlement.GetStoredEquipment(item), Is.Zero);
        }

        [Test]
        public async Task EquipItemAsync_PreventedActionLeavesStateUntouched()
        {
            SettlementInstance settlement = CreateSettlement(out HunterInstance hunter);
            ItemData item = CreateItem("骨针", ItemType.Consumable);
            settlement.AddStoredEquipment(item, 1);
            using PlayableSettlementActionSession session = CreateSession(settlement, item);
            session.Reactors.RegisterGlobal(new PreventEquipReactor());

            SettlementEquipmentCommandResult result = await session.EquipItemAsync(hunter.InstanceId, item);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(hunter.Equipment, Is.Empty);
            Assert.That(settlement.GetStoredEquipment(item), Is.EqualTo(1));
        }

        [Test]
        public async Task UnequipItemAsync_RemovesExactRuntimeInstanceAndReturnsStorage()
        {
            SettlementInstance settlement = CreateSettlement(out HunterInstance hunter);
            ItemData item = CreateItem("投矛", ItemType.Weapon);
            var first = new ItemInstance(item);
            var second = new ItemInstance(item);
            hunter.Equipment.Add(first);
            hunter.Equipment.Add(second);
            hunter.EquippedItemIds.Add(item.ContentId);
            hunter.EquippedItemIds.Add(item.ContentId);
            using PlayableSettlementActionSession session = CreateSession(settlement, item);

            SettlementEquipmentCommandResult result = await session.UnequipItemAsync(hunter.InstanceId, second.InstanceId);

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(hunter.Equipment, Is.EqualTo(new[] { first }));
            Assert.That(hunter.EquippedItemIds, Is.EqualTo(new[] { item.ContentId }));
            Assert.That(settlement.GetStoredEquipment(item), Is.EqualTo(1));
        }

        [Test]
        public async Task EquipItemAsync_UnregisteredAssetCannotChangeState()
        {
            SettlementInstance settlement = CreateSettlement(out HunterInstance hunter);
            ItemData allowed = CreateItem("登记装备", ItemType.Armor);
            ItemData foreign = CreateItem("外来装备", ItemType.Armor);
            settlement.AddStoredEquipment(foreign, 1);
            using PlayableSettlementActionSession session = CreateSession(settlement, allowed);

            SettlementEquipmentCommandResult result = await session.EquipItemAsync(hunter.InstanceId, foreign);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(hunter.Equipment, Is.Empty);
            Assert.That(settlement.GetStoredEquipment(foreign), Is.EqualTo(1));
        }

        private SettlementInstance CreateSettlement(out HunterInstance hunter)
        {
            var settlement = new SettlementInstance();
            hunter = new HunterInstance(null, 701) { Name = "守夜者" };
            settlement.Hunters.Add(hunter);
            return settlement;
        }

        private ItemData CreateItem(string itemName, ItemType itemType)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = itemName;
            item.itemType = itemType;
            createdObjects.Add(item);
            return item;
        }

        private static PlayableSettlementActionSession CreateSession(SettlementInstance settlement, params ItemData[] items)
        {
            return new PlayableSettlementActionSession(settlement, new EmptyWeaponTrainingContent(), equipmentContent: new PlayableSettlementEquipmentContentAdapter(items));
        }

        private sealed class EmptyWeaponTrainingContent : IWeaponTrainingContent
        {
            public string RequiredInventionId => string.Empty;
            public string CostResourceId => string.Empty;
            public int ResourceCost => 0;
            public int Experience => 0;
            public bool TryGetFamily(string masteryId, out HuntingInDarkness.GameCore.Settlement.WeaponMasteryFamilyDefinition family)
            {
                family = default;
                return false;
            }
        }

        private sealed class PreventEquipReactor : GameActionReactor<EquipHunterItemAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;

            protected override void React(EquipHunterItemAction action, ReactionContext context, ReactionResponse response) => response.Prevent("装备效果被阻止");
        }
    }
}
