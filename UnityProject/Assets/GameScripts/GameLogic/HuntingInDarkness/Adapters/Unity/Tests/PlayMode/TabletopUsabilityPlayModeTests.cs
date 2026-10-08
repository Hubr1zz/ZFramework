using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Cards3D;
using GameplayBase.CombatSystem;
using HuntingInDarkness.Data;
using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class TabletopUsabilityPlayModeTests
    {
        private const string PrefabPath = "Assets/AssetRaw/UI/Prefabs/TabletopUsability.prefab";
        private const string PresentationRegistryPrefabPath = "Assets/AssetRaw/UI/Prefabs/TabletopPresentationAssets.prefab";
        private const string PhysicalInteractionScreenPrefabPath = "Assets/AssetRaw/UI/Prefabs/PhysicalInteractionScreen.prefab";

        internal static GameObject CreatePresentationAssets()
        {
#if UNITY_EDITOR
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PresentationRegistryPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"缺少已安装的事件/年鉴注册 Prefab：{PresentationRegistryPrefabPath}");
#else
            Assert.Fail($"只能在 Unity Editor 中从 AssetDatabase 加载 {PresentationRegistryPrefabPath}。");
            return null;
#endif
            GameObject parent = new("TabletopPresentationAssetsFixtureParent");
            parent.SetActive(false);
            UnityEngine.Object.Instantiate(prefab, parent.transform);
            parent.SetActive(true);
            return parent;
        }

        internal static void DestroyPresentationAssets(GameObject root)
        {
            if (root != null) UnityEngine.Object.Destroy(root);
        }

        internal static GameObject CreatePhysicalInteractionScreen()
        {
#if UNITY_EDITOR
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PhysicalInteractionScreenPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"缺少物理交互屏幕 Prefab：{PhysicalInteractionScreenPrefabPath}");
            return UnityEngine.Object.Instantiate(prefab);
#else
            Assert.Fail($"只能在 Unity Editor 中从 AssetDatabase 加载 {PhysicalInteractionScreenPrefabPath}。");
            return null;
#endif
        }

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
        public IEnumerator HoverDoesNotChangePhysicsOrBlockWorldInput()
        {
            GameObject cameraObject = new("UsabilityPhysicsCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.eventMask = 123;
            GameObject overlayRoot = CreateOverlay(camera);
            GameObject presentationAssets = CreatePresentationAssets();
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
                Assert.That(camera.eventMask, Is.EqualTo(123));
                Assert.That(CardInspectionOverlay.BlocksWorldInput, Is.False);

                target.HandlePointerDown(Vector2.zero);
                target.HandlePointerUp();
                Assert.That(clickCount, Is.EqualTo(1));

                CardInspectionOverlay.Close();
                target.HandlePointerDown(Vector2.zero);
                target.HandlePointerUp();
                Assert.That(clickCount, Is.EqualTo(2));

                yield return null;
                yield return null;
                Assert.That(camera.eventMask, Is.EqualTo(123));

                target.HandlePointerDown(Vector2.zero);
                target.HandlePointerUp();
                Assert.That(clickCount, Is.EqualTo(3));

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
                DestroyPresentationAssets(presentationAssets);
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
            GameObject presentationAssets = CreatePresentationAssets();
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
                Assert.That(CardInspectionOverlay.ConsumesScroll, Is.True);
                CardInspectionOverlay.Scroll(-1f);
                Assert.That(scrollRect.verticalNormalizedPosition, Is.LessThan(1f));
                Assert.That(scrollRect.verticalNormalizedPosition, Is.GreaterThan(0f));
                CardInspectionOverlay.Scroll(1f);
                Assert.That(scrollRect.verticalNormalizedPosition, Is.EqualTo(1f).Within(0.001f));
                CaptureOptionalUiScreenshot(overlayRoot, camera);
                Canvas.ForceUpdateCanvases();
            }
            finally
            {
                if (CardInspectionOverlay.BlocksWorldInput) CardInspectionOverlay.Close();
                UnityEngine.Object.Destroy(sourceRoot);
                DestroyOverlay(overlayRoot);
                DestroyPresentationAssets(presentationAssets);
                UnityEngine.Object.Destroy(cameraObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator LedgerDisableReleasesScreenModalGate()
        {
            GameObject assetsRoot = CreatePresentationAssets();
            CampLedgerPanel3D ledger = null;
            try
            {
                ledger = CampLedgerPanel3D.Create(assetsRoot.transform);
                SettlementInstance settlement = new();
                for (int index = 0; index < 48; index++)
                    settlement.Timeline.Add(new AnnalEntry { Year = index + 1, EventId = $"ledger_{index}", EventName = $"年鉴记录 {index + 1}", IsCompleted = true, EntryType = TimelineEntryType.Random });
                ledger.Open(settlement, Vector3.zero);
                Assert.That(ScreenModalInputGate.IsBlocked, Is.True);
                FieldInfo scrollField = typeof(CampLedgerPanel3D).GetField("entriesScrollRect", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(scrollField, Is.Not.Null);
                ScrollRect scrollRect = (ScrollRect)scrollField.GetValue(ledger);
                Assert.That(scrollRect, Is.Not.Null);
                Assert.That(scrollRect.viewport, Is.Not.Null);
                Canvas.ForceUpdateCanvases();
                Assert.That(scrollRect.content.rect.height, Is.GreaterThan(scrollRect.viewport.rect.height));
                CaptureOptionalScreenCanvasScreenshot(ledger.gameObject, CreatePresentationCaptureCamera(), "ledger.png");
                ledger.gameObject.SetActive(false);
                yield return null;
                Assert.That(ScreenModalInputGate.IsBlocked, Is.False);
            }
            finally
            {
                if (ledger != null) UnityEngine.Object.Destroy(ledger.gameObject);
                DestroyPresentationAssets(assetsRoot);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator EventPanelUsesScrollableChoicesAndSingleClick()
        {
            GameObject assetsRoot = CreatePresentationAssets();
            TabletopEventPanel3D panel = null;
            int clickCount = 0;
            try
            {
                panel = TabletopEventPanel3D.Create(assetsRoot.transform);
                List<TabletopEventChoicePresentation> choices = new();
                for (int index = 0; index < 14; index++)
                {
                    choices.Add(new TabletopEventChoicePresentation($"选项 {index + 1}", "这是一段较长的事件选项正文，用于确认多选项面板能滚动查看完整内容。", index != 13, index == 13 ? "不可用" : string.Empty, () => clickCount++));
                }
                panel.Present(Vector3.zero, "事件标题", "完整正文", "事件提示", TabletopEventPrimaryTone.Narrative, choices);
                yield return null;
                Canvas.ForceUpdateCanvases();
                FieldInfo scrollField = typeof(TabletopEventPanel3D).GetField("choicesScrollRect", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(scrollField, Is.Not.Null);
                ScrollRect scrollRect = (ScrollRect)scrollField.GetValue(panel);
                Assert.That(scrollRect, Is.Not.Null);
                Assert.That(scrollRect.viewport, Is.Not.Null);
                Assert.That(scrollRect.content.rect.height, Is.GreaterThan(scrollRect.viewport.rect.height));
                panel.Choices[0].Button.onClick.Invoke();
                panel.Choices[0].Button.onClick.Invoke();
                Assert.That(clickCount, Is.EqualTo(1));
                Assert.That(panel.Choices[13].IsInteractable, Is.False);
                panel.Choices[13].Button.onClick.Invoke();
                Assert.That(clickCount, Is.EqualTo(1));
                scrollRect.verticalNormalizedPosition = 0f;
                yield return null;
                Canvas.ForceUpdateCanvases();
                Vector3[] choiceCorners = new Vector3[4];
                panel.Choices[13].GetComponent<RectTransform>().GetWorldCorners(choiceCorners);
                Rect viewportRect = scrollRect.viewport.rect;
                bool lastChoiceVisible = false;
                foreach (Vector3 corner in choiceCorners)
                {
                    Vector3 local = scrollRect.viewport.InverseTransformPoint(corner);
                    if (viewportRect.Contains(new Vector2(local.x, local.y)))
                    {
                        lastChoiceVisible = true;
                        break;
                    }
                }
                Assert.That(lastChoiceVisible, Is.True);
                CaptureOptionalScreenCanvasScreenshot(panel.gameObject, CreatePresentationCaptureCamera(), "event-scroll-stress.png");
            }
            finally
            {
                if (panel != null) panel.Close();
                DestroyPresentationAssets(assetsRoot);
            }
            yield return null;
        }

        internal static Camera CreatePresentationCaptureCamera()
        {
            GameObject cameraObject = new("PresentationCaptureCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            return camera;
        }

        internal static void CaptureOptionalScreenCanvasScreenshot(GameObject canvasRoot, Camera camera, string fileName)
        {
            string directory = Environment.GetEnvironmentVariable("HID_PRESENTATION_CAPTURE_DIR");
            try
            {
                if (string.IsNullOrWhiteSpace(directory)) return;
                Canvas canvas = canvasRoot.GetComponent<Canvas>();
                RenderMode previousRenderMode = canvas.renderMode;
                Camera previousWorldCamera = canvas.worldCamera;
                RenderTexture previousTarget = camera.targetTexture;
                RenderTexture previousActive = RenderTexture.active;
                RenderTexture renderTexture = new(1600, 900, 24, RenderTextureFormat.ARGB32);
                Texture2D texture = new(1600, 900, TextureFormat.RGBA32, false);
                List<Renderer> hiddenRenderers = new();
                List<bool> rendererStates = new();
                try
                {
                    foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    {
                        if (renderer == null || renderer.transform.IsChildOf(canvas.transform)) continue;
                        hiddenRenderers.Add(renderer);
                        rendererStates.Add(renderer.enabled);
                        renderer.enabled = false;
                    }
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    camera.targetTexture = renderTexture;
                    Canvas.ForceUpdateCanvases();
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
                    canvas.renderMode = previousRenderMode;
                    canvas.worldCamera = previousWorldCamera;
                    Canvas.ForceUpdateCanvases();
                    for (int index = 0; index < hiddenRenderers.Count; index++)
                        if (hiddenRenderers[index] != null)
                            hiddenRenderers[index].enabled = rendererStates[index];
                    UnityEngine.Object.DestroyImmediate(texture);
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }
        }

        internal static void CaptureOptionalUiScreenshot(GameObject overlayRoot, Camera camera, int width = 1600, int height = 900, Action beforeRender = null)
        {
            string path = Environment.GetEnvironmentVariable("HID_USABILITY_CAPTURE_PATH");
            if (string.IsNullOrWhiteSpace(path)) return;

            Canvas canvas = overlayRoot.GetComponent<Canvas>();
            RenderMode previousRenderMode = canvas.renderMode;
            Camera previousWorldCamera = canvas.worldCamera;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture renderTexture = new(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false);
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                camera.targetTexture = renderTexture;
                Canvas.ForceUpdateCanvases();
                beforeRender?.Invoke();
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
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
                    Canvas.ForceUpdateCanvases();
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
