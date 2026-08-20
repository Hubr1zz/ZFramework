using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    public sealed class HunterGrowthChoiceCard3D : CardView3D
    {
        private HunterInstance hunter;
        private HunterGrowthChoice choice;
        private TextMeshPro nameText;
        private TextMeshPro valueText;
        private TextMeshPro hintText;
        private bool canSpend;
        private string reason = string.Empty;

        public HunterGrowthChoice Choice => choice;
        public System.Action<HunterGrowthChoiceCard3D> Requested;
        public override string DisplayName => choice == HunterGrowthChoice.Courage ? "胆识成长" : "知识成长";
        protected override CardCategory GetDefaultCategory() => CardCategory.HunterProfile;

        public static HunterGrowthChoiceCard3D Create(HunterInstance hunter, HunterGrowthChoice choice, Transform parent, Vector3 localPosition)
        {
            var gameObject = new GameObject($"Growth_{choice}");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<HunterGrowthChoiceCard3D>();
            card.hunter = hunter;
            card.choice = choice;
            card.InitView(localPosition);
            return card;
        }

        public void ConfigureState(bool available, string unavailableReason)
        {
            canSpend = available;
            reason = unavailableReason ?? string.Empty;
            ApplyVisuals();
        }

        protected override void BuildTextFields()
        {
            if (nameText != null) return;
            float textY = CD * 0.5f + 0.003f;
            nameText = MakeText("Name", new Vector3(0f, textY, CH * 0.32f), 0.11f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.22f));
            valueText = MakeText("Value", new Vector3(0f, textY, 0f), 0.14f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.24f));
            hintText = MakeText("Hint", new Vector3(0f, textY, -CH * 0.34f), 0.06f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.22f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null || hunter == null) return;
            _bodyRenderer.material.color = canSpend ? IsHovered ? new Color(0.48f, 0.36f, 0.16f) : new Color(0.34f, 0.25f, 0.12f) : new Color(0.17f, 0.17f, 0.19f);
            if (nameText == null) return;
            bool courage = choice == HunterGrowthChoice.Courage;
            nameText.text = courage ? "胆识" : "知识";
            valueText.text = $"{(courage ? hunter.Courage : hunter.Understanding)}/{HunterAdvancementRules.MaximumGrowthAttribute}";
            hintText.text = canSpend ? "点击分配 1 点" : reason;
        }

        protected override bool CanHover() => canSpend;

        protected override void OnMouseDown()
        {
            if (canSpend) Requested?.Invoke(this);
        }
    }
}
