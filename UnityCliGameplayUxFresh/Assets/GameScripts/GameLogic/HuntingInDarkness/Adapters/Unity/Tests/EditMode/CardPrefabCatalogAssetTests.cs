using System;
using System.Reflection;
using Cards3D;
using HuntingInDarkness.Bootstrap;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class CardPrefabCatalogAssetTests
    {
        private const string SettingsPath = "Assets/AssetRaw/Configs/HuntingInDarkness/PlayableBootstrapSettings.asset";
        private const string CatalogPath = "Assets/AssetRaw/Configs/HuntingInDarkness/CardPrefabCatalog.asset";

        [Test]
        public void StarterCatalog_IsAssignedAndContainsEditableCoreCardPrefabs()
        {
            UnityEngine.Object settingsAsset = AssetDatabase.LoadMainAssetAtPath(SettingsPath);
            PlayableBootstrapSettings settings = settingsAsset as PlayableBootstrapSettings;
            CardPrefabCatalog catalog = AssetDatabase.LoadAssetAtPath<CardPrefabCatalog>(CatalogPath);

            Assert.That(settingsAsset, Is.Not.Null, SettingsPath);
            Assert.That(settings, Is.Not.Null, $"{SettingsPath} loaded as {settingsAsset.GetType().FullName}");
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            Assert.That(settings.CardPrefabs, Is.SameAs(catalog));
            Assert.That(catalog.TryGetPrefab(out ResourceCard3D resource), Is.True);
            Assert.That(catalog.TryGetPrefab(out WorkshopCard3D workshop), Is.True);
            Assert.That(catalog.TryGetPrefab(out WorkshopRecipeCard3D recipe), Is.True);
            AssertDimensions(catalog, typeof(ResourceCard3D), 0.75f, 1.05f, 0.025f);
            AssertDimensions(catalog, typeof(WorkshopCard3D), 0.75f, 1.05f, 0.025f);
            AssertDimensions(catalog, typeof(WorkshopRecipeCard3D), 1.25f, 1.8f, 0.025f);
            AssertManualFontControl(resource);
            AssertManualFontControl(workshop);
            AssertManualFontControl(recipe);
        }

        [Test]
        public void PrefabInspectorAction_AppliesCatalogBodyAndColliderDimensions()
        {
            CardPrefabCatalog catalog = AssetDatabase.LoadAssetAtPath<CardPrefabCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            MethodInfo applyMethod = typeof(CardView3D).GetMethod("ApplyCatalogDimensionsInEditor", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(applyMethod, Is.Not.Null);
            string[] prefabPaths =
            {
                "Assets/Prefabs/HuntingInDarkness/Cards/ResourceCard3D.prefab",
                "Assets/Prefabs/HuntingInDarkness/Cards/WorkshopCard3D.prefab",
                "Assets/Prefabs/HuntingInDarkness/Cards/WorkshopRecipeCard3D.prefab"
            };

            foreach (string prefabPath in prefabPaths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    CardView3D card = root.GetComponent<CardView3D>();
                    Assert.That(card, Is.Not.Null, prefabPath);
                    Assert.That(catalog.TryGetDimensions(card.GetType(), out CardDimensions dimensions), Is.True, prefabPath);
                    Renderer body = root.transform.Find("Body")?.GetComponent<Renderer>();
                    Assert.That(body, Is.Not.Null, prefabPath);
                    body.transform.localScale = Vector3.one;
                    BoxCollider collider = root.GetComponent<BoxCollider>();
                    Assert.That(collider, Is.Not.Null, prefabPath);
                    collider.size = Vector3.one;
                    collider.center = Vector3.one;

                    applyMethod.Invoke(card, null);

                    Assert.That(body.transform.localScale, Is.EqualTo(new Vector3(dimensions.Width, dimensions.Depth, dimensions.Height)), prefabPath);
                    Assert.That(collider.size, Is.EqualTo(new Vector3(dimensions.Width, dimensions.Depth, dimensions.Height)), prefabPath);
                    Assert.That(collider.center, Is.EqualTo(body.transform.localPosition), prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void AssertDimensions(CardPrefabCatalog catalog, Type cardType, float width, float height, float depth)
        {
            CardDimensions dimensions = catalog.GetDimensions(cardType, default);
            Assert.That(dimensions.Width, Is.EqualTo(width).Within(0.001f), cardType.Name);
            Assert.That(dimensions.Height, Is.EqualTo(height).Within(0.001f), cardType.Name);
            Assert.That(dimensions.Depth, Is.EqualTo(depth).Within(0.001f), cardType.Name);
        }

        private static void AssertManualFontControl(CardView3D prefab)
        {
            TextMeshPro[] textFields = prefab.GetComponentsInChildren<TextMeshPro>(true);
            Assert.That(textFields, Is.Not.Empty, prefab.name);
            foreach (TextMeshPro text in textFields)
                Assert.That(text.enableAutoSizing, Is.False, $"{prefab.name}/{text.gameObject.name}");
        }
    }
}
