using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cards3D;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class CardView3DFactoryPositionTests
    {
        [TearDown]
        public void TearDown() => CardPrefabRegistry.Configure(null);

        [UnityTest]
        public IEnumerator Factories_PlaceDirectPanelCardsAtRequestedLocalPositions()
        {
            var root = new GameObject("CardFactoryPositionRoot");
            HunterData template = ScriptableObject.CreateInstance<HunterData>();
            template.hunterName = "Extremely Long Candidate Hunter Name For Card Boundary Test";
            var hunter = new HunterInstance(template, 1) { Name = "Extremely Long Recovery Hunter Name" };
            Vector3 recruitmentPosition = new(-1.4f, 0.03f, 0.45f);
            Vector3 recoveryPosition = new(0.8f, 0.03f, -0.05f);
            Vector3 launcherPosition = new(-3.25f, 0.03f, -2.65f);

            RecruitmentTemplateCard3D recruitment = RecruitmentTemplateCard3D.Create(template, root.transform, recruitmentPosition);
            HunterRecoveryCard3D recovery = HunterRecoveryCard3D.Create(hunter, HunterBodyPart.Torso, root.transform, recoveryPosition);
            RecruitmentLauncherCard3D launcher = RecruitmentLauncherCard3D.Create(root.transform, launcherPosition);

            Assert.That(recruitment.transform.localPosition, Is.EqualTo(recruitmentPosition));
            Assert.That(recovery.transform.localPosition, Is.EqualTo(recoveryPosition));
            Assert.That(launcher.transform.localPosition, Is.EqualTo(launcherPosition));
            AssertTextStaysInsideCard(recruitment);
            AssertTextStaysInsideCard(recovery);
            AssertTextStaysInsideCard(launcher);

            Object.Destroy(root);
            Object.Destroy(template);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EventCardInspection_ReturnsCompleteUntruncatedText()
        {
            var root = new GameObject("CardInspectionRoot");
            TabletopEventPrimaryCard3D card = TabletopEventPrimaryCard3D.Create(root.transform);
            const string fullBody = "这是一段超过实体卡可视区域时仍应由详情层完整读取的叙事正文，包括结果、条件与后续影响。";
            card.Present("废墟回声", fullBody, "按 F 查看完整信息", TabletopEventPrimaryTone.Narrative);

            Assert.That(card.TryGetInspectionContent(out CardInspectionContent content), Is.True);
            Assert.That(content.Title, Is.EqualTo("废墟回声"));
            Assert.That(content.Body, Does.Contain(fullBody));
            TextMeshPro bodyText = null;
            foreach (TextMeshPro text in card.GetComponentsInChildren<TextMeshPro>(true))
                if (text.gameObject.name == "Body")
                    bodyText = text;
            Assert.That(bodyText, Is.Not.Null);
            bodyText.fontSize = 0.3f;
            bodyText.ForceMeshUpdate(true, true);
            Assert.That(bodyText.fontSize, Is.EqualTo(0.3f).Within(0.001f));
            AssertTextStaysInsideCard(card);

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EventPrimaryCard_ClickOpensAndDestroyClosesInspection()
        {
            var cameraObject = new GameObject("InspectionCamera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            GameObject overlayRoot = TabletopUsabilityPlayModeTests.CreateOverlay(camera);
            var root = new GameObject("ClickableEventCardRoot");
            TabletopEventPrimaryCard3D card = TabletopEventPrimaryCard3D.Create(root.transform);
            card.Present("旧路标", "点击主叙事卡应打开完整内容，而不是要求玩家猜测隐藏文字。", "详情", TabletopEventPrimaryTone.Narrative);

            try
            {
                card.HandlePointerDown(Vector2.zero);
                card.HandlePointerUp();
                Assert.That(CardInspectionOverlay.BlocksWorldInput, Is.True);
            }
            finally
            {
                Object.Destroy(root);
                TabletopUsabilityPlayModeTests.DestroyOverlay(overlayRoot);
                Object.Destroy(cameraObject);
            }

            yield return null;
            Assert.That(CardInspectionOverlay.BlocksWorldInput, Is.False);
        }

        [UnityTest]
        public IEnumerator ResourceFactory_UsesConfiguredTypePrefab()
        {
            var parent = new GameObject("PrefabFactoryParent");
            var prefabObject = new GameObject("ResourceCardPrefab");
            ResourceCard3D prefab = prefabObject.AddComponent<ResourceCard3D>();
            var marker = new GameObject("PrefabVisualMarker");
            marker.transform.SetParent(prefabObject.transform, false);
            CardPrefabCatalog catalog = ScriptableObject.CreateInstance<CardPrefabCatalog>();
            FieldInfo field = typeof(CardPrefabCatalog).GetField("cards", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(catalog, new List<CardPrefabDefinition> { new(prefab, new CardDimensions(1.4f, 1.8f, 0.04f)) });
            CardPrefabRegistry.Configure(catalog);

            ResourceCard3D card = ResourceCard3D.Create("bone", "骨", 2, parent.transform);
            yield return null;

            Assert.That(card.transform.Find("PrefabVisualMarker"), Is.Not.Null);
            Assert.That(card.Width, Is.EqualTo(1.4f).Within(0.001f));
            Assert.That(card.Height, Is.EqualTo(1.8f).Within(0.001f));
            Assert.That(card.Depth, Is.EqualTo(0.04f).Within(0.001f));
            Assert.That(card.transform.Find("Body").localScale, Is.EqualTo(new Vector3(1.4f, 0.04f, 1.8f)));
            Assert.That(card.GetComponent<BoxCollider>().size.x, Is.EqualTo(1.4f).Within(0.001f));
            Assert.That(card.GetComponent<BoxCollider>().size.z, Is.EqualTo(1.8f).Within(0.001f));
            Collider[] colliders = card.GetComponentsInChildren<Collider>(true);
            Assert.That(colliders, Has.Length.EqualTo(1));
            Assert.That(colliders[0].gameObject, Is.SameAs(card.gameObject));
            Bounds cardBounds = card.GetComponent<BoxCollider>().bounds;
            Bounds bodyBounds = card.transform.Find("Body").GetComponent<Renderer>().bounds;
            Assert.That(cardBounds.size.x, Is.EqualTo(bodyBounds.size.x).Within(0.001f));
            Assert.That(cardBounds.size.y, Is.EqualTo(bodyBounds.size.y).Within(0.001f));
            Assert.That(cardBounds.size.z, Is.EqualTo(bodyBounds.size.z).Within(0.001f));
            Object.Destroy(parent);
            Object.Destroy(prefabObject);
            Object.Destroy(catalog);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorkshopCard_DragMovesBetweenSlotsWithoutOpeningClickAction()
        {
            var root = new GameObject("WorkshopDragRoot");
            WorkshopCard3D card = WorkshopCard3D.Create("制骨坊", "制作骨制品", root.transform);
            CardSlot source = CardSlot.Create(root.transform, Vector3.zero, 1f, 1.3f, false, CardCategory.Workshop);
            CardSlot target = CardSlot.Create(root.transform, Vector3.right * 3f, 1.4f, 1.6f, false, CardCategory.Workshop);
            source.PlaceCard(card, root.transform);
            int clickCount = 0;
            card.OnCraftMenuRequested = _ => clickCount++;

            card.HandlePointerDown(Vector2.zero);
            card.HandlePointerDrag(new Vector2(10f, 0f), target.transform.position + new Vector3(0.65f, 0f, 0.75f));
            card.HandlePointerUp();

            Assert.That(source.OccupantCard, Is.Null);
            Assert.That(target.OccupantCard, Is.SameAs(card));
            Assert.That(card.CurrentSlot, Is.SameAs(target));
            Assert.That(clickCount, Is.Zero);

            Object.Destroy(root);
            yield return null;
        }

        private static void AssertTextStaysInsideCard(CardView3D card)
        {
            Renderer cardBody = card.transform.Find("Body").GetComponent<Renderer>();
            foreach (TextMeshPro text in card.GetComponentsInChildren<TextMeshPro>(true))
            {
                text.ForceMeshUpdate(true, true);
                Assert.That(text.enableAutoSizing, Is.False, text.gameObject.name);
                Bounds visibleText = text.GetComponent<Renderer>().bounds;
                Assert.That(visibleText.min.x, Is.GreaterThanOrEqualTo(cardBody.bounds.min.x - 0.01f), text.gameObject.name);
                Assert.That(visibleText.max.x, Is.LessThanOrEqualTo(cardBody.bounds.max.x + 0.01f), text.gameObject.name);
                Assert.That(visibleText.min.z, Is.GreaterThanOrEqualTo(cardBody.bounds.min.z - 0.01f), text.gameObject.name);
                Assert.That(visibleText.max.z, Is.LessThanOrEqualTo(cardBody.bounds.max.z + 0.01f), text.gameObject.name);
            }
        }
    }
}
