using HuntingInDarkness.GameCore.Settlement;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    public sealed class WeaponTrainingCard3D : CardView3D
    {
        private WeaponMasteryFamilyDefinition family;
        private TextMeshPro nameText;
        private TextMeshPro valueText;
        private TextMeshPro costText;
        private TextMeshPro hintText;
        private int currentValue;
        private int experience;
        private string costLabel = string.Empty;
        private bool canTrain;
        private string reason = string.Empty;

        public WeaponMasteryFamilyDefinition Family => family;
        public System.Action<WeaponTrainingCard3D> Requested;
        public override string DisplayName => family?.DisplayName ?? base.DisplayName;
        protected override CardCategory GetDefaultCategory() => CardCategory.HunterProfile;

        public static WeaponTrainingCard3D Create(WeaponMasteryFamilyDefinition family, Transform parent, Vector3 localPosition)
        {
            var gameObject = new GameObject($"WeaponTraining_{family.Id}");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<WeaponTrainingCard3D>();
            card.family = family;
            card.InitView(localPosition);
            return card;
        }

        public void ConfigureState(int value, int gain, string configuredCostLabel, bool available, string unavailableReason)
        {
            currentValue = Mathf.Max(0, value);
            experience = Mathf.Max(1, gain);
            costLabel = configuredCostLabel ?? string.Empty;
            canTrain = available;
            reason = unavailableReason ?? string.Empty;
            ApplyVisuals();
        }

        protected override void BuildTextFields()
        {
            if (nameText != null) return;
            float textY = CD * 0.5f + 0.003f;
            nameText = MakeText("Name", new Vector3(0f, textY, CH * 0.34f), 0.095f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.20f));
            valueText = MakeText("Value", new Vector3(0f, textY, CH * 0.08f), 0.12f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.20f));
            costText = MakeText("Cost", new Vector3(0f, textY, -CH * 0.14f), 0.06f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.18f));
            hintText = MakeText("Hint", new Vector3(0f, textY, -CH * 0.38f), 0.055f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.20f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null || family == null) return;
            _bodyRenderer.material.color = canTrain ? IsHovered ? new Color(0.25f, 0.42f, 0.52f) : new Color(0.17f, 0.31f, 0.40f) : new Color(0.16f, 0.17f, 0.18f);
            if (nameText == null) return;
            nameText.text = family.DisplayName;
            valueText.text = $"{currentValue}  +{experience}";
            costText.text = costLabel;
            hintText.text = canTrain ? "点击训练" : reason;
        }

        protected override bool CanHover() => canTrain;

        protected override void OnMouseDown()
        {
            if (canTrain) Requested?.Invoke(this);
        }
    }
}
