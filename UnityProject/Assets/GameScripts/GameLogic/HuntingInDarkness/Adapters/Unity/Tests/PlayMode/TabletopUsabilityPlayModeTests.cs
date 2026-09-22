using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Cards3D;
using GameplayBase.CombatSystem;
using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class TabletopUsabilityPlayModeTests
    {
        private const string PrefabPath = "Assets/AssetRaw/UI/Prefabs/TabletopUsability.prefab";

        internal static GameObject CreateOverlay(Camera camera)
        {
            GameObject prefab;
#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, $"缺少已安装的详情层 Prefab：{PrefabPath}");
#else
            prefab = null;
            Assert.Fail($"只能在 Unity Editor 中从 AssetDatabase 加载 {PrefabPath}。");
            return null;
#endif

            GameObject parent = new("TabletopUsabilityFixtureParent");
            parent.SetActive(false);
            GameObject root = UnityEngine.Object.Instantiate(prefab, parent.transform);
            FieldInfo cameraField = typeof(CardInspectionOverlay).GetField("inputCamera", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(cameraField, Is.Not.Null);
            cameraField.SetValue(root.GetComponent<CardInspectionOverlay>(), camera);
            parent.SetActive(true);
            return root;
        }

        internal static void DestroyOverlay(GameObject root)
        {
            if (root == null) return;
            Transform parent = root.transform.parent;
            if (parent != null && parent.name == "TabletopUsabilityFixtureParent")
                UnityEngine.Object.Destroy(parent.gameObject);
            else
                UnityEngine.Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator OpenDoesNotChangePhysicsAndCloseDoesNotClickThrough()
        {
            GameObject cameraObject = new("UsabilityPhysicsCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.eventMask = 123;
            GameObject overlayRoot = CreateOverlay(camera);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "UsabilityPhysicsGround";
            BoxCollider groundCollider = ground.GetComponent<BoxCollider>();
            Rigidbody rigidbody = ground.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            float mass = rigidbody.mass;
            bool isKinematic = rigidbody.isKinematic;
            bool useGravity = rigidbody.useGravity;
            RigidbodyConstraints constraints = rigidbody.constraints;
            GameObject sourceRoot = new("UsabilitySourceRoot");
            TabletopEventPrimaryCard3D source = TabletopEventPrimaryCard3D.Create(sourceRoot.transform);
            source.Present("详情源", "长正文测试", "详情", TabletopEventPrimaryTone.Narrative);
            ResourceCard3D target = ResourceCard3D.Create("bone", "骨", 1, sourceRoot.transform);
            int clickCount = 0;
            target.OnClicked += _ => clickCount++;

            try
            {
                Assert.That(CardInspectionOverlay.Open(source), Is.True);
                Assert.That(groundCollider.enabled, Is.True);
                Assert.That(rigidbody.mass, Is.EqualTo(mass));
                Assert.That(rigidbody.isKinematic, Is.EqualTo(isKinematic));
                Assert.That(rigidbody.useGravity, Is.EqualTo(useGravity));
                Assert.That(rigidbody.constraints, Is.EqualTo(constraints));
                Assert.That(camera.eventMask, Is.Zero);

                target.HandlePointerDown(Vector2.zero);
                target.HandlePointerUp();
                Assert.That(clickCount, Is.Zero);

                CardInspectionOverlay.Close();
                target.HandlePointerDown(Vector2.zero);
                target.HandlePointerUp();
                Assert.That(clickCount, Is.Zero);

                yield return null;
                yield return null;
                Assert.That(camera.eventMask, Is.EqualTo(123));

                target.HandlePointerDown(Vector2.zero);
                target.HandlePointerUp();
                Assert.That(clickCount, Is.EqualTo(1));

                overlayRoot.SetActive(false);
                overlayRoot.SetActive(true);
                Assert.That(CardInspectionOverlay.Open(source), Is.True);
                CardInspectionOverlay.Close();
            }
            finally
            {
                if (CardInspectionOverlay.BlocksWorldInput) CardInspectionOverlay.Close();
                UnityEngine.Object.Destroy(sourceRoot);
                UnityEngine.Object.Destroy(ground);
                DestroyOverlay(overlayRoot);
                UnityEngine.Object.Destroy(cameraObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator LongInspectionTextUsesScrollableContentAndResetsToTop()
        {
            GameObject cameraObject = new("UsabilityLayoutCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            GameObject overlayRoot = CreateOverlay(camera);
            GameObject sourceRoot = new("UsabilityLayoutSourceRoot");
            TabletopEventPrimaryCard3D source = TabletopEventPrimaryCard3D.Create(sourceRoot.transform);
            var bodyBuilder = new StringBuilder();
            for (int index = 0; index < 60; index++)
            {
                if (index > 0) bodyBuilder.Append('\n');
                bodyBuilder.Append($"第{index + 1}段：夜色中的猎人记录仍需完整显示。条件、结果与后续影响都保留在正文中。");
            }
            string fullBody = bodyBuilder.ToString();
            source.Present("长文详情", fullBody, "详情页脚", TabletopEventPrimaryTone.Narrative);

            try
            {
                Assert.That(CardInspectionOverlay.Open(source), Is.True);
                yield return null;
                Canvas.ForceUpdateCanvases();
                FieldInfo scrollField = typeof(CardInspectionOverlay).GetField("bodyScrollRect", BindingFlags.Instance | BindingFlags.NonPublic);
                ScrollRect scrollRect = (ScrollRect)scrollField.GetValue(overlayRoot.GetComponent<CardInspectionOverlay>());
                Assert.That(scrollRect, Is.Not.Null);
                Assert.That(scrollRect.content.rect.height, Is.GreaterThan(scrollRect.viewport.rect.height));
                TMP_Text bodyText = scrollRect.content.GetComponent<TMP_Text>();
                Assert.That(bodyText, Is.Not.Null);
                Assert.That(bodyText.text, Does.Contain(fullBody));

                scrollRect.verticalNormalizedPosition = 0f;
                Canvas.ForceUpdateCanvases();
                Assert.That(scrollRect.verticalNormalizedPosition, Is.EqualTo(0f).Within(0.001f));

                CardInspectionOverlay.Close();
                yield return null;
                yield return null;
                Assert.That(CardInspectionOverlay.Open(source), Is.True);
                Canvas.ForceUpdateCanvases();
                Assert.That(scrollRect.verticalNormalizedPosition, Is.EqualTo(1f).Within(0.001f));

                Canvas canvas = overlayRoot.GetComponent<Canvas>();
                FieldInfo helpField = typeof(TabletopControlHintOverlay).GetField("helpButton", BindingFlags.Instance | BindingFlags.NonPublic);
                Button helpButton = (Button)helpField.GetValue(overlayRoot.GetComponent<TabletopControlHintOverlay>());
                RectTransform canvasRect = (RectTransform)canvas.transform;
                Vector3[] corners = new Vector3[4];
                helpButton.GetComponent<RectTransform>().GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = canvasRect.InverseTransformPoint(corner);
                    Assert.That(canvasRect.rect.Contains(new Vector2(local.x, local.y)), Is.True, $"HelpButton corner outside Canvas: {corner}");
                }

                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(scrollRect.verticalScrollbar, Is.Not.Null);
                Assert.That(scrollRect.verticalScrollbar.value, Is.EqualTo(1f).Within(0.001f));
                CaptureOptionalUiScreenshot(overlayRoot, camera);
                Canvas.ForceUpdateCanvases();
            }
            finally
            {
                if (CardInspectionOverlay.BlocksWorldInput) CardInspectionOverlay.Close();
                UnityEngine.Object.Destroy(sourceRoot);
                DestroyOverlay(overlayRoot);
                UnityEngine.Object.Destroy(cameraObject);
            }

            yield return null;
        }

        private static void CaptureOptionalUiScreenshot(GameObject overlayRoot, Camera camera)
        {
            string path = Environment.GetEnvironmentVariable("HID_USABILITY_CAPTURE_PATH");
            if (string.IsNullOrWhiteSpace(path)) return;

            Canvas canvas = overlayRoot.GetComponent<Canvas>();
            RenderMode previousRenderMode = canvas.renderMode;
            Camera previousWorldCamera = canvas.worldCamera;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture renderTexture = new(1600, 900, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = new(1600, 900, TextureFormat.RGBA32, false);
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                camera.targetTexture = renderTexture;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0f, 0f, 1600f, 900f), 0, 0);
                texture.Apply();
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = previousTarget;
                canvas.renderMode = previousRenderMode;
                canvas.worldCamera = previousWorldCamera;
                UnityEngine.Object.DestroyImmediate(texture);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        [UnityTest]
        public IEnumerator CombatInputProviderDoesNotReuseInspectionCanvas()
        {
            GameObject cameraObject = new("UsabilityProviderCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            GameObject overlayRoot = CreateOverlay(camera);
            Canvas overlayCanvas = overlayRoot.GetComponent<Canvas>();
            UIPlayerInputProvider provider = new(null, null);
            FieldInfo canvasField = typeof(UIPlayerInputProvider).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo panelField = typeof(UIPlayerInputProvider).GetField("_panelRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(canvasField, Is.Not.Null);
            Assert.That(panelField, Is.Not.Null);
            Canvas[] canvasesBefore = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            Canvas selectedCanvas = null;
            try
            {
                Assert.That(provider.RequestSelectTile("测试", new List<Vector2Int>()).GetAwaiter().GetResult(), Is.Null);
                selectedCanvas = (Canvas)canvasField.GetValue(provider);
                Assert.That(selectedCanvas, Is.Not.Null);
                Assert.That(selectedCanvas, Is.Not.SameAs(overlayCanvas));
            }
            finally
            {
                GameObject panelRoot = (GameObject)panelField.GetValue(provider);
                if (panelRoot != null) UnityEngine.Object.Destroy(panelRoot);
                if (selectedCanvas != null && Array.IndexOf(canvasesBefore, selectedCanvas) < 0)
                    UnityEngine.Object.Destroy(selectedCanvas.gameObject);
                DestroyOverlay(overlayRoot);
                UnityEngine.Object.Destroy(cameraObject);
            }

            yield return null;
        }
    }
}
