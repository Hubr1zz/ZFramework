using HuntingInDarkness.Settlement;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    public sealed class WorkshopBlueprintCard3D : CardView3D
    {
        private static readonly Color AvailableColor = new(0.34f, 0.27f, 0.12f);
        private static readonly Color LockedColor = new(0.14f, 0.14f, 0.16f);
        private PlayableWorkshopDefinition definition;
        private TextMeshPro nameText;
        private TextMeshPro descriptionText;
        private TextMeshPro hintText;
        private bool canBuild;
        private string reason = string.Empty;

        public PlayableWorkshopDefinition Definition => definition;
        public System.Action<WorkshopBlueprintCard3D> OnConstructionRequested;
        public override string DisplayName => definition?.DisplayName ?? base.DisplayName;
        protected override CardCategory GetDefaultCategory() => CardCategory.Workshop;

        public static WorkshopBlueprintCard3D Create(PlayableWorkshopDefinition definition, Transform parent)
        {
            var gameObject = new GameObject($"WorkshopBlueprint_{definition?.WorkshopId}");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<WorkshopBlueprintCard3D>();
            card.definition = definition;
            card.InitView(Vector3.zero);
            return card;
        }

        public void ConfigureState(bool buildable, string unavailableReason)
        {
            canBuild = buildable;
            reason = unavailableReason ?? string.Empty;
            ApplyVisuals();
        }

        protected override void BuildTextFields()
        {
            if (nameText != null) return;
            float textY = CD * 0.5f + 0.003f;
            nameText = MakeText("Name", new Vector3(0f, textY, CH * 0.38f), 0.095f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.20f));
            descriptionText = MakeText("Description", new Vector3(0f, textY, CH * 0.02f), 0.066f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.42f));
            hintText = MakeText("Hint", new Vector3(0f, textY, -CH * 0.40f), 0.062f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.14f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null || definition == null) return;
            _bodyRenderer.material.color = canBuild ? (IsHovered ? AvailableColor * 1.25f : AvailableColor) : LockedColor;
            if (nameText == null) return;
            nameText.text = $"蓝图 · {definition.DisplayName}";
            descriptionText.text = definition.Description ?? string.Empty;
            hintText.text = canBuild ? "点击建造" : reason;
        }

        protected override void OnMouseDown() => OnConstructionRequested?.Invoke(this);
    }
}
