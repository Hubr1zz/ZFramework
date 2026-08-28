using System.Collections.Generic;
using System.Reflection;
using Cards3D;
using HuntingInDarkness.Data;
using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class TabletopHuntDeparturePanel3DTests
    {
        [Test]
        public void PresentSquad_RestoresStagedHunterOrderInsteadOfRosterOrder()
        {
            var root = new GameObject("DeparturePanelTestRoot");
            try
            {
                HunterInstance first = CreateHunter(101, "甲");
                HunterInstance second = CreateHunter(102, "乙");
                HunterInstance third = CreateHunter(103, "丙");
                TabletopHuntDeparturePanel3D panel = TabletopHuntDeparturePanel3D.Create(root.transform);

                panel.PresentSquad(Vector3.zero, new[] { first, second, third }, new[] { third.InstanceId, first.InstanceId }, _ => { }, () => { });

                SlotGrid squadGrid = (SlotGrid)typeof(TabletopHuntDeparturePanel3D).GetField("squadGrid", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
                var stagedIds = new List<int>();
                foreach (CardSlot slot in squadGrid.Slots)
                    if (slot.OccupantCard is HuntDepartureHunterCard3D card)
                        stagedIds.Add(card.Hunter.InstanceId);
                Assert.That(stagedIds, Is.EqualTo(new[] { third.InstanceId, first.InstanceId }));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PresentSquad_ClickInspectsHunterWithoutChangingSquadSlot()
        {
            var root = new GameObject("DepartureInspectionTestRoot");
            try
            {
                HunterInstance first = CreateHunter(111, "甲");
                HunterInstance second = CreateHunter(112, "乙");
                TabletopHuntDeparturePanel3D panel = TabletopHuntDeparturePanel3D.Create(root.transform);
                panel.PresentSquad(Vector3.zero, new[] { first, second }, new[] { first.InstanceId }, _ => { }, () => { });
                HuntDepartureHunterCard3D firstCard = FindCard(panel, first.InstanceId);
                CardSlot originalSlot = firstCard.CurrentSlot;

                InvokeCardMethod(firstCard, "OnMouseDown");
                InvokeCardMethod(firstCard, "OnMouseUp");

                Assert.That(panel.InspectedHunterId, Is.EqualTo(first.InstanceId));
                Assert.That(firstCard.CurrentSlot, Is.SameAs(originalSlot));
                Assert.That(GetSquadIds(panel), Is.EqualTo(new[] { first.InstanceId }));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PresentSquad_DragDoesNotReplaceInspectedHunter()
        {
            var root = new GameObject("DepartureDragInspectionTestRoot");
            try
            {
                HunterInstance first = CreateHunter(121, "甲");
                HunterInstance second = CreateHunter(122, "乙");
                TabletopHuntDeparturePanel3D panel = TabletopHuntDeparturePanel3D.Create(root.transform);
                panel.PresentSquad(Vector3.zero, new[] { first, second }, new[] { first.InstanceId }, _ => { }, () => { });
                HuntDepartureHunterCard3D firstCard = FindCard(panel, first.InstanceId);
                HuntDepartureHunterCard3D secondCard = FindCard(panel, second.InstanceId);
                CardSlot secondOriginalSlot = secondCard.CurrentSlot;
                InvokeCardMethod(firstCard, "OnMouseDown");
                InvokeCardMethod(firstCard, "OnMouseUp");

                InvokeCardMethod(secondCard, "OnMouseDown");
                InvokeCardMethod(secondCard, "BeginDrag");
                InvokeCardMethod(secondCard, "OnMouseUp");

                Assert.That(panel.InspectedHunterId, Is.EqualTo(first.InstanceId));
                Assert.That(secondCard.CurrentSlot, Is.SameAs(secondOriginalSlot));
                Assert.That(GetSquadIds(panel), Is.EqualTo(new[] { first.InstanceId }));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HunterPresentation_BoundsDecisionDetailsAndShowsSignedEquipmentNoise()
        {
            var createdItems = new List<ItemData>();
            try
            {
                HunterInstance hunter = CreateHunter(131, "整备猎人");
                hunter.HP.head = 1;
                hunter.HP.arms = 2;
                hunter.Traits.AddRange(new[] { "异常冗长的第一特性名称", "敏锐", "守望者", "隐藏特性" });
                hunter.Equipment.Add(new ItemInstance(CreateItem("嘈杂长矛", 3, createdItems)));
                hunter.Equipment.Add(new ItemInstance(CreateItem("静音斗篷", -5, createdItems)));
                hunter.Equipment.Add(new ItemInstance(CreateItem("药包", 0, createdItems)));
                hunter.Equipment.Add(new ItemInstance(CreateItem("不会完整显示的第四件装备", 0, createdItems)));

                HuntDepartureHunterPresentation presentation = HuntDepartureHunterPresentation.Create(hunter);

                Assert.That(presentation.Title, Is.EqualTo("整备猎人 · 出猎整备"));
                Assert.That(presentation.Body, Does.Contain("头 1/2"));
                Assert.That(presentation.Body, Does.Contain("臂 2/3"));
                Assert.That(presentation.Body, Does.Contain("特性 · 异常冗长的第一特性名…、敏锐、守望者 +1"));
                Assert.That(presentation.Body, Does.Contain("装备 · 嘈杂长矛、静音斗篷、药包 +1"));
                Assert.That(presentation.Body, Does.Contain("装备噪音 -2"));
                Assert.That(presentation.Body, Does.Not.Contain("第四件装备"));
            }
            finally
            {
                foreach (ItemData item in createdItems)
                    Object.DestroyImmediate(item);
            }
        }

        private static HunterInstance CreateHunter(int instanceId, string hunterName) => new(null, instanceId) { Name = hunterName };

        private static ItemData CreateItem(string itemName, int huntNoise, ICollection<ItemData> createdItems)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.name = itemName;
            item.itemName = itemName;
            item.ConfigureHuntNoise(huntNoise);
            createdItems.Add(item);
            return item;
        }

        private static HuntDepartureHunterCard3D FindCard(TabletopHuntDeparturePanel3D panel, int hunterId)
        {
            FieldInfo field = typeof(TabletopHuntDeparturePanel3D).GetField("hunterCards", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var cards = (List<HuntDepartureHunterCard3D>)field.GetValue(panel);
            return cards.Find(card => card != null && card.Hunter?.InstanceId == hunterId);
        }

        private static List<int> GetSquadIds(TabletopHuntDeparturePanel3D panel)
        {
            FieldInfo field = typeof(TabletopHuntDeparturePanel3D).GetField("squadGrid", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var grid = (SlotGrid)field.GetValue(panel);
            var hunterIds = new List<int>();
            foreach (CardSlot slot in grid.Slots)
                if (slot.OccupantCard is HuntDepartureHunterCard3D card)
                    hunterIds.Add(card.Hunter.InstanceId);
            return hunterIds;
        }

        private static void InvokeCardMethod(CardView3D card, string methodName)
        {
            MethodInfo method = typeof(CardView3D).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(card, null);
        }
    }
}
