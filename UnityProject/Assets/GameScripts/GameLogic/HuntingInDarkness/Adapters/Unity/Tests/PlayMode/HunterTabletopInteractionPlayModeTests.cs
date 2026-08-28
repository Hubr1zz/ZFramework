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
    public sealed class HunterTabletopInteractionPlayModeTests
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

        [Test]
        public void PointerRelease_InvokesClickOnlyWhenNoDragStarted()
        {
            var root = new GameObject("PointerIntentTest");
            createdObjects.Add(root);
            TestSlotCard card = root.AddComponent<TestSlotCard>();
            int clickCount = 0;
            card.OnClicked += _ => clickCount++;

            card.HandlePointerDown(Vector2.zero);
            Assert.That(clickCount, Is.Zero);
            card.HandlePointerDrag(Vector2.right * 4f, Vector3.right);
            card.HandlePointerUp();
            Assert.That(clickCount, Is.EqualTo(1));

            card.EnableDrag = true;
            card.HandlePointerDown(Vector2.zero);
            card.HandlePointerDrag(Vector2.right * 10f, Vector3.right);
            card.HandlePointerUp();
            Assert.That(clickCount, Is.EqualTo(1));
        }

        [Test]
        public void PhysicalPointerProjection_HitsColliderAndMovesCardOnSharedDragPath()
        {
            var cameraObject = new GameObject("PointerProjectionCamera");
            createdObjects.Add(cameraObject);
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            cameraObject.transform.SetPositionAndRotation(Vector3.up * 10f, Quaternion.Euler(90f, 0f, 0f));

            var cardObject = new GameObject("PhysicalPointerCard");
            createdObjects.Add(cardObject);
            TestSlotCard card = cardObject.AddComponent<TestSlotCard>();
            card.Initialize(Vector3.zero);
            card.EnableDrag = true;
            int dragStartedCount = 0;
            int dragEndedCount = 0;
            card.DragStarted += _ => dragStartedCount++;
            card.DragEnded += _ => dragEndedCount++;

            Physics.SyncTransforms();
            Vector2 pointerDown = camera.WorldToScreenPoint(card.transform.position);
            Assert.That(Physics.Raycast(camera.ScreenPointToRay(pointerDown), out RaycastHit hit), Is.True);
            Assert.That(hit.collider.GetComponentInParent<CardView3D>(), Is.SameAs(card));

            Vector3 target = Vector3.right * 2f;
            Vector2 pointerDrag = camera.WorldToScreenPoint(target);
            Assert.That(Vector2.Distance(pointerDown, pointerDrag), Is.GreaterThan(5f));
            card.HandlePointerDown(pointerDown);
            card.HandlePointerDrag(pointerDrag);
            card.HandlePointerUp();

            Assert.That(dragStartedCount, Is.EqualTo(1));
            Assert.That(dragEndedCount, Is.EqualTo(1));
            Assert.That(card.transform.position.x, Is.EqualTo(target.x).Within(0.01f));
        }

        [Test]
        public void HunterCard_OpensDossierOnClickButNotOnDrag()
        {
            var root = new GameObject("HunterPointerIntentTest");
            createdObjects.Add(root);
            HunterData template = CreateTemplate("pointer_hunter", "指针猎人");
            var hunter = new HunterInstance(template, 701);
            TestHunterCard card = root.AddComponent<TestHunterCard>();
            card.Init(hunter);
            int dossierCount = 0;
            card.OnHunterClicked = _ => dossierCount++;

            card.HandlePointerDown(Vector2.zero);
            card.HandlePointerUp();
            Assert.That(dossierCount, Is.EqualTo(1));

            card.EnableDrag = true;
            card.HandlePointerDown(Vector2.zero);
            card.HandlePointerDrag(Vector2.right * 10f, Vector3.right);
            card.HandlePointerUp();
            Assert.That(dossierCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Fill_PreservesRetainedCardAndExternalSquadSlot()
        {
            var root = new GameObject("HunterZoneLayoutTest");
            createdObjects.Add(root);
            SlotGrid rosterGrid = SlotGrid.Create(root.transform, Vector3.zero, 3, 1, CardView3D.CW + 0.06f, CardView3D.CH + 0.06f, 0.1f, false, CardCategory.HunterProfile);
            SlotGrid squadGrid = SlotGrid.Create(root.transform, Vector3.forward * 2f, 1, 1, CardView3D.CW + 0.06f, CardView3D.CH + 0.06f, 0.1f, false, CardCategory.HunterProfile);
            HunterZone zone = root.AddComponent<HunterZone>();
            zone.SetRefs(rosterGrid);
            HunterInstance first = CreateHunter(711, "初始猎人");
            HunterInstance removed = CreateHunter(712, "离队猎人");
            zone.Fill(new List<HunterInstance> { first, removed });
            HunterCard3D retainedCard = FindCard(zone, 711);
            HunterCard3D removedCard = FindCard(zone, 712);
            retainedCard.CurrentSlot.ClearCard();
            squadGrid.Slots[0].PlaceCard(retainedCard, root.transform);

            HunterInstance refreshed = CreateHunter(711, "更新猎人");
            HunterInstance added = CreateHunter(713, "新增猎人");
            zone.Fill(new List<HunterInstance> { refreshed, added });

            Assert.That(FindCard(zone, 711), Is.SameAs(retainedCard));
            Assert.That(retainedCard.Hunter, Is.SameAs(refreshed));
            Assert.That(retainedCard.CurrentSlot, Is.SameAs(squadGrid.Slots[0]));
            Assert.That(squadGrid.Slots[0].OccupantCard, Is.SameAs(retainedCard));
            Assert.That(FindCard(zone, 713).CurrentSlot, Is.SameAs(rosterGrid.Slots[0]));
            Assert.That(FindCard(zone, 712), Is.Null);
            yield return null;
            Assert.That(removedCard == null, Is.True);

            zone.Fill(new List<HunterInstance> { added });
            Assert.That(squadGrid.Slots[0].OccupantCard, Is.Null);
            yield return null;
            Assert.That(retainedCard == null, Is.True);
        }

        [UnityTest]
        public IEnumerator Fill_DuringDragAppliesOnlyLatestSnapshotAfterDragEnds()
        {
            var root = new GameObject("HunterZoneDeferredRefreshTest");
            createdObjects.Add(root);
            SlotGrid grid = SlotGrid.Create(root.transform, Vector3.zero, 2, 1, CardView3D.CW + 0.06f, CardView3D.CH + 0.06f, 0.1f, false, CardCategory.HunterProfile);
            HunterZone zone = root.AddComponent<HunterZone>();
            zone.SetRefs(grid);
            HunterInstance first = CreateHunter(721, "拖拽猎人");
            zone.Fill(new List<HunterInstance> { first });
            HunterCard3D draggedCard = FindCard(zone, 721);
            InvokeCardMethod(draggedCard, "BeginDrag");

            zone.Fill(new List<HunterInstance> { CreateHunter(722, "中间快照") });
            zone.Fill(new List<HunterInstance> { CreateHunter(723, "最终快照") });

            Assert.That(FindCard(zone, 721), Is.SameAs(draggedCard));
            Assert.That(FindCard(zone, 722), Is.Null);
            Assert.That(FindCard(zone, 723), Is.Null);

            InvokeCardMethod(draggedCard, "EndDrag");

            Assert.That(FindCard(zone, 721), Is.Null);
            Assert.That(FindCard(zone, 722), Is.Null);
            Assert.That(FindCard(zone, 723), Is.Not.Null);
            yield return null;
            Assert.That(draggedCard == null, Is.True);
        }

        private HunterInstance CreateHunter(int id, string hunterName)
        {
            HunterData template = CreateTemplate($"hunter_{id}", hunterName);
            return new HunterInstance(template, id);
        }

        private HunterData CreateTemplate(string id, string hunterName)
        {
            HunterData template = ScriptableObject.CreateInstance<HunterData>();
            template.name = id;
            template.hunterName = hunterName;
            createdObjects.Add(template);
            return template;
        }

        private static HunterCard3D FindCard(HunterZone zone, int hunterId)
        {
            FieldInfo field = typeof(HunterZone).GetField("_cards", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var cards = (List<HunterCard3D>)field.GetValue(zone);
            return cards.Find(card => card != null && card.Hunter != null && card.Hunter.InstanceId == hunterId);
        }

        private static void InvokeCardMethod(CardView3D card, string methodName)
        {
            MethodInfo method = typeof(CardView3D).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(card, null);
        }

        private sealed class TestSlotCard : SlotDraggableCardView3D
        {
            public void Initialize(Vector3 localPosition) => InitView(localPosition);
            protected override void BuildTextFields() { }
            protected override void ApplyVisuals() { }
        }

        private sealed class TestHunterCard : HunterCard3D { }
    }
}
