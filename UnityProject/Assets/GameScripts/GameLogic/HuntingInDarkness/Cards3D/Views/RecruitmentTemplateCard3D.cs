using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    /// <summary>营火招募候选卡。只负责呈现模板与选择意图，不直接修改营地数据。</summary>
    public sealed class RecruitmentTemplateCard3D : CardView3D
    {
        private static readonly Color AvailableColor = new(0.24f, 0.31f, 0.36f);
        private static readonly Color SelectedColor = new(0.48f, 0.34f, 0.16f);
        private static readonly Color DisabledColor = new(0.16f, 0.16f, 0.17f);
        private HunterData template;
        private TextMeshPro nameText;
        private TextMeshPro statsText;
        private TextMeshPro traitsText;
        private TextMeshPro hintText;
        private bool isSelected;
        private bool isInteractable;

        public HunterData Template => template;
        public System.Action<RecruitmentTemplateCard3D> Selected;
        public override string DisplayName => template != null ? template.hunterName : base.DisplayName;
        protected override CardCategory GetDefaultCategory() => CardCategory.HunterProfile;

        public static RecruitmentTemplateCard3D Create(HunterData hunterTemplate, Transform parent, Vector3 localPosition)
        {
            var gameObject = new GameObject($"Recruitment_{hunterTemplate.hunterName}");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<RecruitmentTemplateCard3D>();
            card.template = hunterTemplate;
            card.InitView(localPosition);
            return card;
        }

        public void ConfigureState(bool selected, bool interactable)
        {
            isSelected = selected;
            isInteractable = interactable;
            ApplyVisuals();
        }

        protected override void BuildTextFields()
        {
            if (nameText != null) return;
            float textY = CD * 0.5f + 0.003f;
            nameText = MakeText("Name", new Vector3(0f, textY, CH * 0.35f), 0.095f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.20f));
            statsText = MakeText("Stats", new Vector3(0f, textY, CH * 0.08f), 0.065f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.28f));
            traitsText = MakeText("Traits", new Vector3(0f, textY, -CH * 0.16f), 0.055f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.24f));
            hintText = MakeText("Hint", new Vector3(0f, textY, -CH * 0.39f), 0.06f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.16f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null || template == null) return;
            _bodyRenderer.material.color = !isInteractable ? DisabledColor : isSelected ? SelectedColor : IsHovered ? AvailableColor * 1.2f : AvailableColor;
            if (nameText == null) return;
            HunterCombatStats stats = template.initialStats;
            nameText.text = template.hunterName;
            statsText.text = $"力 {stats.strength}  技 {stats.accuracy}\n敏 {stats.evasion}  移 {stats.movement}";
            traitsText.text = template.startingTraits.Count > 0 ? PlayableTraitRegistry.GetDisplayNames(template.startingTraits) : "尚无特性";
            hintText.text = isSelected ? "◆ 已选择" : isInteractable ? "点击选择" : "暂不可接纳";
        }

        protected override bool CanHover() => isInteractable;

        protected override void OnMouseDown()
        {
            if (isInteractable) Selected?.Invoke(this);
        }
    }
}
