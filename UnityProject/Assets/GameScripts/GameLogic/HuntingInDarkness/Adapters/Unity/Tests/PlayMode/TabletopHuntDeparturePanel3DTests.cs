using System.Collections.Generic;
using System.Reflection;
using Cards3D;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;
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

        [Test]
        public void PresentDestinations_LockedFirstSelectsFirstAvailableAndRejectsLockedClick()
        {
            var root = new GameObject("LockedDestinationPanelTestRoot");
            try
            {
                PlayableHuntDestination locked = CreateDestination("locked-route", "锁定路线");
                PlayableHuntDestination available = CreateDestination("available-route", "可用路线");
                PlayableHuntDestination confirmedDestination = null;
                var projections = new[]
                {
                    new PlayableHuntDestinationAvailability(locked, false, "第 2 年后才能前往。"),
                    new PlayableHuntDestinationAvailability(available, true, string.Empty)
                };
                TabletopHuntDeparturePanel3D panel = TabletopHuntDeparturePanel3D.Create(root.transform);
                panel.PresentDestinationProjections(Vector3.zero, projections, null, 0, string.Empty, destination => confirmedDestination = destination, () => { }, () => { });

                Assert.That(panel.SelectedDestinationIndex, Is.EqualTo(1));
                Assert.That(panel.AvailableDestinationCount, Is.EqualTo(1));
                TabletopEventChoiceCard3D lockedCard = FindDestinationCard(panel, "锁定路线");
                TabletopEventChoiceCard3D availableCard = FindDestinationCard(panel, "可用路线");
                Assert.That(lockedCard.IsInteractable, Is.False);
                Assert.That(availableCard.IsInteractable, Is.True);

                lockedCard.Clicked?.Invoke();
                Assert.That(panel.SelectedDestinationIndex, Is.EqualTo(1));
                Assert.That(confirmedDestination, Is.Null);

                FindDestinationCard(panel, "确认出发").Clicked?.Invoke();
                Assert.That(confirmedDestination, Is.SameAs(available));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PresentDestinations_AllLockedDisablesConfirmation()
        {
            var root = new GameObject("AllLockedDestinationPanelTestRoot");
            try
            {
                var projections = new[]
                {
                    new PlayableHuntDestinationAvailability(CreateDestination("locked-a", "锁定甲"), false, "第 2 年后才能前往。"),
                    new PlayableHuntDestinationAvailability(CreateDestination("locked-b", "锁定乙"), false, "第 3 年后才能前往。")
                };
                bool confirmed = false;
                TabletopHuntDeparturePanel3D panel = TabletopHuntDeparturePanel3D.Create(root.transform);
                panel.PresentDestinationProjections(Vector3.zero, projections, null, 0, string.Empty, _ => confirmed = true, () => { }, () => { });

                Assert.That(panel.SelectedDestinationIndex, Is.EqualTo(-1));
                Assert.That(panel.AvailableDestinationCount, Is.Zero);
                Assert.That(FindDestinationCard(panel, "锁定甲").IsInteractable, Is.False);
                TabletopEventChoiceCard3D confirmCard = FindDestinationCard(panel, "确认出发");
                Assert.That(confirmCard.IsInteractable, Is.False);
                confirmCard.Clicked?.Invoke();
                Assert.That(confirmed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
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

        private static TabletopEventChoiceCard3D FindDestinationCard(TabletopHuntDeparturePanel3D panel, string title)
        {
            foreach (TabletopEventChoiceCard3D card in panel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true))
                if (card.DisplayName == title)
                    return card;
            Assert.Fail($"找不到目的地卡：{title}");
            return null;
        }

        private static PlayableHuntDestination CreateDestination(string destinationId, string displayName)
        {
            var destination = new PlayableHuntDestination();
            SetPrivateField(destination, "destinationId", destinationId);
            SetPrivateField(destination, "displayName", displayName);
            SetPrivateField(destination, "description", "测试路线");
            SetPrivateField(destination, "resourceHint", "测试资源");
            SetPrivateField(destination, "dangerHint", "测试风险");
            return destination;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
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
