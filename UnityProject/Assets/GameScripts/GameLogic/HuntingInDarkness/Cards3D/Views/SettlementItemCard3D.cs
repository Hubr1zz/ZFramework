using HuntingInDarkness.Data;
using System;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    /// <summary>营地 ItemData 的 3D 卡牌视图；与旧战斗测试用 EquipmentCardData 解耦。</summary>
    public sealed class SettlementItemCard3D : SlotDraggableCardView3D
    {
        private static readonly Color WeaponColor = new(0.34f, 0.25f, 0.20f);
        private static readonly Color ArmorColor = new(0.22f, 0.30f, 0.36f);
        private static readonly Color ConsumableColor = new(0.25f, 0.34f, 0.25f);

        private TextMeshPro nameText;
        private TextMeshPro typeText;
        private TextMeshPro descriptionText;
        private string requiredDropScope;
        private Action<SettlementItemCard3D> dropRequested;
        private bool requestInFlight;

        public ItemData Item { get; private set; }
        public ItemInstance Instance { get; private set; }
        public int Count { get; private set; }
        public override string DisplayName => Item != null ? $"{Item.itemName} ×{Count}" : base.DisplayName;

        protected override CardCategory GetDefaultCategory() => CardCategory.Equipment;

        public static SettlementItemCard3D Create(ItemData item, int count, Transform parent, Vector3 localPosition = default)
        {
            var gameObject = new GameObject(item != null ? $"Item_{item.itemName}" : "Item_Invalid");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<SettlementItemCard3D>();
            card.Item = item;
            card.Count = Mathf.Max(1, count);
            card.InitView(localPosition);
            return card;
        }

        public static SettlementItemCard3D Create(ItemInstance instance, Transform parent, Vector3 localPosition = default)
        {
            SettlementItemCard3D card = Create(instance?.Data, instance?.Count ?? 1, parent, localPosition);
            card.Instance = instance;
            return card;
        }

        public void ConfigureCommandDrop(string dropScope, Action<SettlementItemCard3D> onDropRequested)
        {
            requiredDropScope = dropScope ?? string.Empty;
            dropRequested = onDropRequested;
            EnableDrag = dropRequested != null;
        }

        public void CompleteDropRequest(bool allowRetry)
        {
            requestInFlight = false;
            EnableDrag = allowRetry && dropRequested != null;
        }

        protected override void BuildTextFields()
        {
            float textY = CD * 0.5f + 0.003f;
            nameText = MakeText("Name", new Vector3(0f, textY, CH * 0.34f), 0.105f, TextAlignmentOptions.Center, new Vector2(CW - 0.08f, 0.20f));
            typeText = MakeText("Type", new Vector3(0f, textY, CH * 0.12f), 0.075f, TextAlignmentOptions.Center, new Vector2(CW - 0.08f, 0.14f));
            descriptionText = MakeText("Description", new Vector3(0f, textY, -CH * 0.13f), 0.065f, TextAlignmentOptions.Center, new Vector2(CW - 0.10f, 0.40f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null) return;
            _bodyRenderer.material.color = GetColor();
            if (nameText == null) return;
            nameText.text = Item != null ? Item.itemName : "无效装备";
            typeText.text = Item != null ? $"{GetTypeName(Item.itemType)}  ×{Count}" : string.Empty;
            descriptionText.text = Item != null ? Item.description : string.Empty;
            nameText.color = new Color(0.92f, 0.88f, 0.78f);
            typeText.color = new Color(0.68f, 0.73f, 0.78f);
            descriptionText.color = new Color(0.76f, 0.76f, 0.72f);
        }

        protected override bool CanDropInto(CardSlot slot)
        {
            return !requestInFlight && slot != null && string.Equals(slot.DropScope, requiredDropScope, StringComparison.Ordinal) && base.CanDropInto(slot);
        }

        protected override bool TryHandleSlotDrop(CardSlot slot)
        {
            if (dropRequested == null) return false;
            RestoreDragHome();
            requestInFlight = true;
            EnableDrag = false;
            dropRequested.Invoke(this);
            return true;
        }

        private Color GetColor()
        {
            if (Item == null) return new Color(0.25f, 0.20f, 0.20f);
            Color color = Item.itemType switch
            {
                ItemType.Weapon => WeaponColor,
                ItemType.Armor => ArmorColor,
                _ => ConsumableColor
            };
            return IsHovered ? color * 1.25f : color;
        }

        private static string GetTypeName(ItemType itemType) => itemType switch
        {
            ItemType.Weapon => "武器",
            ItemType.Armor => "防具",
            ItemType.Consumable => "消耗品",
            _ => "资源"
        };
    }
}
