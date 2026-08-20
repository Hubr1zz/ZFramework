using System;
using System.Collections.Generic;
using Cards3D;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>已掌握发明的世界空间主动效果页；事件链仍由 Settlement Runner 执行。</summary>
    public sealed class InventionActiveEffectPanel3D : WorldSpaceViewPanel
    {
        private const int EffectsPerPage = 4;
        private readonly List<EffectButton> buttons = new();
        private TextMeshPro descriptionText;
        private TextMeshPro pageText;
        private TextMeshPro statusText;
        private InventionData invention;
        private SettlementInstance settlement;
        private InventionSystem inventionSystem;
        private Func<InventionData, InventionActiveEffect, UniTask<SettlementInventionActiveEffectCommandResult>> command;
        private Vector3 openPosition;
        private int pageIndex;
        private bool isBuilt;
        private bool isSubmitting;

        public static InventionActiveEffectPanel3D Create(Transform parent)
        {
            var gameObject = new GameObject("InventionActiveEffectPanel3D");
            gameObject.transform.SetParent(parent, false);
            var panel = gameObject.AddComponent<InventionActiveEffectPanel3D>();
            panel.EnsureBuilt();
            panel.Hide();
            return panel;
        }

        private void Awake() => EnsureBuilt();

        public void EnsureBuilt()
        {
            if (isBuilt)
                return;
            isBuilt = true;
            BuildBase();
            SetSize(4.8f, 3.35f);
            descriptionText = BuildText("Description", new Vector3(0f, 0.015f, 1.02f), 0.085f, new Vector2(4.15f, 0.42f));
            pageText = BuildText("Page", new Vector3(0f, 0.015f, 0.68f), 0.07f, new Vector2(2.0f, 0.22f));
            statusText = BuildText("Status", new Vector3(0f, 0.015f, -1.08f), 0.075f, new Vector2(4.0f, 0.30f));
            for (int index = 0; index < EffectsPerPage; index++)
                buttons.Add(BuildEffectButton(index));
            BuildButton("Previous", "上一页", new Vector3(-1.42f, 0.03f, -1.47f), new Vector3(0.78f, 0.04f, 0.26f), PreviousPage, new Color(0.22f, 0.28f, 0.34f));
            BuildButton("Next", "下一页", new Vector3(-0.48f, 0.03f, -1.47f), new Vector3(0.78f, 0.04f, 0.26f), NextPage, new Color(0.22f, 0.28f, 0.34f));
            BuildButton("Close", "关闭", new Vector3(1.34f, 0.03f, -1.47f), new Vector3(0.92f, 0.04f, 0.26f), Hide, new Color(0.42f, 0.15f, 0.14f));
        }

        public void Open(InventionData selectedInvention, SettlementInstance currentSettlement, InventionSystem system, Func<InventionData, InventionActiveEffect, UniTask<SettlementInventionActiveEffectCommandResult>> activateCommand, Vector3 worldPosition)
        {
            if (selectedInvention == null || currentSettlement == null || system == null)
                return;
            invention = selectedInvention;
            settlement = currentSettlement;
            inventionSystem = system;
            command = activateCommand;
            openPosition = worldPosition;
            pageIndex = 0;
            isSubmitting = false;
            Title.text = invention.inventionName;
            descriptionText.text = string.IsNullOrWhiteSpace(invention.effectDescription) ? invention.description ?? string.Empty : invention.effectDescription;
            statusText.text = "选择一个主动效果。";
            RefreshPage();
            ShowAt(worldPosition);
        }

        public void RefreshVisible()
        {
            if (!gameObject.activeSelf || invention == null || settlement == null || isSubmitting)
                return;
            RefreshPage();
        }

        private EffectButton BuildEffectButton(int index)
        {
            float z = 0.36f - index * 0.40f;
            GameObject body = BuildButton($"Effect_{index}", string.Empty, new Vector3(0f, 0.03f, z), new Vector3(4.0f, 0.04f, 0.32f), () => Submit(index), new Color(0.28f, 0.30f, 0.18f));
            TextMeshPro label = body.transform.Find("Label").GetComponent<TextMeshPro>();
            label.rectTransform.sizeDelta = new Vector2(3.7f, 0.25f);
            label.fontSize = 0.09f;
            return new EffectButton(body, label);
        }

        private void RefreshPage()
        {
            IReadOnlyList<InventionActiveEffect> effects = invention?.activeEffects != null ? invention.activeEffects : Array.Empty<InventionActiveEffect>();
            int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)effects.Count / EffectsPerPage));
            pageIndex = Mathf.Clamp(pageIndex, 0, pageCount - 1);
            pageText.text = effects.Count == 0 ? "没有可用的主动效果" : $"第 {pageIndex + 1}/{pageCount} 页";
            for (int visualIndex = 0; visualIndex < buttons.Count; visualIndex++)
            {
                int effectIndex = pageIndex * EffectsPerPage + visualIndex;
                EffectButton button = buttons[visualIndex];
                if (effectIndex >= effects.Count)
                {
                    button.Body.SetActive(false);
                    button.Effect = null;
                    continue;
                }
                InventionActiveEffect effect = effects[effectIndex];
                button.Body.SetActive(true);
                button.Effect = effect;
                bool available = InventionActiveEffectRules.CanActivate(inventionSystem.IsUnlocked(invention), settlement.CurrentYear, effect.effectId, effect.eventId, effect.maxUsesPerYear, settlement.InventionActiveEffectUses, true, out string reason);
                int used = InventionActiveEffectRules.GetUseCount(settlement.InventionActiveEffectUses, effect.effectId, settlement.CurrentYear);
                string allowance = effect.maxUsesPerYear == 0 ? "不限次数" : $"本年 {used}/{effect.maxUsesPerYear}";
                button.Label.text = $"{effect.effectName} · {allowance}\n{(available ? effect.description : reason)}";
                button.Body.GetComponent<Collider>().enabled = available && command != null && !isSubmitting;
                button.Body.GetComponent<Renderer>().material.color = available ? new Color(0.28f, 0.30f, 0.18f) : new Color(0.17f, 0.17f, 0.17f);
            }
        }

        private void Submit(int visualIndex)
        {
            if (visualIndex < 0 || visualIndex >= buttons.Count)
                return;
            SubmitAsync(buttons[visualIndex].Effect).Forget();
        }

        private async UniTaskVoid SubmitAsync(InventionActiveEffect effect)
        {
            if (isSubmitting || effect == null || command == null)
                return;
            if (!InventionActiveEffectRules.CanActivate(inventionSystem.IsUnlocked(invention), settlement.CurrentYear, effect.effectId, effect.eventId, effect.maxUsesPerYear, settlement.InventionActiveEffectUses, true, out string reason))
            {
                statusText.text = reason;
                RefreshPage();
                return;
            }

            isSubmitting = true;
            RefreshPage();
            Hide();
            try
            {
                SettlementInventionActiveEffectCommandResult result = await command.Invoke(invention, effect);
                if (this == null)
                    return;
                isSubmitting = false;
                if (result.Succeeded)
                    return;
                statusText.text = result.Reason;
                RefreshPage();
                ShowAt(openPosition);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (this == null)
                    return;
                isSubmitting = false;
                statusText.text = "主动效果执行异常，请重试。";
                RefreshPage();
                ShowAt(openPosition);
            }
        }

        private void PreviousPage()
        {
            if (pageIndex <= 0)
                return;
            pageIndex--;
            RefreshPage();
        }

        private void NextPage()
        {
            int count = invention?.activeEffects?.Count ?? 0;
            if ((pageIndex + 1) * EffectsPerPage >= count)
                return;
            pageIndex++;
            RefreshPage();
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
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.rectTransform.sizeDelta = size;
            return text;
        }

        private GameObject BuildButton(string name, string labelText, Vector3 position, Vector3 scale, Action onClick, Color color)
        {
            GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = name;
            button.transform.SetParent(transform, false);
            button.transform.localPosition = position;
            button.transform.localScale = scale;
            button.GetComponent<Renderer>().material.color = color;
            button.AddComponent<ClickProxy>().OnClick = onClick;
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(button.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            labelObject.transform.localScale = new Vector3(1f / scale.x, 1f / scale.z, 1f);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = labelText;
            label.fontSize = 0.10f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.95f, 0.90f, 0.78f);
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.rectTransform.sizeDelta = new Vector2(scale.x - 0.12f, scale.z - 0.04f);
            return button;
        }

        private sealed class EffectButton
        {
            public EffectButton(GameObject body, TextMeshPro label)
            {
                Body = body;
                Label = label;
            }

            public GameObject Body { get; }
            public TextMeshPro Label { get; }
            public InventionActiveEffect Effect { get; set; }
        }
    }
}
