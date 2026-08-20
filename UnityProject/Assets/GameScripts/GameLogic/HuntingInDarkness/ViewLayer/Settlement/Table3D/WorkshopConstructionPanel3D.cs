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
    public sealed class WorkshopConstructionPanel3D : WorldSpaceViewPanel
    {
        private TextMeshPro descriptionText;
        private TextMeshPro requirementsText;
        private TextMeshPro statusText;
        private GameObject confirmButton;
        private Renderer confirmRenderer;
        private PlayableWorkshopDefinition definition;
        private PlayableWorkshopConstructionService service;
        private SettlementInstance settlement;
        private Func<PlayableWorkshopDefinition, UniTask<SettlementWorkshopConstructionResult>> buildCommand;
        private bool isBuilt;
        private bool isSubmitting;

        public static WorkshopConstructionPanel3D Create(Transform parent)
        {
            var gameObject = new GameObject("WorkshopConstructionPanel3D");
            gameObject.transform.SetParent(parent, false);
            var panel = gameObject.AddComponent<WorkshopConstructionPanel3D>();
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
            SetSize(4.4f, 2.8f);
            descriptionText = BuildText("Description", new Vector3(0f, 0.015f, 0.48f), 0.095f, new Vector2(3.8f, 0.72f));
            requirementsText = BuildText("Requirements", new Vector3(0f, 0.015f, -0.18f), 0.085f, new Vector2(3.8f, 0.42f));
            statusText = BuildText("Status", new Vector3(0f, 0.015f, -0.62f), 0.08f, new Vector2(3.8f, 0.30f));
            confirmButton = BuildButton("ConfirmButton", "建造", new Vector3(-0.65f, 0.03f, -1.08f), new Color(0.40f, 0.32f, 0.10f), Submit);
            BuildButton("CloseButton", "关闭", new Vector3(0.65f, 0.03f, -1.08f), new Color(0.42f, 0.15f, 0.14f), Hide);
            confirmRenderer = confirmButton.GetComponent<Renderer>();
        }

        public void Open(PlayableWorkshopDefinition selectedDefinition, SettlementInstance settlementData, PlayableWorkshopConstructionService constructionService, Func<PlayableWorkshopDefinition, UniTask<SettlementWorkshopConstructionResult>> command, Vector3 worldPosition)
        {
            if (selectedDefinition == null || settlementData == null || constructionService == null) return;
            definition = selectedDefinition;
            settlement = settlementData;
            service = constructionService;
            buildCommand = command;
            isSubmitting = false;
            Title.text = definition.DisplayName;
            descriptionText.text = definition.Description ?? string.Empty;
            requirementsText.text = BuildRequirements(definition);
            RefreshState();
            ShowAt(worldPosition);
        }

        public void RefreshVisible()
        {
            if (!gameObject.activeSelf || definition == null || service == null || isSubmitting) return;
            RefreshState();
        }

        private void Submit() => SubmitAsync().Forget();

        private async UniTaskVoid SubmitAsync()
        {
            if (isSubmitting || definition == null || service == null) return;
            if (buildCommand == null)
            {
                statusText.text = "建造命令尚未接入。";
                return;
            }
            if (!service.CanBuild(definition, out string reason))
            {
                statusText.text = reason;
                RefreshState(false);
                return;
            }

            isSubmitting = true;
            SetConfirmState(false);
            statusText.text = "正在搭建工坊……";
            try
            {
                SettlementWorkshopConstructionResult result = await buildCommand.Invoke(definition);
                if (this == null) return;
                statusText.text = result.Succeeded ? "工坊已建成，相关配方已经开放。" : result.Reason;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (this != null) statusText.text = "建造命令执行异常，请重试。";
            }
            if (this == null) return;
            isSubmitting = false;
            RefreshState(false);
        }

        private void RefreshState(bool replaceStatus = true)
        {
            bool canBuild = service.CanBuild(definition, out string reason);
            SetConfirmState(canBuild && buildCommand != null);
            if (replaceStatus) statusText.text = canBuild ? "确认投入材料并建造。" : reason;
        }

        private void SetConfirmState(bool enabled)
        {
            bool alreadyBuilt = settlement != null && settlement.IsWorkshopBuilt(definition.WorkshopId);
            confirmButton.SetActive(!alreadyBuilt);
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

        private static string BuildRequirements(PlayableWorkshopDefinition data)
        {
            var builder = new StringBuilder(data.RequiredInvention != null ? $"前置：{data.RequiredInvention.inventionName}" : "无需前置发明");
            bool hasCost = false;
            foreach (PlayableWorkshopCost cost in data.Costs)
            {
                if (cost?.Item == null) continue;
                builder.Append(hasCost ? " · " : "\n材料：").Append(cost.Item.itemName).Append(" ×").Append(cost.Amount);
                hasCost = true;
            }
            return builder.ToString();
        }
    }
}
