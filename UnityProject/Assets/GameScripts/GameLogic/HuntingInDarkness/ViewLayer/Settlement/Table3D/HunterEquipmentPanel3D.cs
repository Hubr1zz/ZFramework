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
    /// <summary>猎人卡打开的 3D 装备桌板；拖放只发命令，权威状态由 Settlement ActionQueue 提交。</summary>
    public sealed class HunterEquipmentPanel3D : WorldSpaceViewPanel
    {
        private const float PanelWidth = 6.2f;
        private const float PanelHeight = 4.5f;
        private const string EquipmentDropScope = "hunter-equipment";
        private const string StorageDropScope = "settlement-equipment-storage";
        private const int StoragePageSize = 9;

        private readonly List<SettlementItemCard3D> storageCards = new();
        private readonly List<ItemData> storedItems = new();
        private TextMeshPro statsText;
        private TextMeshPro statusText;
        private SlotGrid storageGrid;
        private SlotGrid equipmentGrid;
        private HunterInstance hunter;
        private SettlementInstance settlement;
        private IReadOnlyList<ItemData> items = Array.Empty<ItemData>();
        private Func<int, ItemData, UniTask<SettlementEquipmentCommandResult>> equipCommand;
        private Func<int, int, UniTask<SettlementEquipmentCommandResult>> unequipCommand;
        private Action<HunterInstance> recoveryRequested;
        private Action<HunterInstance> advancementRequested;
        private Action<HunterInstance> symptomRequested;
        private GameObject recoveryButton;
        private GameObject advancementButton;
        private GameObject symptomButton;
        private GameObject previousPageButton;
        private GameObject nextPageButton;
        private int storagePage;
        private bool isBuilt;

        public static HunterEquipmentPanel3D Create(Transform parent)
        {
            var gameObject = new GameObject("HunterEquipmentPanel3D");
            gameObject.transform.SetParent(parent, false);
            var panel = gameObject.AddComponent<HunterEquipmentPanel3D>();
            panel.EnsureBuilt();
            panel.Hide();
            return panel;
        }

        private void Awake() => EnsureBuilt();

        public void EnsureBuilt()
        {
            if (isBuilt) return;
            isBuilt = true;
            Build();
        }

        public void ConfigureCommands(Func<int, ItemData, UniTask<SettlementEquipmentCommandResult>> onEquip, Func<int, int, UniTask<SettlementEquipmentCommandResult>> onUnequip, Action<HunterInstance> onRecoveryRequested = null, Action<HunterInstance> onAdvancementRequested = null, Action<HunterInstance> onSymptomRequested = null)
        {
            equipCommand = onEquip;
            unequipCommand = onUnequip;
            recoveryRequested = onRecoveryRequested;
            advancementRequested = onAdvancementRequested;
            symptomRequested = onSymptomRequested;
        }

        public void Show(HunterInstance selectedHunter, SettlementInstance settlementData, IReadOnlyList<ItemData> availableItems, Vector3 worldPosition)
        {
            if (selectedHunter == null || settlementData == null) return;
            hunter = selectedHunter;
            settlement = settlementData;
            items = availableItems ?? Array.Empty<ItemData>();
            Title.text = $"{hunter.Name} · 装备桌";
            statusText.text = "将仓库卡拖入右侧槽位；将已装备卡拖回左侧仓库即可卸下。";
            RebuildCards();
            ShowAt(worldPosition);
        }

        public void RefreshVisible()
        {
            if (!gameObject.activeSelf || hunter == null || settlement == null) return;
            RebuildCards();
        }

        private void Build()
        {
            BuildBase();
            SetSize(PanelWidth, PanelHeight);
            BuildStats();
            BuildStatus();

            storageGrid = SlotGrid.Create(ContentRoot, new Vector3(-1.52f, 0.015f, -0.20f), 3, 3, CardView3D.CW + 0.06f, CardView3D.CH + 0.06f, 0.10f, false, CardCategory.Equipment);
            storageGrid.DropScope = StorageDropScope;
            foreach (CardSlot slot in storageGrid.Slots)
                slot.DropScope = StorageDropScope;
            storageGrid.AddLabel("装备仓库");

            equipmentGrid = SlotGrid.Create(ContentRoot, new Vector3(1.52f, 0.015f, -0.20f), 3, 3, CardView3D.CW + 0.06f, CardView3D.CH + 0.06f, 0.10f, false, CardCategory.Equipment);
            equipmentGrid.DropScope = EquipmentDropScope;
            foreach (CardSlot slot in equipmentGrid.Slots)
                slot.DropScope = EquipmentDropScope;
            equipmentGrid.AddLabel("猎人装备槽");
            previousPageButton = BuildPageButton("PreviousStoragePage", "<", new Vector3(-2.75f, 0.03f, 1.42f), -1);
            nextPageButton = BuildPageButton("NextStoragePage", ">", new Vector3(-0.30f, 0.03f, 1.42f), 1);
            recoveryButton = BuildRecoveryButton();
            advancementButton = BuildAdvancementButton();
            symptomButton = BuildSymptomButton();
            BuildCloseButton();
        }

        private void BuildStats()
        {
            var statsObject = new GameObject("HunterStats");
            statsObject.transform.SetParent(ContentRoot, false);
            statsObject.transform.localPosition = new Vector3(0f, 0.015f, 1.74f);
            statsObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            statsText = statsObject.AddComponent<TextMeshPro>();
            statsText.fontSize = 0.09f;
            statsText.alignment = TextAlignmentOptions.Center;
            statsText.color = new Color(0.82f, 0.82f, 0.78f);
            statsText.rectTransform.sizeDelta = new Vector2(5.2f, 0.32f);
        }

        private void BuildStatus()
        {
            var statusObject = new GameObject("Status");
            statusObject.transform.SetParent(ContentRoot, false);
            statusObject.transform.localPosition = new Vector3(0f, 0.015f, -2.03f);
            statusObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            statusText = statusObject.AddComponent<TextMeshPro>();
            statusText.fontSize = 0.075f;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = new Color(0.72f, 0.74f, 0.70f);
            statusText.rectTransform.sizeDelta = new Vector2(5.2f, 0.25f);
        }

        private void BuildCloseButton()
        {
            GameObject buttonObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            buttonObject.name = "CloseButton";
            buttonObject.transform.SetParent(transform, false);
            buttonObject.transform.localPosition = new Vector3(PanelWidth * 0.5f - 0.38f, 0.03f, PanelHeight * 0.5f - 0.22f);
            buttonObject.transform.localScale = new Vector3(0.5f, 0.04f, 0.22f);
            buttonObject.GetComponent<Renderer>().material.color = new Color(0.42f, 0.15f, 0.14f);
            buttonObject.AddComponent<ClickProxy>().OnClick = Hide;

            var labelObject = new GameObject("CloseLabel");
            labelObject.transform.SetParent(buttonObject.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            labelObject.transform.localScale = new Vector3(2f, 1f / 0.22f, 1f);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = "关闭";
            label.fontSize = 0.10f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.95f, 0.85f, 0.82f);
            label.rectTransform.sizeDelta = new Vector2(0.45f, 0.18f);
        }

        private GameObject BuildPageButton(string name, string labelText, Vector3 localPosition, int direction)
        {
            GameObject buttonObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            buttonObject.name = name;
            buttonObject.transform.SetParent(transform, false);
            buttonObject.transform.localPosition = localPosition;
            buttonObject.transform.localScale = new Vector3(0.42f, 0.04f, 0.22f);
            buttonObject.GetComponent<Renderer>().material.color = new Color(0.22f, 0.28f, 0.36f);
            buttonObject.AddComponent<ClickProxy>().OnClick = () => ChangeStoragePage(direction);

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(buttonObject.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            labelObject.transform.localScale = new Vector3(1f / 0.42f, 1f / 0.22f, 1f);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = labelText;
            label.fontSize = 0.11f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.86f, 0.90f, 0.95f);
            label.rectTransform.sizeDelta = new Vector2(0.32f, 0.18f);
            return buttonObject;
        }

        private GameObject BuildRecoveryButton()
        {
            GameObject buttonObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            buttonObject.name = "RecoveryButton";
            buttonObject.transform.SetParent(transform, false);
            buttonObject.transform.localPosition = new Vector3(2.15f, 0.03f, 1.42f);
            buttonObject.transform.localScale = new Vector3(0.85f, 0.04f, 0.28f);
            buttonObject.GetComponent<Renderer>().material.color = new Color(0.38f, 0.18f, 0.14f);
            buttonObject.AddComponent<ClickProxy>().OnClick = () => recoveryRequested?.Invoke(hunter);
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(buttonObject.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            labelObject.transform.localScale = new Vector3(1f / 0.85f, 1f / 0.28f, 1f);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = "营火休养";
            label.fontSize = 0.095f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.95f, 0.84f, 0.76f);
            label.rectTransform.sizeDelta = new Vector2(0.72f, 0.22f);
            return buttonObject;
        }

        private GameObject BuildAdvancementButton()
        {
            GameObject buttonObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            buttonObject.name = "AdvancementButton";
            buttonObject.transform.SetParent(transform, false);
            buttonObject.transform.localPosition = new Vector3(1.10f, 0.03f, 1.42f);
            buttonObject.transform.localScale = new Vector3(0.85f, 0.04f, 0.28f);
            buttonObject.GetComponent<Renderer>().material.color = new Color(0.18f, 0.30f, 0.40f);
            buttonObject.AddComponent<ClickProxy>().OnClick = () => advancementRequested?.Invoke(hunter);
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(buttonObject.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            labelObject.transform.localScale = new Vector3(1f / 0.85f, 1f / 0.28f, 1f);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = "成长训练";
            label.fontSize = 0.095f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.82f, 0.90f, 0.98f);
            label.rectTransform.sizeDelta = new Vector2(0.72f, 0.22f);
            return buttonObject;
        }

        private GameObject BuildSymptomButton()
        {
            GameObject buttonObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            buttonObject.name = "SymptomButton";
            buttonObject.transform.SetParent(transform, false);
            buttonObject.transform.localPosition = new Vector3(0.38f, 0.03f, 1.42f);
            buttonObject.transform.localScale = new Vector3(0.58f, 0.04f, 0.28f);
            buttonObject.GetComponent<Renderer>().material.color = new Color(0.35f, 0.20f, 0.27f);
            buttonObject.AddComponent<ClickProxy>().OnClick = () => symptomRequested?.Invoke(hunter);
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(buttonObject.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            labelObject.transform.localScale = new Vector3(1f / 0.58f, 1f / 0.28f, 1f);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = "症状";
            label.fontSize = 0.09f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.95f, 0.82f, 0.90f);
            label.rectTransform.sizeDelta = new Vector2(0.48f, 0.22f);
            return buttonObject;
        }

        private void ChangeStoragePage(int direction)
        {
            storagePage += direction;
            RebuildCards();
        }

        private void RebuildCards()
        {
            ClearCards();
            statsText.text = BuildStats(hunter);
            recoveryButton.SetActive(IsWounded(hunter));
            advancementButton.SetActive(hunter.IsAvailable);
            symptomButton.SetActive(HasSymptoms(hunter));
            FillStorageCards();
            FillEquipmentCards();
        }

        private void FillStorageCards()
        {
            storedItems.Clear();
            foreach (ItemData item in items)
            {
                if (item == null || item.itemType == ItemType.Resource) continue;
                if (settlement.GetStoredEquipment(item) > 0)
                    storedItems.Add(item);
            }

            int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)storedItems.Count / StoragePageSize));
            storagePage = Mathf.Clamp(storagePage, 0, pageCount - 1);
            previousPageButton.SetActive(storagePage > 0);
            nextPageButton.SetActive(storagePage < pageCount - 1);
            statusText.text = storedItems.Count == 0 ? "装备仓库为空。制造装备后，可将卡牌拖入猎人装备槽。" : $"仓库 {storagePage + 1}/{pageCount} 页 · 拖入右侧装备；将右侧卡拖回仓库可卸下。";

            int startIndex = storagePage * StoragePageSize;
            int endIndex = Mathf.Min(startIndex + StoragePageSize, storedItems.Count);
            for (int itemIndex = startIndex; itemIndex < endIndex; itemIndex++)
            {
                ItemData item = storedItems[itemIndex];
                int count = settlement.GetStoredEquipment(item);
                CardSlot visualSlot = storageGrid.Slots[itemIndex - startIndex];
                Vector3 localPosition = ContentRoot.InverseTransformPoint(visualSlot.transform.position + Vector3.up * 0.013f);
                SettlementItemCard3D card = SettlementItemCard3D.Create(item, count, ContentRoot, localPosition);
                card.ConfigureCommandDrop(EquipmentDropScope, RequestEquip);
                storageCards.Add(card);
            }
        }

        private void FillEquipmentCards()
        {
            if (hunter.Equipment == null) return;
            foreach (ItemInstance item in hunter.Equipment)
            {
                if (item?.Data == null) continue;
                SettlementItemCard3D card = SettlementItemCard3D.Create(item, ContentRoot);
                if (!equipmentGrid.TryPlaceCard(card))
                {
                    Destroy(card.gameObject);
                    continue;
                }
                card.ConfigureCommandDrop(StorageDropScope, RequestUnequip);
            }
        }

        private void RequestEquip(SettlementItemCard3D card) => EquipAsync(card).Forget();

        private async UniTaskVoid EquipAsync(SettlementItemCard3D card)
        {
            if (card == null || equipCommand == null)
            {
                card?.CompleteDropRequest(true);
                statusText.text = "装备命令尚未接入。";
                return;
            }

            SettlementEquipmentCommandResult result;
            try
            {
                result = await equipCommand.Invoke(hunter.InstanceId, card.Item);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (this != null && card != null) card.CompleteDropRequest(true);
                if (this != null) statusText.text = "装备命令执行异常，请重试。";
                return;
            }
            if (result.Succeeded) return;
            if (this == null) return;
            if (card != null) card.CompleteDropRequest(true);
            statusText.text = result.Reason;
        }

        private void RequestUnequip(SettlementItemCard3D card) => UnequipAsync(card).Forget();

        private async UniTaskVoid UnequipAsync(SettlementItemCard3D card)
        {
            if (card == null || card.Instance == null || unequipCommand == null)
            {
                card?.CompleteDropRequest(true);
                statusText.text = "卸下命令尚未接入。";
                return;
            }

            SettlementEquipmentCommandResult result;
            try
            {
                result = await unequipCommand.Invoke(hunter.InstanceId, card.Instance.InstanceId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (this != null && card != null) card.CompleteDropRequest(true);
                if (this != null) statusText.text = "卸下命令执行异常，请重试。";
                return;
            }
            if (result.Succeeded) return;
            if (this == null) return;
            if (card != null) card.CompleteDropRequest(true);
            statusText.text = result.Reason;
        }

        private void ClearCards()
        {
            foreach (SettlementItemCard3D card in storageCards)
                if (card != null)
                    Destroy(card.gameObject);
            storageCards.Clear();

            foreach (CardSlot slot in equipmentGrid.Slots)
            {
                CardView3D card = slot.OccupantCard;
                slot.ClearCard();
                if (card != null) Destroy(card.gameObject);
            }
        }

        private static string BuildStats(HunterInstance hunter)
        {
            return $"年龄 {hunter.Age}  意志 {hunter.Willpower}/{hunter.WillpowerMax}  命运 {hunter.Luck}  压抑 {hunter.Insanity}    力 {hunter.Stats.strength}  准 {hunter.Stats.accuracy}  敏 {hunter.Stats.evasion}  移 {hunter.Stats.movement}  速 {hunter.Stats.speed}    装备 {hunter.Equipment?.Count ?? 0}/{EquipmentRules.MaximumEquipmentCount}";
        }

        private static bool IsWounded(HunterInstance hunter)
        {
            return HunterRecoveryRules.CanRecover(hunter, HunterBodyPart.Head, out _)
                || HunterRecoveryRules.CanRecover(hunter, HunterBodyPart.Torso, out _)
                || HunterRecoveryRules.CanRecover(hunter, HunterBodyPart.Arms, out _)
                || HunterRecoveryRules.CanRecover(hunter, HunterBodyPart.Legs, out _);
        }

        private static bool HasSymptoms(HunterInstance hunter)
        {
            if (hunter?.SymptomStates == null) return false;
            foreach (HunterSymptomState state in hunter.SymptomStates)
                if (state != null && !state.IsOvercome && PlayableSymptomRuntime.Catalog != null && PlayableSymptomRuntime.Catalog.TryGetById(state.SymptomId, out _))
                    return true;
            return false;
        }
    }
}
