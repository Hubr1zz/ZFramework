using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cards3D;
using HuntingInDarkness.Data;
using NUnit.Framework;
using UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class ResourceZoneLayoutPlayModeTests
    {
        private GameObject root;
        private SlotGrid grid;
        private ResourceZone zone;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ResourceZoneLayoutTest");
            grid = SlotGrid.Create(root.transform, Vector3.zero, 3, 1, CardView3D.CW + 0.06f, CardView3D.CH + 0.06f, 0.1f, false, CardCategory.Resource);
            zone = root.AddComponent<ResourceZone>();
            zone.SetRefs(grid);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Synchronize_PreservesRetainedLayoutWhileRemovingAndAddingCards()
        {
            zone.Synchronize(Resources(("wood", 2), ("stone", 1)));
            ResourceCard3D wood = FindCard("wood");
            ResourceCard3D stone = FindCard("stone");
            CardSlot retainedSlot = grid.Slots[2];
            wood.CurrentSlot.ClearCard();
            retainedSlot.PlaceCard(wood, root.transform);

            zone.Synchronize(Resources(("wood", 4), ("bone", 3)));
            ResourceCard3D bone = FindCard("bone");

            Assert.That(FindCard("wood"), Is.SameAs(wood));
            Assert.That(wood.CurrentSlot, Is.SameAs(retainedSlot));
            Assert.That(wood.DisplayName, Does.EndWith("×4"));
            Assert.That(bone, Is.Not.Null);
            Assert.That(bone.CurrentSlot, Is.SameAs(grid.Slots[0]));
            Assert.That(FindCard("stone"), Is.Null);
            yield return null;
            Assert.That(stone == null, Is.True);
        }

        [UnityTest]
        public IEnumerator Synchronize_DuringDragAppliesOnlyAfterDragEnds()
        {
            zone.Synchronize(Resources(("wood", 2)));
            ResourceCard3D wood = FindCard("wood");
            InvokeDrag(wood, "BeginDrag");

            zone.Synchronize(Resources(("stone", 1)));
            zone.Synchronize(Resources(("bone", 2)));

            Assert.That(FindCard("wood"), Is.SameAs(wood));
            Assert.That(FindCard("stone"), Is.Null);
            Assert.That(FindCard("bone"), Is.Null);

            InvokeDrag(wood, "EndDrag");

            Assert.That(FindCard("wood"), Is.Null);
            Assert.That(FindCard("stone"), Is.Null);
            Assert.That(FindCard("bone"), Is.Not.Null);
            yield return null;
            Assert.That(wood == null, Is.True);
        }

        private ResourceCard3D FindCard(string resourceId)
        {
            FieldInfo field = typeof(ResourceZone).GetField("_cards", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var cards = (List<ResourceCard3D>)field.GetValue(zone);
            return cards.Find(card => card != null && card.ResourceId == resourceId);
        }

        private static List<ResourceEntry> Resources(params (string id, int amount)[] values)
        {
            var resources = new List<ResourceEntry>();
            foreach ((string id, int amount) value in values)
                resources.Add(new ResourceEntry { Key = value.id, Value = value.amount });
            return resources;
        }

        private static void InvokeDrag(CardView3D card, string methodName)
        {
            MethodInfo method = typeof(CardView3D).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(card, null);
        }
    }
}
