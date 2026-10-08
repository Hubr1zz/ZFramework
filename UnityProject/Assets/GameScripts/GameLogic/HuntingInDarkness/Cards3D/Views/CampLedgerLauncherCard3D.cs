using HuntingInDarkness.Data;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    public sealed class CampLedgerLauncherCard3D : CardView3D
    {
        private SettlementInstance settlement;
        [SerializeField] private TextMeshPro titleText;
        [SerializeField] private TextMeshPro yearText;
        [SerializeField] private TextMeshPro countText;

        public System.Action Clicked;
        public override string DisplayName => "营地年鉴";
        protected override CardCategory GetDefaultCategory() => CardCategory.Invention;

        public static CampLedgerLauncherCard3D Create(Transform parent, Vector3 localPosition)
        {
            var gameObject = new GameObject("CampLedgerLauncherCard3D");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<CampLedgerLauncherCard3D>();
            card.InitView(localPosition);
            return card;
        }

        public void Configure(SettlementInstance settlementData)
        {
            settlement = settlementData;
            ApplyVisuals();
        }

        protected override void BuildTextFields()
        {
            if (titleText != null) return;
            float textY = CD * 0.5f + 0.003f;
            titleText = MakeText("Title", new Vector3(0f, textY, CH * 0.32f), 0.105f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.30f));
            yearText = MakeText("Year", new Vector3(0f, textY, 0f), 0.105f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.30f));
            countText = MakeText("Count", new Vector3(0f, textY, -CH * 0.34f), 0.06f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.36f));
            countText.enableWordWrapping = true;
            countText.overflowMode = TextOverflowModes.Overflow;
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null) return;
            _bodyRenderer.material.color = IsHovered ? new Color(0.30f, 0.18f, 0.08f) : new Color(0.18f, 0.10f, 0.05f);
            if (titleText == null) return;
            titleText.text = "营地年鉴";
            yearText.text = settlement != null ? $"第 {settlement.CurrentYear} 年" : "尚未启封";
            countText.text = settlement != null ? $"远征 {GetLastHuntYear(settlement)}→{settlement.CurrentYear}\n记录 {(settlement.Timeline?.Count ?? 0) + (settlement.HuntHistory?.Count ?? 0)}" : "点击查看";
            titleText.color = new Color(0.98f, 0.82f, 0.45f);
            yearText.color = new Color(0.90f, 0.88f, 0.82f);
            countText.color = new Color(0.90f, 0.88f, 0.82f);
        }

        private static int GetLastHuntYear(SettlementInstance data)
        {
            if (data.HuntHistory == null || data.HuntHistory.Count == 0) return 0;
            return data.HuntHistory[data.HuntHistory.Count - 1]?.Year ?? 0;
        }

        protected override void OnClickReleased() => Clicked?.Invoke();
    }
}
