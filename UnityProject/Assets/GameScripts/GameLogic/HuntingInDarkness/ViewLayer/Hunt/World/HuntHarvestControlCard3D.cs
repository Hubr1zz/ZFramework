using TMPro;
using UnityEngine;

namespace UI.Hunt
{
    /// <summary>3D 采集面板的实体关闭卡，避免用屏幕空间按钮打断桌面交互。</summary>
    public sealed class HuntHarvestControlCard3D : Cards3D.CardView3D
    {
        private static readonly Color EnabledColor = new(0.36f, 0.25f, 0.18f);
        private static readonly Color DisabledColor = new(0.18f, 0.18f, 0.19f);

        private TextMeshPro labelText;
        private bool isInteractable;
        private string label = "离开";

        public System.Action Clicked;
        public override float Width => 1.05f;
        public override float Height => 0.55f;
        public override string DisplayName => label;

        public static HuntHarvestControlCard3D Create(Transform parent, Vector3 localPosition)
        {
            var gameObject = new GameObject("HarvestCloseCard");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<HuntHarvestControlCard3D>();
            card.InitView(localPosition);
            card.transform.localPosition = localPosition;
            return card;
        }

        public void Present(string text, bool interactable)
        {
            label = string.IsNullOrWhiteSpace(text) ? "离开" : text;
            isInteractable = interactable;
            ApplyVisuals();
        }

        protected override bool CanHover() => isInteractable;

        protected override void BuildTextFields()
        {
            labelText = MakeText("Label", new Vector3(0f, Depth * 0.5f + 0.003f, 0f), 0.10f, TextAlignmentOptions.Center, new Vector2(Width - 0.08f, Height - 0.08f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null) return;
            _bodyRenderer.material.color = isInteractable ? IsHovered ? EnabledColor * 1.25f : EnabledColor : DisabledColor;
            if (labelText == null) return;
            labelText.text = label;
            labelText.color = isInteractable ? Color.white : new Color(0.55f, 0.55f, 0.55f);
        }

        protected override void OnMouseDown()
        {
            if (!isInteractable) return;
            Clicked?.Invoke();
        }
    }
}
