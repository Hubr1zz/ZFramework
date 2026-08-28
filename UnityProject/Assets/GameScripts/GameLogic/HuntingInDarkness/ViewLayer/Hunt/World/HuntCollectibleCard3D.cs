using Cards3D;
using HuntingInDarkness.ViewLayer.Hunt;
using TMPro;
using UnityEngine;

namespace UI.Hunt
{
    /// <summary>选中猎人在本次狩猎中携带的实体资源卡；只投影权威状态，不提交玩法命令。</summary>
    public sealed class HuntCollectibleCard3D : CardView3D
    {
        private static readonly Color BaseColor = new(0.43f, 0.39f, 0.30f);
        private HuntCollectibleStackPresentation stack;
        private TextMeshPro nameText;
        private TextMeshPro countText;
        private TextMeshPro labelText;

        public System.Action Clicked;

        public string ContentId => stack.ContentId;
        public int Count => stack.Count;
        public bool IsInteractable => stack.CanUseInHunt && Clicked != null;
        public override string DisplayName => $"{stack.DisplayName} ×{stack.Count}";

        public static HuntCollectibleCard3D Create(HuntCollectibleStackPresentation stack, Transform parent, Vector3 localPosition, System.Action clicked = null)
        {
            var gameObject = new GameObject($"HuntCollectible_{stack.ContentId}");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<HuntCollectibleCard3D>();
            card.stack = stack;
            card.Clicked = clicked;
            card.InitView(localPosition);
            return card;
        }

        protected override CardCategory GetDefaultCategory() => CardCategory.Resource;

        protected override bool CanHover() => IsInteractable;

        protected override void BuildTextFields()
        {
            float textHeight = Depth * 0.5f + 0.003f;
            nameText = MakeText("Name", new Vector3(0f, textHeight, Height * 0.31f), 0.095f, TextAlignmentOptions.Center, new Vector2(Width - 0.08f, 0.20f));
            countText = MakeText("Count", new Vector3(0f, textHeight, 0f), 0.16f, TextAlignmentOptions.Center, new Vector2(Width - 0.08f, 0.26f));
            labelText = MakeText("Label", new Vector3(0f, textHeight, -Height * 0.33f), 0.065f, TextAlignmentOptions.Center, new Vector2(Width - 0.08f, 0.14f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null) return;
            _bodyRenderer.material.color = IsHovered ? BaseColor * 1.18f : BaseColor;
            if (nameText == null) return;
            nameText.text = stack.DisplayName;
            countText.text = $"×{stack.Count}";
            labelText.text = IsInteractable ? "点击使用" : "狩猎携带物";
            nameText.color = new Color(0.95f, 0.91f, 0.80f);
            countText.color = Color.white;
            labelText.color = new Color(0.74f, 0.72f, 0.65f);
        }

        protected override void OnMouseDown()
        {
            if (IsInteractable) Clicked.Invoke();
        }
    }
}
