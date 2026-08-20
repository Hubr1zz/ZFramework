using TMPro;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    /// <summary>事件选项、猎人选择和继续操作共用的实体卡。</summary>
    public sealed class TabletopEventChoiceCard3D : Cards3D.CardView3D
    {
        private static readonly Color EnabledColor = new(0.28f, 0.22f, 0.16f);
        private static readonly Color HoverColor = new(0.42f, 0.34f, 0.23f);
        private static readonly Color DisabledColor = new(0.16f, 0.16f, 0.17f);

        private TextMeshPro titleText;
        private TextMeshPro bodyText;
        private TextMeshPro statusText;
        private string title = string.Empty;
        private string body = string.Empty;
        private string status = string.Empty;
        private bool isInteractable;

        public System.Action Clicked;
        public bool IsInteractable => isInteractable;
        public override float Width => 1.42f;
        public override float Height => 1.72f;
        public override string DisplayName => title;

        public static TabletopEventChoiceCard3D Create(Transform parent, Vector3 localPosition)
        {
            var gameObject = new GameObject("TabletopEventChoiceCard");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<TabletopEventChoiceCard3D>();
            card.InitView(localPosition);
            card.transform.localPosition = localPosition;
            return card;
        }

        public void Present(string titleTextValue, string bodyTextValue, bool interactable, string statusTextValue, System.Action action)
        {
            title = titleTextValue ?? string.Empty;
            body = bodyTextValue ?? string.Empty;
            isInteractable = interactable;
            status = statusTextValue ?? string.Empty;
            Clicked = action;
            ApplyVisuals();
        }

        protected override bool CanHover() => isInteractable;

        protected override void BuildTextFields()
        {
            float textHeight = Depth * 0.5f + 0.003f;
            titleText = MakeText("Title", new Vector3(0f, textHeight, Height * 0.34f), 0.115f, TextAlignmentOptions.Center, new Vector2(Width - 0.12f, 0.34f));
            bodyText = MakeText("Body", new Vector3(0f, textHeight, 0f), 0.082f, TextAlignmentOptions.Center, new Vector2(Width - 0.14f, 0.76f));
            statusText = MakeText("Status", new Vector3(0f, textHeight, -Height * 0.38f), 0.068f, TextAlignmentOptions.Center, new Vector2(Width - 0.12f, 0.24f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null) return;
            _bodyRenderer.material.color = !isInteractable ? DisabledColor : IsHovered ? HoverColor : EnabledColor;
            if (titleText == null) return;
            titleText.text = title;
            titleText.color = isInteractable ? new Color(0.95f, 0.82f, 0.56f) : new Color(0.52f, 0.52f, 0.52f);
            bodyText.text = body;
            bodyText.color = isInteractable ? new Color(0.86f, 0.84f, 0.78f) : new Color(0.48f, 0.48f, 0.48f);
            statusText.text = status;
            statusText.color = isInteractable ? new Color(0.66f, 0.74f, 0.78f) : new Color(0.62f, 0.40f, 0.38f);
        }

        protected override void OnMouseDown()
        {
            if (!isInteractable) return;
            Clicked?.Invoke();
        }
    }
}
