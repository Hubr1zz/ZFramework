using Core;
using GameplayBase;
using GameplayBase.CombatSystem;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    /// <summary>
    /// 角色行动卡的3D视图。显示卡名、时点费用、效果描述和弃置/恢复提示。
    /// </summary>
    public class CharacterActionCard : CardView3D
    {
        private static readonly Color ColFaceUp   = new(0.96f, 0.93f, 0.84f);
        private static readonly Color ColFaceDown = new(0.12f, 0.16f, 0.32f);
        private static readonly Color ColHover    = new(1.00f, 0.96f, 0.62f);

        private TextMeshPro _nameText;
        private TextMeshPro _costText;
        private TextMeshPro _descText;
        private TextMeshPro _hintText;

        private ICharacterActionCardInstanceState _state;

        public int CardInstanceId => _state?.InstanceId ?? -1;

        // ─── 工厂方法 ───

        public static CharacterActionCard Create(
            ICharacterActionCardInstanceState state, Transform parent, Vector3 localPos)
        {
            var go = new GameObject($"Card_{state.InstanceId}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var view = go.AddComponent<CharacterActionCard>();
            view._state = state;
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

            _hintText = MakeText("Hint",
                new Vector3(0f, ty, -CH * 0.38f), 0.07f,
                TextAlignmentOptions.Center,
                new Vector2(CW - 0.1f, 0.12f));
        }

        // ─── 视觉刷新 ───

        protected override void ApplyVisuals()
        {
            if (_state == null) return;

            bool faceUp = _state.CurrentFace == CardFace.FaceUp;

            _bodyRenderer.material.color =
                faceUp ? (IsHovered ? ColHover : ColFaceUp) : ColFaceDown;

            if (faceUp)
            {
                _nameText.color = new Color(0.08f, 0.08f, 0.08f);
                _descText.color = new Color(0.15f, 0.15f, 0.15f);

                _nameText.text = _state.CardName;
                _costText.text = _state.CostDescription;
                _descText.text = _state.FaceUpDescription;
                _hintText.text = _state.CanDiscard ? "右键·弃置" : "";
            }
            else
            {
                _nameText.color = new Color(0.48f, 0.52f, 0.70f);
                _descText.color = new Color(0.38f, 0.42f, 0.60f);

                _nameText.text = "—";
                _costText.text = "";
                _descText.text = _state.FaceDownDescription;
                _hintText.text = _state.CanRestore ? "可恢复" : "";
            }
        }

        public void Refresh(ICharacterActionCardInstanceState state)
        {
            _state = state;
            ApplyVisuals();
        }

        // ─── 悬浮范围预览 ───

        protected override void OnMouseEnter()
        {
            base.OnMouseEnter();
            if (CardInstanceId >= 0)
                EventBus.Publish(new CardHoverPreviewEvent { CardInstanceId = CardInstanceId });
        }

        protected override void OnMouseExit()
        {
            base.OnMouseExit();
            EventBus.Publish(new CardHoverPreviewEndEvent());
        }
    }
}
