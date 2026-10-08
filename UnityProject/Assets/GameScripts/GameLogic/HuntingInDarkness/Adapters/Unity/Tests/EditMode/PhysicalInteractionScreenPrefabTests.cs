using HuntingInDarkness.ViewLayer.Presentation;
using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PhysicalInteractionScreenPrefabTests
    {
        private const string D6Path = "Assets/AssetRaw/UI/Prefabs/PhysicalDieD6.prefab";
        private const string D10Path = "Assets/AssetRaw/UI/Prefabs/PhysicalDieD10.prefab";
        private const string ScreenPath = "Assets/AssetRaw/UI/Prefabs/PhysicalInteractionScreen.prefab";

        [TestCase(D6Path, 6)]
        [TestCase(D10Path, 10)]
        public void DiePrefabRetainsBodyAndFaceMapping(string path, int sides)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(asset, Is.Not.Null);
            PhysicalDie3D prefabDie = asset.GetComponent<PhysicalDie3D>();
            Assert.That(prefabDie, Is.Not.Null);
            PhysicalDie3D die = Object.Instantiate(prefabDie);
            try
            {
                Assert.That(die.Body, Is.Not.Null);
                Assert.That(die.Sides, Is.EqualTo(sides));
                Assert.That(die.GetUpwardValue(), Is.InRange(1, sides));
                if (sides == 6)
                {
                    Assert.That(die.GetUpwardValue(), Is.EqualTo(1));
                    die.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                    Assert.That(die.GetUpwardValue(), Is.EqualTo(3));
                }
            }
            finally
            {
                Object.DestroyImmediate(die.gameObject);
            }
        }

        [Test]
        public void DetachingStageFromScaledCanvasRestoresUnitPhysicsScale()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath);
            Assert.That(asset, Is.Not.Null);
            GameObject screenObject = Object.Instantiate(asset);
            try
            {
                var serialized = new UnityEditor.SerializedObject(screenObject.GetComponent<PhysicalInteractionScreenView>());
                Assert.That(serialized.FindProperty("resultScrollRect").objectReferenceValue, Is.Not.Null);
                screenObject.transform.localScale = Vector3.one * 0.5f;
                PhysicalInteractionScreenView view = screenObject.GetComponent<PhysicalInteractionScreenView>();
                Assert.That(view, Is.Not.Null);
                view.DetachStageForStandalonePresentation();
                Assert.That(view.StageRoot.lossyScale, Is.EqualTo(Vector3.one));
                Assert.That(view.StageRoot.position, Is.EqualTo(new Vector3(0f, -100f, 0f)));
            }
            finally
            {
                Object.DestroyImmediate(screenObject);
            }
        }

        [Test]
        public void CardSessionLayoutAndShuffleSettingsAreSerializedOnTheScreenPrefab()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath);
            var serialized = new SerializedObject(asset.GetComponent<PhysicalInteractionScreenView>());
            Assert.That(serialized.FindProperty("cardsPerPage").intValue, Is.EqualTo(12));
            Assert.That(serialized.FindProperty("cardColumns").intValue, Is.EqualTo(6));
            Assert.That(serialized.FindProperty("cardRowSpacing").floatValue, Is.EqualTo(1.08f));
            Assert.That(serialized.FindProperty("cardFramePadding").floatValue, Is.EqualTo(1.15f));
            Assert.That(serialized.FindProperty("cardCameraLocalPosition").vector3Value, Is.EqualTo(new Vector3(0f, 6f, -2f)));
            Assert.That(serialized.FindProperty("cardSize").vector2Value, Is.EqualTo(new Vector2(0.6f, 0.9f)));
            Assert.That(serialized.FindProperty("shuffleDuration").floatValue, Is.EqualTo(0.3f));
        }

        [Test]
        public void ScreenDieScaleMatchesTheOriginalPhysicalFactoryColliderSize()
        {
            GameObject prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(D6Path);
            PhysicalDie3D prefab = prefabObject.GetComponent<PhysicalDie3D>();
            PhysicalDie3D screenDie = Object.Instantiate(prefab);
            PhysicalDie3D originalDie = PhysicalDie3D.Create(6, null, Vector3.zero, 0.34f, prefab.GetComponentInChildren<Renderer>().sharedMaterial);
            try
            {
                screenDie.transform.localScale = PhysicalDiceTabletopPresenter.CalculateScreenPrefabScale(prefab.transform.localScale, 0.34f, 0.34f);
                Physics.SyncTransforms();
                Assert.That(Vector3.Distance(screenDie.Body.GetComponent<Collider>().bounds.size, originalDie.Body.GetComponent<Collider>().bounds.size), Is.LessThan(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(screenDie.gameObject);
                Object.DestroyImmediate(originalDie.gameObject);
            }
        }
    }
}
