using TMPro;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    /// <summary>事件叙事、判定和结果共用的主卡。</summary>
    public sealed class TabletopEventPrimaryCard3D : Cards3D.CardView3D
    {
        private static readonly Color NarrativeColor = new(0.20f, 0.13f, 0.10f);
        private static readonly Color CheckColor = new(0.16f, 0.20f, 0.25f);
        private static readonly Color SuccessColor = new(0.16f, 0.30f, 0.18f);
        private static readonly Color FailureColor = new(0.34f, 0.15f, 0.14f);

        private TextMeshPro titleText;
        private TextMeshPro bodyText;
        private TextMeshPro footerText;
        private string title = string.Empty;
        private string body = string.Empty;
        private string footer = string.Empty;
        private TabletopEventPrimaryTone tone;

        public override float Width => 2.35f;
        public override float Height => 2.85f;
        public override string DisplayName => title;

        public static TabletopEventPrimaryCard3D Create(Transform parent)
        {
            var gameObject = new GameObject("TabletopEventPrimaryCard");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<TabletopEventPrimaryCard3D>();
            card.InitView(Vector3.zero);
            return card;
        }

        public void Present(string titleTextValue, string bodyTextValue, string footerTextValue, TabletopEventPrimaryTone cardTone)
        {
            title = titleTextValue ?? string.Empty;
            body = bodyTextValue ?? string.Empty;
            footer = footerTextValue ?? string.Empty;
            tone = cardTone;
            ApplyVisuals();
        }

        protected override bool CanHover() => false;

        protected override void BuildTextFields()
        {
            float textHeight = Depth * 0.5f + 0.003f;
            titleText = MakeText("Title", new Vector3(0f, textHeight, Height * 0.39f), 0.17f, TextAlignmentOptions.Center, new Vector2(Width - 0.16f, 0.42f));
            bodyText = MakeText("Body", new Vector3(0f, textHeight, 0f), 0.105f, TextAlignmentOptions.Center, new Vector2(Width - 0.20f, 1.58f));
            footerText = MakeText("Footer", new Vector3(0f, textHeight, -Height * 0.40f), 0.085f, TextAlignmentOptions.Center, new Vector2(Width - 0.18f, 0.36f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null) return;
            _bodyRenderer.material.color = tone switch
            {
                TabletopEventPrimaryTone.Check => CheckColor,
                TabletopEventPrimaryTone.Success => SuccessColor,
                TabletopEventPrimaryTone.Failure => FailureColor,
                _ => NarrativeColor
            };
            if (titleText == null) return;
            titleText.text = title;
            titleText.color = new Color(0.98f, 0.79f, 0.42f);
            bodyText.text = body;
            bodyText.color = new Color(0.88f, 0.86f, 0.80f);
            footerText.text = footer;
            footerText.color = new Color(0.68f, 0.72f, 0.76f);
        }
    }

    public enum TabletopEventPrimaryTone
    {
        Narrative,
        Check,
        Success,
        Failure
    }
}
