using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    /// <summary>营地桌面的招募入口卡，持续展示当前年度的接纳条件。</summary>
    public sealed class RecruitmentLauncherCard3D : CardView3D
    {
        private TextMeshPro titleText;
        private TextMeshPro costText;
        private TextMeshPro stateText;
        private SettlementInstance settlement;
        private PlayableSettlementContentCatalog catalog;
        private bool canRecruit;
        private string unavailableReason = string.Empty;

        public System.Action Clicked;
        public override string DisplayName => "营火招募";
        protected override CardCategory GetDefaultCategory() => CardCategory.HunterProfile;

        public static RecruitmentLauncherCard3D Create(Transform parent, Vector3 localPosition)
        {
            var gameObject = new GameObject("RecruitmentLauncherCard3D");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<RecruitmentLauncherCard3D>();
            card.InitView(localPosition);
            return card;
        }

        public void Configure(SettlementInstance settlementData, PlayableSettlementContentCatalog content)
        {
            settlement = settlementData;
            catalog = content;
            RefreshState();
        }

        public void RefreshState()
        {
            canRecruit = EvaluateAvailability(out unavailableReason);
            ApplyVisuals();
        }

        protected override void BuildTextFields()
        {
            if (titleText != null) return;
            float textY = CD * 0.5f + 0.003f;
            titleText = MakeText("Title", new Vector3(0f, textY, CH * 0.32f), 0.11f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.24f));
            costText = MakeText("Cost", new Vector3(0f, textY, 0f), 0.07f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.26f));
            stateText = MakeText("State", new Vector3(0f, textY, -CH * 0.34f), 0.06f, TextAlignmentOptions.Center, new Vector2(CW - 0.06f, 0.24f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null) return;
            _bodyRenderer.material.color = canRecruit ? IsHovered ? new Color(0.54f, 0.31f, 0.12f) : new Color(0.40f, 0.22f, 0.10f) : new Color(0.17f, 0.16f, 0.15f);
            if (titleText == null) return;
            titleText.text = "营火招募";
            costText.text = FormatCost();
            stateText.text = canRecruit ? "点击呼唤幸存者" : unavailableReason;
        }

        protected override bool CanHover() => canRecruit;

        protected override void OnMouseDown()
        {
            if (canRecruit) Clicked?.Invoke();
        }

        private bool EvaluateAvailability(out string reason)
        {
            if (settlement == null || catalog == null || !HasTemplate())
            {
                reason = "暂无候选猎人";
                return false;
            }
            int aliveCount = settlement.GetAliveHunters().Count;
            int cost = RecruitmentRules.GetCost(aliveCount, catalog.RecruitmentCost);
            if (cost > 0 && catalog.RecruitmentCostItem == null)
            {
                reason = "接纳物资尚未配置";
                return false;
            }
            int availableResource = settlement.GetResource(catalog.RecruitmentCostItem);
            return RecruitmentRules.CanRecruit(settlement.CurrentYear, settlement.LastRecruitmentYear, aliveCount, catalog.MaximumLivingHunters, availableResource, catalog.RecruitmentCost, out reason);
        }

        private string FormatCost()
        {
            int aliveCount = settlement?.GetAliveHunters().Count ?? 0;
            int cost = RecruitmentRules.GetCost(aliveCount, catalog?.RecruitmentCost ?? 0);
            if (cost == 0) return "无人守火 · 免费援助";
            return catalog?.RecruitmentCostItem != null ? $"{catalog.RecruitmentCostItem.itemName} ×{cost}" : "接纳物资未配置";
        }

        private bool HasTemplate()
        {
            foreach (HunterData template in catalog.RecruitmentTemplates)
                if (template != null) return true;
            return false;
        }
    }
}
