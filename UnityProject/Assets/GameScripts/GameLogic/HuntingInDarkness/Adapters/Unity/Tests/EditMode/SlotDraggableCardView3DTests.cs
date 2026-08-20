using Cards3D;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class SlotDraggableCardView3DTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp() => root = new GameObject("DragTestRoot");

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            CardSlot.AllSlots.RemoveAll(slot => slot == null);
            CardView3D.AllCards.RemoveAll(card => card == null);
        }

        [Test]
        public void InvalidDrop_RestoresOriginalSlotOccupancy()
        {
            CardSlot slot = CardSlot.Create(root.transform, Vector3.zero, CardView3D.CW, CardView3D.CH, false, CardCategory.Resource);
            TestCard card = TestCard.Create(root.transform);
            slot.PlaceCard(card);

            card.SimulateBeginDrag();
            Assert.That(slot.OccupantCard, Is.Null);

            card.SimulateEndDrag();

            Assert.That(slot.OccupantCard, Is.SameAs(card));
            Assert.That(card.CurrentSlot, Is.SameAs(slot));
        }

        [Test]
        public void ReadOnlySlot_DisablesCardDrag()
        {
            CardSlot slot = CardSlot.Create(root.transform, Vector3.zero, CardView3D.CW, CardView3D.CH, false, CardCategory.Equipment);
            slot.AllowOccupantDrag = false;
            TestCard card = TestCard.Create(root.transform, CardCategory.Equipment);

            slot.PlaceCard(card);

            Assert.That(card.EnableDrag, Is.False);
        }

        private sealed class TestCard : SlotDraggableCardView3D
        {
            private CardCategory category;

            public static TestCard Create(Transform parent, CardCategory category = CardCategory.Resource)
            {
                var gameObject = new GameObject("TestCard");
                gameObject.transform.SetParent(parent, false);
                var card = gameObject.AddComponent<TestCard>();
                card.category = category;
                card.InitView(Vector3.zero);
                return card;
            }

            public void SimulateBeginDrag()
            {
                _preDragParent = transform.parent;
                _preDragLocalPos = transform.localPosition;
                transform.SetParent(null, true);
                OnBeginDrag();
            }

            public void SimulateEndDrag() => OnEndDrag();

            protected override CardCategory GetDefaultCategory() => category;
            protected override void BuildTextFields() { }
            protected override void ApplyVisuals() { }
        }
    }
}
