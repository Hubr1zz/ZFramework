using SO.Boss.ActionCard;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    /// <summary>
    /// Boss行动卡的3D视图。显示行动名称、时点费用和描述文本。
    /// </summary>
    public class BossActionCard : CardView3D
    {
        private static readonly Color ColCard  = new(0.20f, 0.10f, 0.10f);
        private static readonly Color ColHover = new(0.30f, 0.15f, 0.15f);

        private TextMeshPro _nameText;
        private TextMeshPro _costText;
        private TextMeshPro _descText;

        private BossActionCardData _data;

        public BossActionCardData Data => _data;

        // ─── 工厂方法 ───

        public static BossActionCard Create(
            BossActionCardData data, Transform parent, Vector3 localPos)
        {
            var go = new GameObject($"BossCard_{data.actionName}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var view = go.AddComponent<BossActionCard>();
            view._data = data;
            view.InitView(localPos);
            return view;
        }

        // ─── 几何：文字区域 ───

        protected override void BuildTextFields()
        {
            float ty = CD * 0.5f + 0.003f;

            _nameText = MakeText("Name",
                new Vector3(0f, ty, CH * 0.36f), 0.115f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.1f, 0.17f));

            _costText = MakeText("Cost",
                new Vector3(CW * 0.3f, ty, CH * 0.42f), 0.09f,
                TextAlignmentOptions.Right,
                new Vector2(0.22f, 0.13f));

            _descText = MakeText("Desc",
                new Vector3(0f, ty, CH * 0.02f), 0.08f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.1f, 0.58f));
        }

        // ─── 视觉刷新 ───

        protected override void ApplyVisuals()
        {
            if (_data == null) return;

            _bodyRenderer.material.color = IsHovered ? ColHover : ColCard;
            _nameText.color = new Color(0.90f, 0.75f, 0.60f);
            _descText.color = new Color(0.80f, 0.65f, 0.50f);

            _nameText.text = _data.actionName;
            _costText.text = _data.timePointCost > 0 ? $"时:{_data.timePointCost}" : "";
            _descText.text = _data.description;
        }

        public void Refresh(BossActionCardData data)
        {
            _data = data;
            ApplyVisuals();
        }
    }
}
