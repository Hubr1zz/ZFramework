using Cards3D;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>点击猎人卡后铺在营地桌面的 3D 装备查看板；装备变更入口预留给后续 ActionQueue 命令。</summary>
    public sealed class HunterEquipmentPanel3D : WorldSpaceViewPanel
    {
        private const float PanelWidth = 4.2f;
        private const float PanelHeight = 3.9f;

        private TextMeshPro statsText;
        private SlotGrid equipmentGrid;

        public static HunterEquipmentPanel3D Create(Transform parent)
        {
            var gameObject = new GameObject("HunterEquipmentPanel3D");
            gameObject.transform.SetParent(parent, false);
            var panel = gameObject.AddComponent<HunterEquipmentPanel3D>();
            panel.Build();
            panel.Hide();
            return panel;
        }

        public void Show(HunterInstance hunter, Vector3 worldPosition)
        {
            if (hunter == null) return;
            ClearEquipmentCards();
            Title.text = $"{hunter.Name} · 装备板";
            statsText.text = BuildStats(hunter);

            if (hunter.Equipment != null)
                foreach (ItemInstance item in hunter.Equipment)
                {
                    if (item?.Data == null) continue;
                    SettlementItemCard3D card = SettlementItemCard3D.Create(item.Data, item.Count, ContentRoot);
                    equipmentGrid.TryPlaceCard(card);
                }

            ShowAt(worldPosition);
        }

        private void Build()
        {
            BuildBase();
            SetSize(PanelWidth, PanelHeight);

            var statsObject = new GameObject("HunterStats");
            statsObject.transform.SetParent(ContentRoot, false);
            statsObject.transform.localPosition = new Vector3(-1.25f, 0.015f, -0.05f);
            statsObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            statsText = statsObject.AddComponent<TextMeshPro>();
            statsText.fontSize = 0.105f;
            statsText.alignment = TextAlignmentOptions.TopLeft;
            statsText.color = new Color(0.82f, 0.82f, 0.78f);
            statsText.rectTransform.sizeDelta = new Vector2(1.35f, 2.8f);

            equipmentGrid = SlotGrid.Create(ContentRoot, new Vector3(0.72f, 0.015f, -0.18f), 3, 3, CardView3D.CW + 0.06f, CardView3D.CH + 0.06f, 0.10f, false, CardCategory.Equipment);
            equipmentGrid.OccupantsDraggable = false;
            foreach (CardSlot slot in equipmentGrid.Slots)
                slot.AllowOccupantDrag = false;
            equipmentGrid.AddLabel("装备槽");
            BuildCloseButton();
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

        private void ClearEquipmentCards()
        {
            foreach (CardSlot slot in equipmentGrid.Slots)
            {
                CardView3D card = slot.OccupantCard;
                slot.ClearCard();
                if (card != null) Destroy(card.gameObject);
            }
        }

        private static string BuildStats(HunterInstance hunter)
        {
            return $"年龄  {hunter.Age}\n意志  {hunter.Willpower}/{hunter.WillpowerMax}\n命运  {hunter.Luck}\n压抑  {hunter.Insanity}\n\n力量  {hunter.Stats.strength}\n精准  {hunter.Stats.accuracy}\n敏捷  {hunter.Stats.evasion}\n移动  {hunter.Stats.movement}\n速度  {hunter.Stats.speed}\n\n装备  {hunter.Equipment?.Count ?? 0}/{EquipmentRules.MaximumEquipmentCount}";
        }
    }
}
