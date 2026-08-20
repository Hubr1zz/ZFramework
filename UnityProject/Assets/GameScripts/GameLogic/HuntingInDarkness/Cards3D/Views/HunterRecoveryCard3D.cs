using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    public sealed class HunterRecoveryCard3D : CardView3D
    {
        private static readonly Color AvailableColor = new(0.34f, 0.18f, 0.14f);
        private static readonly Color HealthyColor = new(0.14f, 0.25f, 0.18f);
        private HunterInstance hunter;
        private HunterBodyPart bodyPart;
        private TextMeshPro nameText;
        private TextMeshPro healthText;
        private TextMeshPro hintText;
        private bool canRecover;
        private string reason = string.Empty;

        public HunterBodyPart BodyPart => bodyPart;
        public System.Action<HunterRecoveryCard3D> OnRecoveryRequested;
        public override string DisplayName => GetBodyPartName(bodyPart);
        protected override CardCategory GetDefaultCategory() => CardCategory.HunterProfile;

        public static HunterRecoveryCard3D Create(HunterInstance hunter, HunterBodyPart bodyPart, Transform parent, Vector3 localPosition)
        {
            var gameObject = new GameObject($"Recovery_{bodyPart}");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<HunterRecoveryCard3D>();
            card.hunter = hunter;
            card.bodyPart = bodyPart;
            card.InitView(localPosition);
            return card;
        }

        public void ConfigureState(bool recoverable, string unavailableReason)
        {
            canRecover = recoverable;
            reason = unavailableReason ?? string.Empty;
            ApplyVisuals();
        }

        protected override void BuildTextFields()
        {
            if (nameText != null) return;
            float textY = CD * 0.5f + 0.003f;
            nameText = MakeText("Name", new Vector3(0f, textY, CH * 0.34f), 0.10f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.20f));
            healthText = MakeText("Health", new Vector3(0f, textY, 0f), 0.13f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.24f));
            hintText = MakeText("Hint", new Vector3(0f, textY, -CH * 0.36f), 0.06f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.20f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null || hunter == null) return;
            _bodyRenderer.material.color = canRecover ? (IsHovered ? AvailableColor * 1.2f : AvailableColor) : HealthyColor;
            if (nameText == null) return;
            HunterRecoveryRules.GetHealth(hunter, bodyPart, out int currentHealth, out int maximumHealth);
            nameText.text = GetBodyPartName(bodyPart);
            healthText.text = $"{currentHealth}/{maximumHealth}";
            hintText.text = canRecover ? "点击休养" : reason;
        }

        protected override void OnMouseDown()
        {
            if (canRecover) OnRecoveryRequested?.Invoke(this);
        }

        private static string GetBodyPartName(HunterBodyPart part)
        {
            return part switch
            {
                HunterBodyPart.Head => "头部",
                HunterBodyPart.Torso => "躯干",
                HunterBodyPart.Arms => "手臂",
                HunterBodyPart.Legs => "腿部",
                _ => "未知部位"
            };
        }
    }
}
