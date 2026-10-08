using TMPro;
using UnityEngine;

namespace Cards3D
{
    /// <summary>发明预览扩展区中的明确抽取入口；只触发既有 DrawCandidates 流程。</summary>
    public sealed class InventionDrawCandidatesCard3D : CardView3D
    {
        [SerializeField] private TextMeshPro titleText;
        [SerializeField] private TextMeshPro bodyText;
        private bool available;
        private int count;

        public System.Action DrawRequested;
        protected override CardCategory GetDefaultCategory() => CardCategory.Invention;

        public static InventionDrawCandidatesCard3D Create(Transform parent, Vector3 localPosition, TMP_FontAsset font = null)
        {
            var gameObject = new GameObject("InventionDrawCandidatesCard3D");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<InventionDrawCandidatesCard3D>();
            card.InitView(localPosition);
            return card;
        }

        public void Present(int eligible, bool choicesOpen, bool drawPending = false)
        {
            count = Mathf.Max(0, eligible);
            available = drawPending || !choicesOpen && count > 0;
            if (titleText == null) return;
            bodyText.text = drawPending ? "继续选择候选" : choicesOpen ? "已有候选" : available ? $"从 {count} 项中抽取最多两张" : "暂无可抽取候选";
            ApplyVisuals();
        }

        protected override void BuildTextFields()
        {
            if (titleText != null) return;
            float y = Depth * 0.5f + 0.003f;
            titleText = MakeText("Title", new Vector3(0f, y, Height * 0.24f), 0.11f, TextAlignmentOptions.Center, new Vector2(Width - 0.1f, 0.28f));
            bodyText = MakeText("Body", new Vector3(0f, y, -Height * 0.12f), 0.075f, TextAlignmentOptions.Center, new Vector2(Width - 0.12f, 0.55f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer != null) _bodyRenderer.material.color = available ? IsHovered ? new Color(0.46f, 0.36f, 0.16f) : new Color(0.30f, 0.24f, 0.12f) : new Color(0.16f, 0.16f, 0.17f);
            if (titleText == null) return;
            titleText.text = "抽取候选";
            titleText.color = available ? new Color(0.98f, 0.82f, 0.45f) : new Color(0.70f, 0.70f, 0.68f);
            bodyText.color = available ? new Color(0.90f, 0.88f, 0.82f) : new Color(0.68f, 0.68f, 0.66f);
        }

        protected override void OnClickReleased()
        {
            if (available) DrawRequested?.Invoke();
        }
    }
}
