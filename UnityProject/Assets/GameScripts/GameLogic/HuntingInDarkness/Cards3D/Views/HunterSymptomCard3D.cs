using HuntingInDarkness.GameCore.Settlement;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    public sealed class HunterSymptomCard3D : CardView3D
    {
        private SymptomDefinition definition;
        private HunterSymptomState state;
        private TextMeshPro nameText;
        private TextMeshPro progressText;
        private TextMeshPro stateText;
        private bool isSelected;

        public SymptomDefinition Definition => definition;
        public System.Action<HunterSymptomCard3D> Selected;
        public override string DisplayName => definition?.DisplayName ?? "症状";
        protected override CardCategory GetDefaultCategory() => CardCategory.HunterProfile;

        public static HunterSymptomCard3D Create(SymptomDefinition definition, HunterSymptomState state, Transform parent, Vector3 localPosition)
        {
            var gameObject = new GameObject($"Symptom_{definition?.Id ?? "unknown"}");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<HunterSymptomCard3D>();
            card.definition = definition;
            card.state = state;
            card.InitView(localPosition);
            return card;
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            ApplyVisuals();
        }

        protected override void BuildTextFields()
        {
            if (nameText != null) return;
            float textY = CD * 0.5f + 0.003f;
            nameText = MakeText("Name", new Vector3(0f, textY, CH * 0.32f), 0.095f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.22f));
            progressText = MakeText("Progress", new Vector3(0f, textY, 0f), 0.11f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.24f));
            stateText = MakeText("State", new Vector3(0f, textY, -CH * 0.34f), 0.06f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.22f));
            ConfigureText(nameText);
            ConfigureText(progressText);
            ConfigureText(stateText);
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null || definition == null || state == null) return;
            _bodyRenderer.material.color = isSelected ? new Color(0.50f, 0.26f, 0.18f) : IsHovered ? new Color(0.38f, 0.24f, 0.22f) : new Color(0.25f, 0.16f, 0.17f);
            if (nameText == null) return;
            nameText.text = definition.DisplayName;
            progressText.text = $"内化 {state.InternalizationProgress}/{definition.InternalizationThreshold}";
            stateText.text = state.IsInternalized ? "已内化 · 仍可克服" : "尚未内化";
        }

        protected override void OnMouseDown() => Selected?.Invoke(this);

        private static void ConfigureText(TextMeshPro text)
        {
#if UNITY_6000_0_OR_NEWER
            text.textWrappingMode = TextWrappingModes.Normal;
#else
            text.enableWordWrapping = true;
#endif
            text.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
