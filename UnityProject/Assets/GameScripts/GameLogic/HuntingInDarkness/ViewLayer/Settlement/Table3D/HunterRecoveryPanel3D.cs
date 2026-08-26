using System;
using System.Collections.Generic;
using Cards3D;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using TMPro;
using UnityEngine;

namespace UI
{
    public sealed class HunterRecoveryPanel3D : WorldSpaceViewPanel
    {
        private static readonly HunterBodyPart[] BodyParts = { HunterBodyPart.Head, HunterBodyPart.Torso, HunterBodyPart.Arms, HunterBodyPart.Legs };
        private readonly List<HunterRecoveryCard3D> cards = new();
        private TextMeshPro summaryText;
        private TextMeshPro statusText;
        private HunterInstance hunter;
        private SettlementInstance settlement;
        private PlayableSettlementContentCatalog catalog;
        private Func<int, HunterBodyPart, UniTask<RecoverHunterCommandResult>> recoverCommand;
        private Func<int, ItemData, HunterBodyPart, UniTask<SettlementConsumableCommandResult>> consumableCommand;
        private ItemData consumableItem;
        private bool consumableMode;
        private bool isBuilt;
        private bool isSubmitting;

        public static HunterRecoveryPanel3D Create(Transform parent)
        {
            var gameObject = new GameObject("HunterRecoveryPanel3D");
            gameObject.transform.SetParent(parent, false);
            var panel = gameObject.AddComponent<HunterRecoveryPanel3D>();
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
            SetSize(5.6f, 3.4f);
            summaryText = BuildText("Summary", new Vector3(0f, 0.015f, 1.12f), 0.085f, new Vector2(4.9f, 0.36f));
            statusText = BuildText("Status", new Vector3(0f, 0.015f, -1.20f), 0.075f, new Vector2(4.9f, 0.30f));
            BuildCloseButton();
        }

        public void Open(HunterInstance selectedHunter, SettlementInstance settlementData, PlayableSettlementContentCatalog content, Func<int, HunterBodyPart, UniTask<RecoverHunterCommandResult>> command, Vector3 worldPosition)
        {
            if (selectedHunter == null || settlementData == null) return;
            hunter = selectedHunter;
            settlement = settlementData;
            catalog = content;
            recoverCommand = command;
            consumableCommand = null;
            consumableItem = null;
            consumableMode = false;
            isSubmitting = false;
            RebuildCards();
            ShowAt(worldPosition);
        }

        public void OpenConsumable(HunterInstance selectedHunter, ItemData item, SettlementInstance settlementData, Func<int, ItemData, HunterBodyPart, UniTask<SettlementConsumableCommandResult>> command, Vector3 worldPosition)
        {
            if (selectedHunter == null || item == null || settlementData == null) return;
            hunter = selectedHunter;
            consumableItem = item;
            settlement = settlementData;
            catalog = null;
            recoverCommand = null;
            consumableCommand = command;
            consumableMode = true;
            isSubmitting = false;
            RebuildCards();
            ShowAt(worldPosition);
        }

        public void RefreshVisible()
        {
            if (!gameObject.activeSelf || hunter == null || settlement == null || isSubmitting) return;
            if (!ReferenceEquals(settlement.GetHunter(hunter.InstanceId), hunter) || !hunter.IsAlive || !hunter.IsAvailable)
            {
                Hide();
                return;
            }
            RebuildCards();
        }

        private void RebuildCards()
        {
            ClearCards();
            Title.text = consumableMode ? $"{hunter.Name} · 使用 {consumableItem.itemName}" : $"{hunter.Name} · 营火休养";
            summaryText.text = consumableMode ? $"使用 {consumableItem.itemName}，恢复一个受伤部位 {consumableItem.ConsumableEffectAmount} 点普通生命。" : $"每次消耗 {FormatCost()}，恢复一个部位 {catalog?.RecoveryAmount ?? 1} 点普通生命。永久损伤与症状不会消失。";
            float spacing = CardView3D.CW + 0.24f;
            float startX = -1.5f * spacing;
            for (int index = 0; index < BodyParts.Length; index++)
            {
                HunterBodyPart bodyPart = BodyParts[index];
                HunterRecoveryCard3D card = HunterRecoveryCard3D.Create(hunter, bodyPart, ContentRoot, new Vector3(startX + index * spacing, 0f, -0.05f));
                card.OnRecoveryRequested = RequestRecovery;
                card.ConfigureState(CanRecover(bodyPart, out string reason), reason);
                cards.Add(card);
            }
            statusText.text = HasRecoverablePart() ? (consumableMode ? "选择受伤部位使用。" : "选择一张受伤部位卡进行休养。") : (consumableMode ? "该猎人没有可使用的普通伤势。" : "该猎人没有可恢复的普通伤势。");
        }

        private void RequestRecovery(HunterRecoveryCard3D card)
        {
            if (card == null || isSubmitting) return;
            RecoverAsync(card.BodyPart).Forget();
        }

        private async UniTaskVoid RecoverAsync(HunterBodyPart bodyPart)
        {
            if ((consumableMode ? consumableCommand == null : recoverCommand == null))
            {
                statusText.text = consumableMode ? "消耗品使用命令尚未接入。" : "休养命令尚未接入。";
                return;
            }
            isSubmitting = true;
            SetCardsEnabled(false);
            try
            {
                string resultText;
                if (consumableMode)
                {
                    SettlementConsumableCommandResult result = await consumableCommand.Invoke(hunter.InstanceId, consumableItem, bodyPart);
                    resultText = result.Succeeded ? $"使用成功，恢复 {result.Recovery.RecoveredHealth} 点生命（{result.Recovery.CurrentHealth}/{result.Recovery.MaximumHealth}）。" : result.Reason;
                }
                else
                {
                    RecoverHunterCommandResult result = await recoverCommand.Invoke(hunter.InstanceId, bodyPart);
                    resultText = result.Succeeded ? $"恢复 {result.Recovery.RecoveredHealth} 点生命（{result.Recovery.CurrentHealth}/{result.Recovery.MaximumHealth}）。" : result.Reason;
                }
                if (this == null) return;
                isSubmitting = false;
                RebuildCards();
                statusText.text = resultText;
                return;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (this == null) return;
                isSubmitting = false;
                RebuildCards();
                statusText.text = consumableMode ? "消耗品使用命令执行异常，请重试。" : "休养命令执行异常，请重试。";
            }
        }

        private bool CanRecover(HunterBodyPart bodyPart, out string reason)
        {
            if (!HunterRecoveryRules.CanRecover(hunter, bodyPart, out reason)) return false;
            if (consumableMode)
            {
                if (settlement.GetStoredItem(consumableItem) <= 0)
                {
                    reason = $"缺少 {consumableItem.itemName}";
                    return false;
                }
                return true;
            }
            int cost = catalog?.RecoveryCost ?? 0;
            if (cost == 0) return true;
            if (catalog?.RecoveryCostItem == null)
            {
                reason = "休养成本尚未配置。";
                return false;
            }
            if (settlement.GetResource(catalog.RecoveryCostItem) >= cost) return true;
            reason = $"缺少 {catalog.RecoveryCostItem.itemName}";
            return false;
        }

        private bool HasRecoverablePart()
        {
            foreach (HunterBodyPart bodyPart in BodyParts)
                if (CanRecover(bodyPart, out _)) return true;
            return false;
        }

        private string FormatCost()
        {
            int cost = catalog?.RecoveryCost ?? 0;
            if (cost == 0) return "无需物资";
            return catalog?.RecoveryCostItem != null ? $"{catalog.RecoveryCostItem.itemName} ×{cost}" : "未配置物资";
        }

        private void SetCardsEnabled(bool enabled)
        {
            foreach (HunterRecoveryCard3D card in cards)
                if (card != null && card.TryGetComponent(out Collider collider))
                    collider.enabled = enabled;
        }

        private void ClearCards()
        {
            foreach (HunterRecoveryCard3D card in cards)
                if (card != null) Destroy(card.gameObject);
            cards.Clear();
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
    }
}
