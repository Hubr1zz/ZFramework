using HuntingInDarkness.GameCore.Settlement;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    public sealed class SettlementFacilityDutyCard3D : CardView3D
    {
        private SettlementFacilityDutyDefinition definition;
        private TextMeshPro nameText;
        private TextMeshPro detailText;
        public System.Action<SettlementFacilityDutyCard3D> Clicked;
        public SettlementFacilityDutyDefinition Definition => definition;
        public override string DisplayName => definition?.DisplayName ?? "设施岗位";

        public static SettlementFacilityDutyCard3D Create(SettlementFacilityDutyDefinition dutyDefinition, Transform parent, Vector3 localPosition)
        {
            var gameObject = new GameObject($"FacilityDuty_{dutyDefinition.DutyId}");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<SettlementFacilityDutyCard3D>();
            card.SetDefinition(dutyDefinition);
            card.InitView(localPosition);
            return card;
        }

        private void SetDefinition(SettlementFacilityDutyDefinition dutyDefinition) => definition = dutyDefinition;

        protected override CardCategory GetDefaultCategory() => CardCategory.Invention;

        protected override void BuildTextFields()
        {
            if (nameText != null) return;
            float textY = CD * 0.5f + 0.003f;
            nameText = MakeText("Name", new Vector3(0f, textY, CH * 0.28f), 0.08f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.22f));
            detailText = MakeText("Detail", new Vector3(0f, textY, -CH * 0.20f), 0.055f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.28f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null || definition == null) return;
            _bodyRenderer.material.color = IsHovered ? new Color(0.38f, 0.34f, 0.20f) : new Color(0.25f, 0.24f, 0.17f);
            if (nameText == null) return;
            nameText.text = DisplayName;
            detailText.text = $"{definition.RequiredFacilityId} · d{definition.DiceSides} · {definition.DurationSeasons}季";
        }

        protected override void OnMouseDown()
        {
            Clicked?.Invoke(this);
            base.OnMouseDown();
        }
    }
}
