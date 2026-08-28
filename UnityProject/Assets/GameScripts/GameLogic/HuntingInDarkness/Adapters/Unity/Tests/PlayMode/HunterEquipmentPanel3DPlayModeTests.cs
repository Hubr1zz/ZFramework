using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cards3D;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class HunterEquipmentPanel3DPlayModeTests
    {
        private readonly List<Object> createdObjects = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Object createdObject in createdObjects)
                if (createdObject != null)
                    Object.Destroy(createdObject);
            createdObjects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Show_PlacesStorageCardsInSlotsAndRefreshClearsOccupancy()
        {
            var root = new GameObject("HunterEquipmentPanelTest");
            createdObjects.Add(root);
            ItemData item = CreateItem("panel_weapon", ItemType.Weapon);
            HunterData template = CreateTemplate();
            var hunter = new HunterInstance(template, 901);
            var settlement = new SettlementInstance();
            settlement.AddStoredItem(item, 1);
            HunterEquipmentPanel3D panel = HunterEquipmentPanel3D.Create(root.transform);

            panel.Show(hunter, settlement, new[] { item }, Vector3.zero);
            SlotGrid storageGrid = GetPrivateField<SlotGrid>(panel, "storageGrid");
            CardSlot storageSlot = storageGrid.Slots[0];
            Assert.That(storageSlot.OccupantCard, Is.Not.Null);
            Assert.That(storageSlot.OccupantCard.CurrentSlot, Is.SameAs(storageSlot));

            settlement.SpendStoredItem(item, 1);
            panel.RefreshVisible();
            yield return null;

            Assert.That(storageSlot.OccupantCard, Is.Null);
        }

        [UnityTest]
        public IEnumerator Show_ProjectsDecisionAttributesAndBoundedTraitSummary()
        {
            var root = new GameObject("HunterDecisionDossierTest");
            createdObjects.Add(root);
            HunterData template = CreateTemplate();
            var hunter = new HunterInstance(template, 906) { Courage = 2, Understanding = 3 };
            hunter.Traits.AddRange(new[] { "异常冗长的特性显示名称用于测试", "敏锐", "守望者", "不会完整显示的第四项特性" });
            var settlement = new SettlementInstance();
            HunterEquipmentPanel3D panel = HunterEquipmentPanel3D.Create(root.transform);

            panel.Show(hunter, settlement, System.Array.Empty<ItemData>(), Vector3.zero);
            TextMeshPro statsText = GetPrivateField<TextMeshPro>(panel, "statsText");

            Assert.That(statsText.text, Does.Contain("胆识 2"));
            Assert.That(statsText.text, Does.Contain("知识 3"));
            Assert.That(statsText.text, Does.Contain("特性 异常冗长的特性显示名…、敏锐、守望者 +1"));
            Assert.That(statsText.text, Does.Not.Contain("第四项特性"));

            hunter.Courage = 4;
            hunter.Understanding = 5;
            hunter.Traits.Clear();
            panel.RefreshVisible();
            yield return null;

            Assert.That(statsText.text, Does.Contain("胆识 4"));
            Assert.That(statsText.text, Does.Contain("知识 5"));
            Assert.That(statsText.text, Does.Contain("特性 无"));
        }

        [UnityTest]
        public IEnumerator ConsumableUseGrid_IsTransientAndCommandPendingLocksOtherCards()
        {
            var root = new GameObject("HunterConsumablePanelTest");
            createdObjects.Add(root);
            ItemData consumable = CreateItem("panel_consumable", ItemType.Consumable);
            ItemData weapon = CreateItem("panel_weapon_pending", ItemType.Weapon);
            HunterData template = CreateTemplate();
            var hunter = new HunterInstance(template, 902);
            var settlement = new SettlementInstance();
            settlement.AddStoredItem(consumable, 1);
            settlement.AddStoredItem(weapon, 1);
            HunterEquipmentPanel3D panel = HunterEquipmentPanel3D.Create(root.transform);
            bool useRequested = false;
            panel.ConfigureCommands((_, _) => DelayedFailure(), null, null, null, null, (_, _) => useRequested = true);
            panel.Show(hunter, settlement, new[] { consumable, weapon }, Vector3.zero);

            SlotGrid storageGrid = GetPrivateField<SlotGrid>(panel, "storageGrid");
            SlotGrid equipmentGrid = GetPrivateField<SlotGrid>(panel, "equipmentGrid");
            SlotGrid useGrid = GetPrivateField<SlotGrid>(panel, "consumableUseGrid");
            Assert.That(useGrid.Slots[0].OccupantCard, Is.Null);
            Assert.That(storageGrid.Slots[0].OccupantCard, Is.Not.Null);
            Assert.That(storageGrid.Slots[0].OccupantCard.CurrentSlot, Is.SameAs(storageGrid.Slots[0]));

            SettlementItemCard3D consumableCard = (SettlementItemCard3D)storageGrid.Slots[0].OccupantCard;
            BeginAndDrop(consumableCard, useGrid.Slots[0]);
            Assert.That(useRequested, Is.True);
            Assert.That(useGrid.Slots[0].OccupantCard, Is.Null);
            Assert.That(consumableCard.CurrentSlot, Is.SameAs(storageGrid.Slots[0]));

            SettlementItemCard3D weaponCard = (SettlementItemCard3D)storageGrid.Slots[1].OccupantCard;
            BeginAndDrop(weaponCard, equipmentGrid.Slots[0]);
            Assert.That(GetPrivateField<bool>(panel, "commandPending"), Is.True);
            Assert.That(weaponCard.EnableDrag, Is.False);
            Assert.That(storageGrid.Slots[0].OccupantCard.EnableDrag, Is.False);

            yield return null;
            yield return null;

            Assert.That(GetPrivateField<bool>(panel, "commandPending"), Is.False);
            Assert.That(weaponCard.CurrentSlot, Is.SameAs(storageGrid.Slots[1]));
            Assert.That(weaponCard.EnableDrag, Is.True);
        }

        [UnityTest]
        public IEnumerator PendingCommandSurvivesHideShowAndRejectsSecondDrop()
        {
            var root = new GameObject("HunterPendingRebindTest");
            createdObjects.Add(root);
            ItemData weapon = CreateItem("panel_weapon_rebind", ItemType.Weapon);
            HunterData template = CreateTemplate();
            var hunter = new HunterInstance(template, 903);
            var settlement = new SettlementInstance();
            settlement.AddStoredItem(weapon, 1);
            HunterEquipmentPanel3D panel = HunterEquipmentPanel3D.Create(root.transform);
            var completion = new UniTaskCompletionSource<SettlementEquipmentCommandResult>();
            int requestCount = 0;
            panel.ConfigureCommands((_, _) =>
            {
                requestCount++;
                return completion.Task;
            }, null);
            panel.Show(hunter, settlement, new[] { weapon }, Vector3.zero);

            SlotGrid storageGrid = GetPrivateField<SlotGrid>(panel, "storageGrid");
            SlotGrid equipmentGrid = GetPrivateField<SlotGrid>(panel, "equipmentGrid");
            SettlementItemCard3D card = (SettlementItemCard3D)storageGrid.Slots[0].OccupantCard;
            BeginAndDrop(card, equipmentGrid.Slots[0]);
            Assert.That(requestCount, Is.EqualTo(1));
            Assert.That(GetPrivateField<bool>(panel, "commandPending"), Is.True);
            Assert.That(card.EnableDrag, Is.False);

            ItemData otherWeapon = CreateItem("panel_weapon_other", ItemType.Weapon);
            HunterData otherTemplate = CreateTemplate();
            var otherHunter = new HunterInstance(otherTemplate, 905);
            var otherSettlement = new SettlementInstance();
            otherSettlement.AddStoredItem(otherWeapon, 1);
            panel.Hide();
            panel.Show(otherHunter, otherSettlement, new[] { otherWeapon }, Vector3.zero);
            yield return null;
            Assert.That(GetPrivateField<bool>(panel, "commandPending"), Is.True);
            Assert.That(card.EnableDrag, Is.False);
            Assert.That(GetPrivateField<HunterInstance>(panel, "hunter"), Is.SameAs(hunter));
            Assert.That(GetPrivateField<SettlementInstance>(panel, "settlement"), Is.SameAs(settlement));

            BeginAndDrop(card, equipmentGrid.Slots[0]);
            Assert.That(requestCount, Is.EqualTo(1));
            Assert.That(GetPrivateField<bool>(panel, "commandPending"), Is.True);

            completion.TrySetResult(new SettlementEquipmentCommandResult(true, string.Empty, hunter.InstanceId, weapon.itemName, 1));
            yield return null;
            Assert.That(GetPrivateField<bool>(panel, "commandPending"), Is.False);
            Assert.That(storageGrid.Slots[0].OccupantCard, Is.Not.Null);
            Assert.That(storageGrid.Slots[0].OccupantCard.CurrentSlot, Is.SameAs(storageGrid.Slots[0]));
            Assert.That(requestCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator InactivePanelCommandCompletionRebuildsOnEnable()
        {
            var root = new GameObject("HunterInactiveCompletionTest");
            createdObjects.Add(root);
            ItemData weapon = CreateItem("panel_weapon_inactive", ItemType.Weapon);
            HunterData template = CreateTemplate();
            var hunter = new HunterInstance(template, 904);
            var settlement = new SettlementInstance();
            settlement.AddStoredItem(weapon, 1);
            HunterEquipmentPanel3D panel = HunterEquipmentPanel3D.Create(root.transform);
            var completion = new UniTaskCompletionSource<SettlementEquipmentCommandResult>();
            panel.ConfigureCommands((_, _) => completion.Task, null);
            panel.Show(hunter, settlement, new[] { weapon }, Vector3.zero);

            SlotGrid storageGrid = GetPrivateField<SlotGrid>(panel, "storageGrid");
            SlotGrid equipmentGrid = GetPrivateField<SlotGrid>(panel, "equipmentGrid");
            SettlementItemCard3D card = (SettlementItemCard3D)storageGrid.Slots[0].OccupantCard;
            BeginAndDrop(card, equipmentGrid.Slots[0]);
            Assert.That(GetPrivateField<bool>(panel, "commandPending"), Is.True);

            root.SetActive(false);
            completion.TrySetResult(new SettlementEquipmentCommandResult(true, string.Empty, hunter.InstanceId, weapon.itemName, 1));
            yield return null;
            Assert.That(GetPrivateField<bool>(panel, "commandPending"), Is.False);
            Assert.That(GetPrivateField<bool>(panel, "refreshPending"), Is.True);

            root.SetActive(true);
            yield return null;
            Assert.That(GetPrivateField<bool>(panel, "refreshPending"), Is.False);
            Assert.That(storageGrid.Slots[0].OccupantCard, Is.Not.Null);
            Assert.That(storageGrid.Slots[0].OccupantCard.CurrentSlot, Is.SameAs(storageGrid.Slots[0]));
            Assert.That(storageGrid.Slots[0].OccupantCard.EnableDrag, Is.True);
        }

        private ItemData CreateItem(string id, ItemType type)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.name = id;
            item.itemName = id;
            item.itemType = type;
            createdObjects.Add(item);
            return item;
        }

        private HunterData CreateTemplate()
        {
            HunterData template = ScriptableObject.CreateInstance<HunterData>();
            template.name = "panel_hunter_template";
            template.hunterName = "测试猎人";
            createdObjects.Add(template);
            return template;
        }

        private static async UniTask<SettlementEquipmentCommandResult> DelayedFailure()
        {
            await UniTask.Yield();
            return SettlementEquipmentCommandResult.Failed("测试失败");
        }

        private static void BeginAndDrop(SettlementItemCard3D card, CardSlot target)
        {
            Vector2 pointerDown = Vector2.zero;
            card.HandlePointerDown(pointerDown);
            card.HandlePointerDrag(pointerDown + Vector2.right * 10f, target.transform.position);
            card.HandlePointerUp();
        }

        private static T GetPrivateField<T>(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(instance);
        }

    }
}
