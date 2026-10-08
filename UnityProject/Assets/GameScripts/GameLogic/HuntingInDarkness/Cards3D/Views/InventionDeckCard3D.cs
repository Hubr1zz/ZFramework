using TMPro;
using UnityEngine;

namespace Cards3D
{
    /// <summary>营地发明牌堆入口。点击先打开候选扩展区，抽取由独立入口调用 DrawRequested。</summary>
    public sealed class InventionDeckCard3D : CardView3D
    {
        [SerializeField] private TextMeshPro titleText;
        [SerializeField] private TextMeshPro bodyText;
        private int eligibleCount;
        private bool drawPending;

        public System.Action DrawRequested;
        public System.Action PreviewRequested;
        public int EligibleCount => eligibleCount;
        public override string DisplayName => "发明牌堆";

        protected override CardCategory GetDefaultCategory() => CardCategory.Invention;

        public static InventionDeckCard3D Create(Transform parent)
        {
            GameObject gameObject = new("InventionDeckCard3D");
            gameObject.transform.SetParent(parent, false);
            InventionDeckCard3D card = gameObject.AddComponent<InventionDeckCard3D>();
            card.InitView(Vector3.zero);
            return card;
        }

        public void Present(int count, bool choicesOpen, bool drawPending = false)
        {
            eligibleCount = Mathf.Max(0, count);
            this.drawPending = drawPending;
            if (titleText == null) return;
            titleText.text = "发明牌堆";
            bodyText.text = drawPending ? "继续选择候选" : choicesOpen ? "已有候选" : eligibleCount > 0 ? $"可抽取\n{eligibleCount} 项" : "暂无可发明";
            ApplyVisuals();
        }

        protected override void BuildTextFields()
        {
            if (titleText != null) return;
            float y = Depth * 0.5f + 0.003f;
            titleText = MakeText("Title", new Vector3(0f, y, 0.33f), CardPresentationConsts.DynamicTitleFontSize, TextAlignmentOptions.Center, new Vector2(Width - 0.1f, 0.30f));
            bodyText = MakeText("Body", new Vector3(0f, y, -0.12f), CardPresentationConsts.DynamicBodyFontSize, TextAlignmentOptions.Center, new Vector2(Width - 0.12f, 0.50f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer != null) _bodyRenderer.material.color = eligibleCount > 0 ? IsHovered ? new Color(0.08f, 0.05f, 0.13f) : new Color(0.04f, 0.025f, 0.075f) : new Color(0.10f, 0.10f, 0.12f);
            if (titleText != null) titleText.color = new Color(0.88f, 0.76f, 1f);
            if (bodyText != null) bodyText.color = new Color(0.90f, 0.88f, 0.96f);
        }

        protected override void OnClickReleased()
        {
            if (PreviewRequested != null)
            {
                PreviewRequested.Invoke();
                return;
            }
            if (drawPending || eligibleCount > 0) DrawRequested?.Invoke();
        }
    }
}
