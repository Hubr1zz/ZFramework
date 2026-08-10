using GameplayBase.CombatSystem;
using SO.Boss.HitLocation;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    /// <summary>
    /// Boss部位卡的3D视图。
    /// 背面：深红色。正面：显示部位名/韧性/血量。摧毁：橙褐色标注"★摧毁"。
    /// 仅在正面朝上或已摧毁时允许悬停抬起。
    /// </summary>
    public class BossHitLocationCard : CardView3D
    {
        private static readonly Color ColFaceDown  = new(0.22f, 0.06f, 0.06f);
        private static readonly Color ColFaceUp    = new(0.96f, 0.88f, 0.72f);
        private static readonly Color ColDestroyed = new(0.55f, 0.20f, 0.08f);

        protected override float HoverLift => 0.08f;

        private TextMeshPro _nameText;
        private TextMeshPro _detailText;
        private TextMeshPro _hpText;

        private HitLocationRuntimeState _state;

        public HitLocationCardData CardData => _state?.Data;

        // ─── 工厂方法 ───

        public static BossHitLocationCard Create(
            HitLocationRuntimeState state, Transform parent, Vector3 localPos)
        {
            var go = new GameObject($"HitLocation_{state.Data.locationName}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var view = go.AddComponent<BossHitLocationCard>();
            view._state = state;
            view.InitView(localPos);
            return view;
        }

        // ─── 几何：文字区域 ───

        protected override void BuildTextFields()
        {
            float ty = CD * 0.5f + 0.003f;

            _nameText = MakeText("Name",
                new Vector3(0f, ty, CH * 0.36f), 0.10f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.1f, 0.18f));

            _detailText = MakeText("Detail",
                new Vector3(0f, ty, CH * 0.02f), 0.075f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.1f, 0.52f));

            _hpText = MakeText("HP",
                new Vector3(0f, ty, -CH * 0.36f), 0.08f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.1f, 0.14f));
        }

        // ─── 悬停条件 ───

        protected override bool CanHover()
            => _state != null && (_state.IsFaceUp || _state.IsDestroyed);

        // ─── 视觉刷新 ───

        protected override void ApplyVisuals()
        {
            if (_state == null) return;

            if (_state.IsDestroyed)
            {
                _bodyRenderer.material.color = ColDestroyed;
                _nameText.color   = new Color(1f, 0.75f, 0.5f);
                _detailText.color = new Color(0.9f, 0.6f, 0.4f);
                _hpText.color     = new Color(0.9f, 0.6f, 0.4f);
                _nameText.text   = _state.Data.locationName;
                _detailText.text = "★ 摧毁";
                _hpText.text     = "";
            }
            else if (_state.IsFaceUp)
            {
                _bodyRenderer.material.color = ColFaceUp;
                _nameText.color   = new Color(0.08f, 0.08f, 0.08f);
                _detailText.color = new Color(0.15f, 0.15f, 0.15f);
                _hpText.color     = new Color(0.08f, 0.08f, 0.08f);
                _nameText.text   = _state.Data.locationName;
                _detailText.text = $"韧性: {_state.Data.toughness}\n{_state.Data.description}";
                _hpText.text     = $"HP {_state.CurrentHp}/{_state.Data.maxHp}";
            }
            else
            {
                _bodyRenderer.material.color = ColFaceDown;
                _nameText.color   = new Color(0.48f, 0.22f, 0.22f);
                _detailText.color = Color.clear;
                _hpText.color     = Color.clear;
                _nameText.text   = "—";
                _detailText.text = "";
                _hpText.text     = "";
            }
        }

        public void Refresh() => ApplyVisuals();
    }
}
