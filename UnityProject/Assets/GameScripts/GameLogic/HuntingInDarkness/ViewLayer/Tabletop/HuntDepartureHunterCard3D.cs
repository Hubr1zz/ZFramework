using System;
using Cards3D;
using HuntingInDarkness.Data;
using TMPro;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    /// <summary>仅用于远征整备桌的可拖拽猎人投影；规则状态仍由 HunterInstance 拥有。</summary>
    public sealed class HuntDepartureHunterCard3D : SlotDraggableCardView3D
    {
        private TextMeshPro nameText;
        private TextMeshPro statsText;
        private string requiredDropScope = string.Empty;

        public HunterInstance Hunter { get; private set; }
        public System.Action PlacementChanged;
        public override string DisplayName => Hunter?.Name ?? "未知猎人";

        public static HuntDepartureHunterCard3D Create(HunterInstance hunter, Transform parent)
        {
            var gameObject = new GameObject(hunter != null ? $"DepartureHunter_{hunter.Name}" : "DepartureHunter_Invalid");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<HuntDepartureHunterCard3D>();
            card.Hunter = hunter;
            card.InitView(Vector3.zero);
            return card;
        }

        public void ConfigureDropScope(string dropScope)
        {
            requiredDropScope = dropScope ?? string.Empty;
            EnableDrag = Hunter?.IsAvailable == true;
        }

        protected override CardCategory GetDefaultCategory() => CardCategory.HunterProfile;

        protected override bool CanDropInto(CardSlot slot) => slot != null && string.Equals(slot.DropScope, requiredDropScope, StringComparison.Ordinal) && base.CanDropInto(slot);

        protected override void OnPlacedInSlot(CardSlot slot) => PlacementChanged?.Invoke();

        protected override void BuildTextFields()
        {
            float textHeight = Depth * 0.5f + 0.003f;
            nameText = MakeText("Name", new Vector3(0f, textHeight, Height * 0.28f), 0.105f, TextAlignmentOptions.Center, new Vector2(Width - 0.08f, 0.25f));
            statsText = MakeText("Stats", new Vector3(0f, textHeight, -Height * 0.10f), 0.073f, TextAlignmentOptions.Center, new Vector2(Width - 0.10f, 0.52f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null)
                return;
            bool available = Hunter?.IsAvailable == true;
            _bodyRenderer.material.color = !available ? new Color(0.20f, 0.12f, 0.12f) : IsHovered ? new Color(0.34f, 0.46f, 0.58f) : new Color(0.21f, 0.30f, 0.42f);
            if (nameText == null)
                return;
            nameText.text = Hunter?.Name ?? "无效猎人";
            nameText.color = available ? new Color(0.88f, 0.92f, 0.96f) : new Color(0.65f, 0.45f, 0.45f);
            statsText.text = Hunter != null ? $"意志 {Hunter.Willpower}/{Hunter.WillpowerMax}\n力 {Hunter.Stats?.strength ?? 0} · 敏 {Hunter.Stats?.evasion ?? 0}" : "数据缺失";
            statsText.color = new Color(0.68f, 0.74f, 0.82f);
        }
    }
}
