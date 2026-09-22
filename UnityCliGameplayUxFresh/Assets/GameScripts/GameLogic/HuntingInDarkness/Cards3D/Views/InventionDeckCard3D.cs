using TMPro;
using UnityEngine;

namespace Cards3D
{
    /// <summary>营地发明牌堆入口。只显示当前资格牌数量，点击后由 InventionZone 抽取候选。</summary>
    public sealed class InventionDeckCard3D : CardView3D
    {
        private TextMeshPro titleText;
        private TextMeshPro bodyText;
        private int eligibleCount;

        public System.Action DrawRequested;
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

        public void Present(int count, bool choicesOpen)
        {
            eligibleCount = Mathf.Max(0, count);
            if (titleText == null) return;
            titleText.text = "发明牌堆";
            bodyText.text = choicesOpen ? "先从已抽出的候选中选择一项" : eligibleCount > 0 ? $"当前可发明 {eligibleCount} 项\n点击抽取两张" : "当前没有满足条件的新发明";
            ApplyVisuals();
        }

        protected override void BuildTextFields()
        {
            float y = Depth * 0.5f + 0.003f;
            titleText = MakeText("Title", new Vector3(0f, y, Height * 0.25f), 0.11f, TextAlignmentOptions.Center, new Vector2(Width - 0.1f, 0.28f));
            bodyText = MakeText("Body", new Vector3(0f, y, -Height * 0.08f), 0.075f, TextAlignmentOptions.Center, new Vector2(Width - 0.12f, 0.65f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer != null) _bodyRenderer.material.color = eligibleCount > 0 ? IsHovered ? new Color(0.38f, 0.31f, 0.52f) : new Color(0.25f, 0.20f, 0.36f) : new Color(0.14f, 0.14f, 0.17f);
            if (titleText != null) titleText.color = new Color(0.88f, 0.76f, 1f);
            if (bodyText != null) bodyText.color = new Color(0.76f, 0.72f, 0.82f);
        }

        protected override void OnClickReleased()
        {
            if (eligibleCount > 0) DrawRequested?.Invoke();
        }
    }
}
