using TMPro;
using UI;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Cards3D
{
    /// <summary>营地实体扩展入口卡；只负责切换已配置的区域控制器。</summary>
    public sealed class SettlementExpansionLauncherCard3D : CardView3D
    {
        [SerializeField] private TextMeshPro titleText;
        [SerializeField] private TextMeshPro bodyText;
        [SerializeField] private SettlementExpansionAreaController expansion;
        [SerializeField] private InventionZone previewZone;
        [SerializeField] private string title = "展开区域";
        [SerializeField] private string body = "点击展开 / 收起";

        public SettlementExpansionAreaController Expansion => expansion;
        public InventionZone PreviewZone => previewZone;

        protected override CardCategory GetDefaultCategory() => CardCategory.Any;

        public static SettlementExpansionLauncherCard3D Create(Transform parent, Vector3 localPosition, SettlementExpansionAreaController area, string title, string body)
        {
            var gameObject = new GameObject("SettlementExpansionLauncherCard3D");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<SettlementExpansionLauncherCard3D>();
            card.Configure(area, title, body);
            card.InitView(localPosition);
            return card;
        }

        public void Configure(SettlementExpansionAreaController area, string label, string hint)
        {
            expansion = area;
            title = label ?? "展开区域";
            body = hint ?? "点击展开 / 收起";
            ApplyVisuals();
        }

        public void ConfigurePreview(InventionZone zone)
        {
            previewZone = zone;
        }

        protected override void BuildTextFields()
        {
            if (titleText != null) return;
            float y = Depth * 0.5f + 0.003f;
            titleText = MakeText("Title", new Vector3(0f, y, Height * 0.25f), 0.11f, TextAlignmentOptions.Center, new Vector2(Width - 0.1f, 0.28f));
            bodyText = MakeText("Body", new Vector3(0f, y, -Height * 0.12f), 0.075f, TextAlignmentOptions.Center, new Vector2(Width - 0.12f, 0.55f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer != null) _bodyRenderer.material.color = expansion != null && expansion.IsOpen ? new Color(0.30f, 0.26f, 0.12f) : new Color(0.20f, 0.18f, 0.12f);
            if (titleText == null) return;
            titleText.text = title;
            bodyText.text = body;
            bool enabled = expansion != null;
            titleText.color = enabled ? new Color(0.98f, 0.82f, 0.45f) : new Color(0.70f, 0.70f, 0.68f);
            bodyText.color = enabled ? new Color(0.90f, 0.88f, 0.82f) : new Color(0.68f, 0.68f, 0.66f);
        }

        protected override void OnClickReleased()
        {
            if (previewZone != null)
            {
                previewZone.DrawCandidatesAsync().Forget();
                return;
            }
            expansion?.Toggle();
            ApplyVisuals();
        }
    }
}
