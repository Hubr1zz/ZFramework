using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cards3D;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class SettlementPresentationPlayModeTests
    {
        private const string RegistryPath = "Assets/AssetRaw/UI/Prefabs/TabletopPresentationAssets.prefab";
        private const string LayoutPath = "Assets/Prefabs/HuntingInDarkness/Settlement/SettlementTableVisuals.prefab";

        [UnityTest]
        public IEnumerator ReloadedPrefabWiresEntrancesAndMutuallyExclusiveAreas()
        {
            GameObject registryRoot = InstantiatePrefab(RegistryPath);
            GameObject layoutRoot = InstantiatePrefab(LayoutPath);
            try
            {
                yield return null;
                SettlementTableVisualLayout layout = layoutRoot.GetComponentInChildren<SettlementTableVisualLayout>(true);
                Assert.That(layout, Is.Not.Null);
                Assert.That(layout.HunterZone, Is.Not.Null);
                Assert.That(layout.ResourceZone, Is.Not.Null);
                Assert.That(layout.WorkshopZone, Is.Not.Null);
                Assert.That(layout.InventionZone, Is.Not.Null);
                Assert.That(layout.PreviewExpansion, Is.Not.Null);
                Assert.That(layout.InventionZone.PreviewRoot, Is.Not.Null);
                Assert.That(layout.InventionZone.PreviewGrid, Is.Not.Null);
                Assert.That(layout.InventionZone.DrawEntry, Is.Not.Null);
                Assert.That(layout.InventionZone.PreviewGrid.Columns, Is.EqualTo(4));
                Assert.That(layout.InventionZone.PreviewGrid.OccupantsDraggable, Is.False);
                CardView3D[] launchers =
                {
                    layout.RecruitmentLauncher,
                    layout.FacilityDutyLauncher,
                    layout.CampLedgerLauncher,
                    layout.DepartureLauncher,
                    layout.WorkshopExpansionLauncher,
                    layout.ResourceExpansionLauncher,
                    layout.InventionExpansionLauncher,
                    layout.PreviewExpansionLauncher
                };
                foreach (CardView3D launcher in launchers)
                {
                    Assert.That(launcher, Is.Not.Null);
                    Assert.That(launcher.GetComponentsInChildren<TMP_Text>(true), Is.Not.Empty, launcher.name);
                }

                CardView3D entrance = layout.RecruitmentLauncher;
                Assert.That(entrance, Is.Not.Null);
                Vector3 basePosition = entrance.transform.localPosition;
                Assert.That(entrance.BaseLocalPos, Is.EqualTo(basePosition));
                InvokeCardMethod(entrance, "OnMouseEnter");
                Assert.That(entrance.transform.localPosition.x, Is.EqualTo(basePosition.x).Within(0.001f));
                Assert.That(entrance.transform.localPosition.z, Is.EqualTo(basePosition.z).Within(0.001f));
                InvokeCardMethod(entrance, "OnMouseExit");

                SlotGrid resourceGrid = layout.ResourceZone.GetComponentInChildren<SlotGrid>(true);
                ResourceCard3D campCard = ResourceCard3D.Create("presentation_camp_resource", "营地测试资源", 2, resourceGrid.transform);
                Assert.That(resourceGrid.TryPlaceCard(campCard), Is.True);
                layout.ResourceExpansion.Open();
                Assert.That(layout.ResourceExpansion.IsOpen, Is.True);
                layout.WorkshopExpansion.Open();
                Assert.That(layout.ResourceExpansion.IsOpen, Is.False);
                Assert.That(layout.WorkshopExpansion.IsOpen, Is.True);
                layout.WorkshopExpansion.Close();
                CaptureLayout(layoutRoot, "camp.png");
            }
            finally
            {
                if (layoutRoot != null) UnityEngine.Object.Destroy(layoutRoot);
                if (registryRoot != null) UnityEngine.Object.Destroy(registryRoot);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExpansionCapacityPreservesCardsAndNoCandidatePreviewIsRendered()
        {
            GameObject registryRoot = InstantiatePrefab(RegistryPath);
            GameObject layoutRoot = InstantiatePrefab(LayoutPath);
            var createdAssets = new List<UnityEngine.Object>();
            try
            {
                yield return null;
                SettlementTableVisualLayout layout = layoutRoot.GetComponentInChildren<SettlementTableVisualLayout>(true);
                SlotGrid resourceGrid = layout.ResourceZone.GetComponentInChildren<SlotGrid>(true);
                Assert.That(resourceGrid, Is.Not.Null);
                ResourceCard3D firstCard = ResourceCard3D.Create("presentation_resource_0", "测试资源", 1, resourceGrid.transform);
                Assert.That(resourceGrid.TryPlaceCard(firstCard), Is.True);
                CardSlot firstSlot = resourceGrid.Slots[0];
                List<CardSlot> originalSlots = new(resourceGrid.Slots);
                resourceGrid.EnsureCapacityPreservingCards(24, resourceGrid.Columns);
                Assert.That(resourceGrid.Slots.Count, Is.GreaterThanOrEqualTo(24));
                Assert.That(resourceGrid.Slots[0], Is.SameAs(firstSlot));
                Assert.That(firstSlot.OccupantCard, Is.SameAs(firstCard));
                for (int index = 1; index < 24; index++)
                {
                    ResourceCard3D card = ResourceCard3D.Create($"presentation_resource_{index}", "测试资源", index + 1, resourceGrid.transform);
                    Assert.That(resourceGrid.TryPlaceCard(card), Is.True);
                }
                for (int index = 0; index < originalSlots.Count; index++)
                    Assert.That(resourceGrid.Slots[index], Is.SameAs(originalSlots[index]));
                layout.ResourceExpansion.Open();
                CaptureLayout(layoutRoot, "expanded-resource-24.png");

                InventionData mastered = CreateInvention("presentation_passive", "被动发明", createdAssets);
                mastered.unlockEffects.Add(new InventionPassiveEffect { lifetime = InventionEffectLifetime.Campaign, modifierId = "presentation:passive", kind = InventionEffectKind.ModifyStrength, value = 1 });
                InventionData candidate = CreateInvention("presentation_candidate", "可发明预览", createdAssets);
                ItemData cost = ScriptableObject.CreateInstance<ItemData>();
                cost.name = "presentation_cost";
                cost.ConfigureContentId("presentation_cost");
                cost.itemName = "测试材料";
                cost.itemType = ItemType.Resource;
                createdAssets.Add(cost);
                candidate.costs.Add(new InventionCost { resource = cost, count = 1 });
                var settlement = new SettlementInstance();
                settlement.UnlockInvention(mastered.ContentId);
                settlement.AddResource(cost, 3);
                var inventionSystem = new InventionSystem(settlement) { AllInventions = new List<InventionData> { mastered, candidate } };
                InventionZone inventionZone = layout.InventionZone;
                inventionZone.Fill(inventionSystem);
                yield return null;

                SlotGrid masteredGrid = inventionZone.MasteredGrid;
                InventionCard3D masteredCard = FindCard<InventionCard3D>(masteredGrid);
                Assert.That(masteredCard, Is.Not.Null);
                Assert.That(masteredCard.Data, Is.SameAs(mastered));
                int resourceBeforePreview = settlement.GetResource(cost);
                Assert.That(FindCard<InventionCard3D>(inventionZone.PreviewGrid), Is.Null);
                Assert.That(settlement.IsInventionUnlocked(candidate.ContentId), Is.False);
                Assert.That(settlement.GetResource(cost), Is.EqualTo(resourceBeforePreview));
                Assert.That(inventionZone.DrawnCount, Is.Zero);
                layout.InventionExpansion.Open();
                CaptureLayout(layoutRoot, "mastered-expanded.png");
            }
            finally
            {
                foreach (UnityEngine.Object asset in createdAssets)
                    if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
                if (layoutRoot != null) UnityEngine.Object.Destroy(layoutRoot);
                if (registryRoot != null) UnityEngine.Object.Destroy(registryRoot);
            }
            yield return null;
        }

        private static GameObject InstantiatePrefab(string path)
        {
#if UNITY_EDITOR
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, $"缺少持久化表现 Prefab：{path}");
            return UnityEngine.Object.Instantiate(prefab);
#else
            Assert.Fail($"只能在 Unity Editor 中加载 Prefab：{path}");
            return null;
#endif
        }

        private static T FindCard<T>(SlotGrid grid) where T : CardView3D
        {
            if (grid == null) return null;
            foreach (CardSlot slot in grid.Slots)
                if (slot?.OccupantCard is T card) return card;
            return null;
        }

        private static InventionData CreateInvention(string id, string title, List<UnityEngine.Object> createdAssets)
        {
            InventionData invention = ScriptableObject.CreateInstance<InventionData>();
            invention.name = id;
            invention.ConfigureContentId(id);
            invention.inventionName = title;
            invention.description = "表现测试文本";
            createdAssets.Add(invention);
            return invention;
        }

        private static void InvokeCardMethod(CardView3D card, string methodName)
        {
            MethodInfo method = typeof(CardView3D).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(card, null);
        }

        internal static void CaptureLayout(GameObject root, string fileName)
        {
            string directory = Environment.GetEnvironmentVariable("HID_PRESENTATION_CAPTURE_DIR");
            if (string.IsNullOrWhiteSpace(directory)) return;
            if (!TabletopCameraFraming.TryGetActiveBounds(out Bounds bounds) && !TabletopCameraFraming.TryCalculateWorldBounds(root.transform, out bounds)) return;

            Camera sceneCamera = UnityEngine.Object.FindFirstObjectByType<SettlementCameraController>()?.GetComponent<Camera>();
            GameObject cameraObject = sceneCamera == null ? new GameObject("SettlementPresentationCaptureCamera") : null;
            Camera camera = sceneCamera ?? cameraObject.AddComponent<Camera>();
            float restoreAspect = camera.aspect;
            float restoreNearClipPlane = camera.nearClipPlane;
            float restoreFarClipPlane = camera.farClipPlane;
            camera.aspect = 16f / 9f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            bool restoreOrthographic = camera.orthographic;
            float restoreOrthographicSize = camera.orthographicSize;
            if (sceneCamera == null)
            {
                camera.orthographic = true;
                Quaternion rotation = Quaternion.Euler(75f, 0f, 0f);
                camera.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * 20f, rotation);
                float horizontalSize = bounds.size.x / camera.aspect;
                float verticalSize = bounds.size.z * Mathf.Sin(75f * Mathf.Deg2Rad);
                camera.orthographicSize = Mathf.Max(1f, Mathf.Max(horizontalSize, verticalSize) * 0.5f * 1.08f);
            }
            GameObject lightObject = null;
            if (sceneCamera == null)
            {
                lightObject = new GameObject("SettlementPresentationCaptureLight");
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 2f;
                light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            }
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture renderTexture = new(1600, 900, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = new(1600, 900, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0f, 0f, 1600f, 900f), 0, 0);
                texture.Apply();
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, fileName), texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = previousTarget;
                camera.aspect = restoreAspect;
                camera.nearClipPlane = restoreNearClipPlane;
                camera.farClipPlane = restoreFarClipPlane;
                camera.orthographic = restoreOrthographic;
                camera.orthographicSize = restoreOrthographicSize;
                UnityEngine.Object.DestroyImmediate(texture);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
                if (lightObject != null) UnityEngine.Object.DestroyImmediate(lightObject);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
