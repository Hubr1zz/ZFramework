using TMPro;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    /// <summary>营地桌面固定的狩猎整备入口卡。</summary>
    public sealed class TabletopDepartureLauncherCard3D : Cards3D.CardView3D
    {
        [SerializeField] private TextMeshPro titleText;
        [SerializeField] private TextMeshPro bodyText;

        public System.Action Clicked;
        public override float Width => Cards3D.CardView3D.CW;
        public override float Height => Cards3D.CardView3D.CH;
        public override string DisplayName => "出猎";

        public static TabletopDepartureLauncherCard3D Create(Transform parent, Vector3 localPosition)
        {
            var gameObject = new GameObject("TabletopDepartureLauncherCard");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<TabletopDepartureLauncherCard3D>();
            card.InitView(localPosition);
            card.transform.localPosition = localPosition;
            return card;
        }

        protected override void BuildTextFields()
        {
            if (titleText != null) return;
            float textHeight = Depth * 0.5f + 0.003f;
            titleText = MakeText("Title", new Vector3(0f, textHeight, Height * 0.24f), 0.14f, TextAlignmentOptions.Center, new Vector2(Width - 0.12f, 0.38f));
            bodyText = MakeText("Body", new Vector3(0f, textHeight, -Height * 0.13f), 0.085f, TextAlignmentOptions.Center, new Vector2(Width - 0.14f, 0.68f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null)
                return;
            _bodyRenderer.material.color = IsHovered ? new Color(0.38f, 0.32f, 0.20f) : new Color(0.25f, 0.20f, 0.13f);
            if (titleText == null)
                return;
            titleText.text = "出猎";
            titleText.color = new Color(0.96f, 0.78f, 0.40f);
            bodyText.text = "拖动猎人\n组队";
            bodyText.color = new Color(0.90f, 0.88f, 0.82f);
        }

        protected override void OnClickReleased()
        {
            Clicked?.Invoke();
            base.OnClickReleased();
        }
    }
}
