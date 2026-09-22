using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>最小全局设置入口：右上角按钮打开相机速度和主音量滑杆。</summary>
    public sealed class GlobalSettingsMenu : MonoBehaviour
    {
        private GameObject panel;

        public void Initialize()
        {
            if (panel != null) return;
            Build();
        }

        private void Build()
        {
            GameObject entry = CreateBox("Settings Entry", transform, new Color(0.08f, 0.12f, 0.16f, 0.94f));
            RectTransform entryRect = entry.GetComponent<RectTransform>();
            entryRect.anchorMin = Vector2.one;
            entryRect.anchorMax = Vector2.one;
            entryRect.pivot = Vector2.one;
            entryRect.anchoredPosition = new Vector2(-14f, -14f);
            entryRect.sizeDelta = new Vector2(90f, 34f);
            Button entryButton = entry.AddComponent<Button>();
            entryButton.onClick.AddListener(Toggle);
            AddText(entry.transform, "设置", 16, TextAnchor.MiddleCenter);

            panel = CreateBox("Global Settings Panel", transform, new Color(0.035f, 0.045f, 0.06f, 0.98f));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.one;
            panelRect.anchorMax = Vector2.one;
            panelRect.pivot = Vector2.one;
            panelRect.anchoredPosition = new Vector2(-14f, -56f);
            panelRect.sizeDelta = new Vector2(360f, 230f);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 9f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddLabel(panel.transform, "全局设置", 21, 34f);
            AddSlider(panel.transform, "相机移动速度", 0.25f, 3f, GlobalGameSettings.CameraPanSpeed, GlobalGameSettings.SetCameraPanSpeed);
            AddSlider(panel.transform, "相机缩放速度", 0.25f, 3f, GlobalGameSettings.CameraZoomSpeed, GlobalGameSettings.SetCameraZoomSpeed);
            AddSlider(panel.transform, "主音量", 0f, 1f, GlobalGameSettings.MasterVolume, GlobalGameSettings.SetMasterVolume);
            panel.SetActive(false);
        }

        private void Toggle()
        {
            if (panel != null) panel.SetActive(!panel.activeSelf);
        }

        private void OnDestroy() => PlayerPrefs.Save();

        private static void AddSlider(Transform parent, string label, float minimum, float maximum, float value, Action<float> changed)
        {
            GameObject row = new(label, typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 38f;
            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;

            Text labelText = AddText(row.transform, label, 15, TextAnchor.MiddleLeft);
            labelText.gameObject.AddComponent<LayoutElement>().preferredWidth = 116f;

            GameObject sliderObject = new("Slider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            sliderObject.transform.SetParent(row.transform, false);
            sliderObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
            Slider slider = sliderObject.GetComponent<Slider>();
            slider.minValue = minimum;
            slider.maxValue = maximum;

            GameObject background = CreateBox("Background", sliderObject.transform, new Color(0.12f, 0.15f, 0.18f, 1f));
            Stretch(background.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            GameObject fillArea = new("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>(), 5f, 5f, 8f, 8f);
            GameObject fill = CreateBox("Fill", fillArea.transform, new Color(0.18f, 0.55f, 0.72f, 1f));
            Stretch(fill.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.targetGraphic = background.GetComponent<Image>();

            Text valueText = AddText(row.transform, value.ToString("0.00"), 14, TextAnchor.MiddleCenter);
            valueText.gameObject.AddComponent<LayoutElement>().preferredWidth = 48f;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(nextValue =>
            {
                changed(nextValue);
                valueText.text = nextValue.ToString("0.00");
            });
        }

        private static void AddLabel(Transform parent, string value, int fontSize, float height)
        {
            Text text = AddText(parent, value, fontSize, TextAnchor.MiddleCenter);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        }

        private static GameObject CreateBox(string name, Transform parent, Color color)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            gameObject.GetComponent<Image>().color = color;
            return gameObject;
        }

        private static Text AddText(Transform parent, string value, int fontSize, TextAnchor alignment)
        {
            GameObject gameObject = new("Text", typeof(RectTransform), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            Stretch(gameObject.GetComponent<RectTransform>(), 6f, 6f, 2f, 2f);
            Text text = gameObject.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect, float left, float right, float bottom, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
