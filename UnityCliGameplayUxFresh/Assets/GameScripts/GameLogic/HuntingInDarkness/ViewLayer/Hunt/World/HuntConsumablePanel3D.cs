using System;
using System.Collections.Generic;
using Cards3D;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.ViewLayer.Hunt;
using TMPro;
using UnityEngine;

namespace UI.Hunt
{
    public sealed class HuntConsumablePanel3D : WorldSpaceViewPanel
    {
        private static readonly HunterBodyPart[] BodyParts = { HunterBodyPart.Head, HunterBodyPart.Torso, HunterBodyPart.Arms, HunterBodyPart.Legs };
        private readonly List<HunterRecoveryCard3D> cards = new();
        private TextMeshPro summaryText;
        private TextMeshPro statusText;
        private HunterInstance hunter;
        private string itemId = string.Empty;
        private string itemName = string.Empty;
        private int effectAmount;
        private IPlayableHuntConsumableInput command;
        private Action<HuntConsumableCommandResult> completed;
        private bool isBuilt;
        private bool isSubmitting;

        public bool IsSubmitting => isSubmitting;

        public static HuntConsumablePanel3D Create(Transform parent)
        {
            var gameObject = new GameObject("HuntConsumablePanel3D");
            gameObject.transform.SetParent(parent, false);
            var panel = gameObject.AddComponent<HuntConsumablePanel3D>();
            panel.EnsureBuilt();
            panel.Hide();
            return panel;
        }

        private void Awake() => EnsureBuilt();

        public void Open(HunterInstance owner, HuntCollectibleStackPresentation stack, IPlayableHuntConsumableInput input, Vector3 worldPosition, Action<HuntConsumableCommandResult> onCompleted)
        {
            if (isSubmitting || owner == null || !stack.CanUseInHunt || input == null) return;
            hunter = owner;
            itemId = stack.ContentId;
            itemName = stack.DisplayName;
            effectAmount = stack.EffectAmount;
            command = input;
            completed = onCompleted;
            isSubmitting = false;
            RebuildCards();
            ShowAt(worldPosition);
        }

        private void EnsureBuilt()
        {
            if (isBuilt) return;
            isBuilt = true;
            BuildBase();
            SetSize(5.6f, 3.4f);
            summaryText = BuildText("Summary", new Vector3(0f, 0.015f, 1.12f), 0.085f, new Vector2(4.9f, 0.36f));
            statusText = BuildText("Status", new Vector3(0f, 0.015f, -1.20f), 0.075f, new Vector2(4.9f, 0.30f));
            BuildCloseButton();
        }

        private void RebuildCards()
        {
            ClearCards();
            Title.text = $"{hunter.Name} · 使用 {itemName}";
            summaryText.text = $"消耗 1 件，恢复一个受伤部位 {effectAmount} 点普通生命。";
            float spacing = CardView3D.CW + 0.24f;
            float startX = -1.5f * spacing;
            bool hasRecoverablePart = false;
            for (int index = 0; index < BodyParts.Length; index++)
            {
                HunterBodyPart bodyPart = BodyParts[index];
                HunterRecoveryCard3D card = HunterRecoveryCard3D.Create(hunter, bodyPart, ContentRoot, new Vector3(startX + index * spacing, 0f, -0.05f));
                bool canRecover = HunterRecoveryRules.CanRecover(hunter, bodyPart, out string reason);
                card.ConfigureState(canRecover && !isSubmitting, isSubmitting ? "正在提交" : reason, "点击使用");
                card.OnRecoveryRequested = RequestUse;
                cards.Add(card);
                hasRecoverablePart |= canRecover;
            }
            statusText.text = isSubmitting ? "正在提交狩猎消耗品命令……" : hasRecoverablePart ? "选择一张受伤部位卡。" : "该猎人没有可恢复的普通伤势。";
        }

        private void RequestUse(HunterRecoveryCard3D card)
        {
            if (card == null || isSubmitting || command == null) return;
            UseAsync(card.BodyPart).Forget();
        }

        private async UniTaskVoid UseAsync(HunterBodyPart bodyPart)
        {
            isSubmitting = true;
            RebuildCards();
            HuntConsumableCommandResult result;
            try
            {
                result = await command.UseConsumableAsync(hunter.InstanceId, itemId, bodyPart);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                result = HuntConsumableCommandResult.Failed("狩猎消耗品命令执行异常，请重试。");
            }
            if (this == null) return;
            isSubmitting = false;
            completed?.Invoke(result);
            if (result.Succeeded && result.RemainingCount <= 0)
            {
                Hide();
                return;
            }
            RebuildCards();
            statusText.text = result.Succeeded ? $"恢复 {result.Recovery.RecoveredHealth} 点生命，剩余 {result.RemainingCount} 件。" : result.Reason;
        }

        private TextMeshPro BuildText(string objectName, Vector3 localPosition, float fontSize, Vector2 size)
        {
            var textObject = new GameObject(objectName);
            textObject.transform.SetParent(ContentRoot, false);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TextMeshPro text = textObject.AddComponent<TextMeshPro>();
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.82f, 0.82f, 0.78f);
            text.rectTransform.sizeDelta = size;
            return text;
        }

        private void BuildCloseButton()
        {
            GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = "CloseButton";
            button.transform.SetParent(transform, false);
            button.transform.localPosition = new Vector3(2.42f, 0.03f, 1.46f);
            button.transform.localScale = new Vector3(0.5f, 0.04f, 0.22f);
            button.GetComponent<Renderer>().material.color = new Color(0.42f, 0.15f, 0.14f);
            button.AddComponent<ClickProxy>().OnClick = Hide;
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(button.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            labelObject.transform.localScale = new Vector3(2f, 1f / 0.22f, 1f);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = "关闭";
            label.fontSize = 0.10f;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(0.45f, 0.18f);
        }

        private void ClearCards()
        {
            foreach (HunterRecoveryCard3D card in cards)
                if (card != null) Destroy(card.gameObject);
            cards.Clear();
        }

        private void OnDestroy() => ClearCards();
    }
}
