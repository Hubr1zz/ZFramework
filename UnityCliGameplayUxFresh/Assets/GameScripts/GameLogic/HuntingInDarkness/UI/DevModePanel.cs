using System;
using Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    /// <summary>运行时 GM 指令面板。只负责参数输入、Tab 显隐和反馈，实际命令由战役开发者命令门面执行。</summary>
    public sealed class DevModePanel : MonoBehaviour
    {
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

        private ICampaignDeveloperCommands commands;
        private GameObject panel;
        private Text statusText;
        private bool visible;

        internal bool IsVisible => visible;
        internal KeyCode ToggleKey => toggleKey;

        internal void Init(ICampaignDeveloperCommands developerCommands)
        {
            commands = developerCommands ?? throw new ArgumentNullException(nameof(developerCommands));
            BuildPanel();
            SetVisible(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) Toggle();
        }

        public void Toggle() => SetVisible(!visible);

        private void SetVisible(bool value)
        {
            visible = value;
            if (panel != null) panel.SetActive(value);
            if (!value && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform)) EventSystem.current.SetSelectedGameObject(null);
        }

        private void BuildPanel()
        {
            panel = new GameObject("GM Command Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.56f, 0.1f);
            panelRect.anchorMax = new Vector2(0.98f, 0.94f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.035f, 0.045f, 0.055f, 0.97f);

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 14);
            layout.spacing = 8f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddText(panel.transform, "GM 指令面板  [Tab 关闭]", 22, TextAnchor.MiddleCenter, 38f, new Color(0.65f, 0.9f, 1f));
            AddText(panel.transform, "仅营地阶段可执行；ID 必须使用内容目录中的稳定 ID。", 14, TextAnchor.MiddleLeft, 26f, new Color(0.75f, 0.8f, 0.86f));

            AddSectionTitle(panel.transform, "生成资源");
            Transform resourceRow = AddRow(panel.transform, 38f);
            InputField resourceId = AddInput(resourceRow, "资源 ID", 0.58f);
            InputField resourceAmount = AddInput(resourceRow, "数量", 0.2f, "1");
            AddButton(resourceRow, "生成", 0.22f, () => ExecuteAmount(resourceId.text, resourceAmount.text, commands.AddResource));

            AddSectionTitle(panel.transform, "解锁并生成工坊");
            Transform workshopRow = AddRow(panel.transform, 38f);
            InputField workshopId = AddInput(workshopRow, "工坊 ID", 0.76f);
            AddButton(workshopRow, "生成", 0.24f, () => ShowResult(commands.UnlockWorkshop(workshopId.text)));

            AddSectionTitle(panel.transform, "增加空白猎人");
            Transform hunterRow = AddRow(panel.transform, 38f);
            InputField hunterAmount = AddInput(hunterRow, "数量", 0.76f, "1");
            AddButton(hunterRow, "生成", 0.24f, () => ExecuteAmount(string.Empty, hunterAmount.text, (_, amount) => commands.AddBlankHunters(amount)));

            AddSectionTitle(panel.transform, "直接触发营地事件");
            Transform eventRow = AddRow(panel.transform, 38f);
            InputField eventId = AddInput(eventRow, "事件 ID", 0.76f);
            AddButton(eventRow, "触发", 0.24f, () => ShowResult(commands.TriggerSettlementEvent(eventId.text)));

            statusText = AddText(panel.transform, "等待指令。", 15, TextAnchor.UpperLeft, 58f, new Color(0.75f, 0.85f, 0.75f));
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private void ExecuteAmount(string id, string amountText, Func<string, int, DeveloperCommandResult> command)
        {
            if (!int.TryParse(amountText?.Trim(), out int amount))
            {
                ShowResult(DeveloperCommandResult.Failed("数量必须是整数。"));
                return;
            }
            ShowResult(command(id, amount));
        }

        private void ShowResult(DeveloperCommandResult result)
        {
            if (statusText == null) return;
            statusText.text = $"{(result.Succeeded ? "成功" : "失败")}：{result.Message}";
            statusText.color = result.Succeeded ? new Color(0.55f, 1f, 0.65f) : new Color(1f, 0.55f, 0.5f);
        }

        private static void AddSectionTitle(Transform parent, string label)
        {
            AddText(parent, label, 16, TextAnchor.MiddleLeft, 27f, new Color(0.98f, 0.82f, 0.45f));
        }

        private static Transform AddRow(Transform parent, float height)
        {
            var row = new GameObject("Command Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = height;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 7f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return row.transform;
        }

        private static InputField AddInput(Transform parent, string placeholder, float flexibleWidth, string initialValue = "")
        {
            var inputObject = new GameObject($"Input {placeholder}", typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
            inputObject.transform.SetParent(parent, false);
            inputObject.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.18f, 1f);
            inputObject.GetComponent<LayoutElement>().flexibleWidth = flexibleWidth;

            Text valueText = AddChildText(inputObject.transform, "Value", 16, new Color(0.95f, 0.97f, 1f));
            valueText.alignment = TextAnchor.MiddleLeft;
            valueText.GetComponent<RectTransform>().offsetMin = new Vector2(9f, 2f);
            valueText.GetComponent<RectTransform>().offsetMax = new Vector2(-9f, -2f);

            Text placeholderText = AddChildText(inputObject.transform, "Placeholder", 15, new Color(0.48f, 0.52f, 0.58f));
            placeholderText.text = placeholder;
            placeholderText.fontStyle = FontStyle.Italic;
            placeholderText.alignment = TextAnchor.MiddleLeft;
            placeholderText.GetComponent<RectTransform>().offsetMin = new Vector2(9f, 2f);
            placeholderText.GetComponent<RectTransform>().offsetMax = new Vector2(-9f, -2f);

            InputField input = inputObject.GetComponent<InputField>();
            input.textComponent = valueText;
            input.placeholder = placeholderText;
            input.text = initialValue;
            return input;
        }

        private static void AddButton(Transform parent, string label, float flexibleWidth, Action action)
        {
            var buttonObject = new GameObject($"Button {label}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<Image>().color = new Color(0.12f, 0.32f, 0.42f, 1f);
            buttonObject.GetComponent<LayoutElement>().flexibleWidth = flexibleWidth;
            Button button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(() => action());
            Text text = AddChildText(buttonObject.transform, "Label", 16, Color.white);
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
        }

        private static Text AddText(Transform parent, string value, int fontSize, TextAnchor alignment, float height, Color color)
        {
            var textObject = new GameObject($"Text {value}", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);
            textObject.GetComponent<LayoutElement>().preferredHeight = height;
            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static Text AddChildText(Transform parent, string name, int fontSize, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = color;
            text.supportRichText = false;
            return text;
        }
    }
}
