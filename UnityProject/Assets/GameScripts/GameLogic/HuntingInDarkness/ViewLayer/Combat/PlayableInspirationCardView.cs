using Cards3D;
using HuntingInDarkness.Combat;
using HuntingInDarkness.GameCore.Cards;
using TMPro;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Combat
{
    public sealed class PlayableInspirationCardView : CardView3D
    {
        private CombatInspirationToken token;
        private TextMeshPro nameText;
        private TextMeshPro symbolText;

        public int TokenId => token.Id;
        public override string DisplayName => CombatInspirationPresentation.GetName(token.Color);

        public static PlayableInspirationCardView Create(CombatInspirationToken token, Transform parent)
        {
            var gameObject = new GameObject($"Inspiration_{token.Id}_{token.Color}");
            gameObject.transform.SetParent(parent, false);
            var view = gameObject.AddComponent<PlayableInspirationCardView>();
            view.token = token;
            view.InitView(Vector3.zero);
            view.ForceCategory(CardCategory.Any);
            return view;
        }

        protected override void BuildTextFields()
        {
            float textHeight = CD * 0.5f + 0.003f;
            symbolText = MakeText("Symbol", new Vector3(0f, textHeight, 0.1f), 0.28f, TextAlignmentOptions.Center, new Vector2(CW - 0.1f, 0.4f));
            nameText = MakeText("Name", new Vector3(0f, textHeight, -0.27f), 0.09f, TextAlignmentOptions.Center, new Vector2(CW - 0.1f, 0.22f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null || nameText == null || symbolText == null) return;

            Color color = GetColor(token.Color);
            Color displayColor = IsHovered ? Color.Lerp(color, Color.white, 0.25f) : color;
            _bodyRenderer.material.color = displayColor;
            if (_bodyRenderer.material.HasProperty("_EmissionColor"))
            {
                _bodyRenderer.material.EnableKeyword("_EMISSION");
                _bodyRenderer.material.SetColor("_EmissionColor", displayColor * 0.35f);
            }
            symbolText.text = GetSymbol(token.Color);
            symbolText.color = Color.white;
            nameText.text = CombatInspirationPresentation.GetName(token.Color);
            nameText.color = Color.white;
        }

        private static Color GetColor(CombatInspirationColor color)
        {
            return color switch
            {
                CombatInspirationColor.Red => new Color(0.68f, 0.12f, 0.12f),
                CombatInspirationColor.Blue => new Color(0.10f, 0.30f, 0.72f),
                CombatInspirationColor.Yellow => new Color(0.82f, 0.58f, 0.08f),
                _ => Color.gray
            };
        }

        private static string GetSymbol(CombatInspirationColor color)
        {
            return color switch
            {
                CombatInspirationColor.Red => "◆",
                CombatInspirationColor.Blue => "●",
                CombatInspirationColor.Yellow => "▲",
                _ => "?"
            };
        }
    }
}
