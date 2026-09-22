using System;
using System.Text;
using Cards3D;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>发明卡打开的 3D 确认板；只展示规则结果并向 Settlement ActionQueue 提交命令。</summary>
    public sealed class InventionUnlockPanel3D : WorldSpaceViewPanel
    {
        private const float PanelWidth = 4.4f;
        private const float PanelHeight = 2.8f;

        private TextMeshPro descriptionText;
        private TextMeshPro costText;
        private TextMeshPro statusText;
        private GameObject confirmButton;
        private Renderer confirmRenderer;
        private InventionData invention;
        private InventionSystem inventionSystem;
        private Func<InventionData, UniTask<SettlementInventionCommandResult>> unlockCommand;
        private bool isBuilt;
        private bool isSubmitting;

        public static InventionUnlockPanel3D Create(Transform parent)
        {
            var gameObject = new GameObject("InventionUnlockPanel3D");
            gameObject.transform.SetParent(parent, false);
            var panel = gameObject.AddComponent<InventionUnlockPanel3D>();
            panel.EnsureBuilt();
            panel.Hide();
            return panel;
        }

        private void Awake() => EnsureBuilt();

        public void EnsureBuilt()
        {
            if (isBuilt) return;
            isBuilt = true;
            BuildBase();
            SetSize(PanelWidth, PanelHeight);
            descriptionText = BuildText("Description", new Vector3(0f, 0.015f, 0.50f), 0.095f, new Vector2(3.8f, 0.76f));
            costText = BuildText("Costs", new Vector3(0f, 0.015f, -0.15f), 0.09f, new Vector2(3.8f, 0.45f));
            statusText = BuildText("Status", new Vector3(0f, 0.015f, -0.62f), 0.08f, new Vector2(3.8f, 0.30f));
            confirmButton = BuildButton("ConfirmButton", "发明", new Vector3(-0.65f, 0.03f, -1.08f), new Color(0.40f, 0.32f, 0.10f), Submit);
            BuildButton("CloseButton", "关闭", new Vector3(0.65f, 0.03f, -1.08f), new Color(0.42f, 0.15f, 0.14f), Hide);
            confirmRenderer = confirmButton.GetComponent<Renderer>();
        }

        public void Open(InventionData selectedInvention, InventionSystem system, Func<InventionData, UniTask<SettlementInventionCommandResult>> command, Vector3 worldPosition)
        {
            if (selectedInvention == null || system == null) return;
            invention = selectedInvention;
            inventionSystem = system;
            unlockCommand = command;
            isSubmitting = false;
            Title.text = invention.inventionName;
            descriptionText.text = BuildDescription(invention);
            costText.text = BuildCosts(invention);
            RefreshState();
            ShowAt(worldPosition);
        }

        public void RefreshVisible()
        {
            if (!gameObject.activeSelf || invention == null || inventionSystem == null || isSubmitting) return;
            RefreshState();
        }

        private void Submit() => SubmitAsync().Forget();

        private async UniTaskVoid SubmitAsync()
        {
            if (isSubmitting || invention == null || inventionSystem == null) return;
            if (unlockCommand == null)
            {
                statusText.text = "发明命令尚未接入。";
                return;
            }
            if (!inventionSystem.CanUnlock(invention, out string reason))
            {
                statusText.text = reason;
                RefreshState();
                return;
            }

            isSubmitting = true;
            SetConfirmState(false);
            statusText.text = "正在推演发明……";
            try
            {
                SettlementInventionCommandResult result = await unlockCommand.Invoke(invention);
                if (this == null) return;
                statusText.text = result.Succeeded ? "发明完成，营地已掌握该能力。" : result.Reason;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (this != null) statusText.text = "发明命令执行异常，请重试。";
            }
            if (this == null) return;
            isSubmitting = false;
            RefreshState(false);
        }

        private void RefreshState(bool replaceStatus = true)
        {
            bool unlocked = inventionSystem.IsUnlocked(invention);
            string reason = string.Empty;
            bool canUnlock = !unlocked && inventionSystem.CanUnlock(invention, out reason);
            SetConfirmState(canUnlock && unlockCommand != null);
            if (!replaceStatus) return;
            statusText.text = unlocked ? "已掌握" : (canUnlock ? "点击发明以确认消耗。" : reason);
        }

        private void SetConfirmState(bool enabled)
        {
            confirmButton.SetActive(!inventionSystem.IsUnlocked(invention));
            confirmButton.GetComponent<Collider>().enabled = enabled;
            confirmRenderer.material.color = enabled ? new Color(0.40f, 0.32f, 0.10f) : new Color(0.18f, 0.18f, 0.18f);
        }

        private TextMeshPro BuildText(string name, Vector3 position, float fontSize, Vector2 size)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(ContentRoot, false);
            textObject.transform.localPosition = position;
            textObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TextMeshPro text = textObject.AddComponent<TextMeshPro>();
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.82f, 0.82f, 0.78f);
            text.rectTransform.sizeDelta = size;
            return text;
        }

        private GameObject BuildButton(string name, string labelText, Vector3 position, Color color, Action onClick)
        {
            GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = name;
            button.transform.SetParent(transform, false);
            button.transform.localPosition = position;
            button.transform.localScale = new Vector3(0.95f, 0.04f, 0.32f);
            button.GetComponent<Renderer>().material.color = color;
            button.AddComponent<ClickProxy>().OnClick = onClick;

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(button.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            labelObject.transform.localScale = new Vector3(1f / 0.95f, 1f / 0.32f, 1f);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = labelText;
            label.fontSize = 0.11f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.95f, 0.90f, 0.78f);
            label.rectTransform.sizeDelta = new Vector2(0.75f, 0.24f);
            return button;
        }

        private static string BuildDescription(InventionData data)
        {
            if (string.IsNullOrWhiteSpace(data.effectDescription)) return data.description ?? string.Empty;
            return $"{data.description}\n\n效果：{data.effectDescription}";
        }

        private static string BuildCosts(InventionData data)
        {
            if (data.costs == null || data.costs.Count == 0) return "成本：无";
            var builder = new StringBuilder("成本：");
            bool hasCost = false;
            foreach (InventionCost cost in data.costs)
            {
                if (cost?.resource == null || cost.count <= 0) continue;
                if (hasCost) builder.Append(" · ");
                builder.Append(cost.resource.itemName).Append(" ×").Append(cost.count);
                hasCost = true;
            }
            return hasCost ? builder.ToString() : "成本：无";
        }
    }
}
