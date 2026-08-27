using HuntingInDarkness.Data;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    public sealed class SettlementFacilityDutyLauncherCard3D : CardView3D
    {
        private SettlementInstance settlement;
        private TextMeshPro titleText;
        private TextMeshPro statusText;
        public System.Action Clicked;
        public override string DisplayName => "设施值守";
        protected override CardCategory GetDefaultCategory() => CardCategory.Invention;

        public static SettlementFacilityDutyLauncherCard3D Create(Transform parent, Vector3 localPosition)
        {
            var gameObject = new GameObject("SettlementFacilityDutyLauncherCard3D");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<SettlementFacilityDutyLauncherCard3D>();
            card.InitView(localPosition);
            return card;
        }

        public void Configure(SettlementInstance settlementData)
        {
            settlement = settlementData;
            ApplyVisuals();
        }

        protected override void BuildTextFields()
        {
            if (titleText != null) return;
            float textY = CD * 0.5f + 0.003f;
            titleText = MakeText("Title", new Vector3(0f, textY, CH * 0.30f), 0.09f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.22f));
            statusText = MakeText("Status", new Vector3(0f, textY, -CH * 0.28f), 0.06f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.26f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null || titleText == null) return;
            _bodyRenderer.material.color = IsHovered ? new Color(0.39f, 0.31f, 0.19f) : new Color(0.28f, 0.22f, 0.14f);
            titleText.text = "设施值守";
            int activeCount = 0;
            foreach (HuntingInDarkness.GameCore.Settlement.SettlementFacilityDutyState duty in settlement?.FacilityDuties ?? new System.Collections.Generic.List<HuntingInDarkness.GameCore.Settlement.SettlementFacilityDutyState>())
                if (duty != null && duty.Status == HuntingInDarkness.GameCore.Settlement.SettlementFacilityDutyStateStatus.Active) activeCount++;
            statusText.text = settlement == null ? "尚未配置" : $"人口 {settlement.Population} · 岗位 {activeCount}";
        }

        protected override void OnMouseDown() => Clicked?.Invoke();
    }
}
