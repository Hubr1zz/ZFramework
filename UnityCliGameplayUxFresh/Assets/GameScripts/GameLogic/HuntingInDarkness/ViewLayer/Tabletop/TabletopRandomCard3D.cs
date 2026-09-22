using System;
using Cards3D;
using TMPro;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    /// <summary>一次桌面随机交互中的实体卡牌，只负责选择手势与正反面投影。</summary>
    public sealed class TabletopRandomCard3D : CardView3D
    {
        private string cardId = string.Empty;
        private int cardIndex;
        private int value;
        private bool isOldMaid;
        private bool isOldMaidDeck;
        private bool isDeathDeck;
        private string deathFaceLabel;
        private bool isFaceUp;
        private bool isSelectable;
        private float width;
        private float height;
        private float depth;
        private Material backMaterial;
        private Material frontMaterial;
        private Material ownedMaterial;
        private TMP_FontAsset font;
        private TextMeshPro faceLabel;
        private Collider cardCollider;

        public Action<TabletopRandomCard3D> Selected;
        public string CardId => cardId;
        public int CardIndex => cardIndex;
        public int Value => value;
        public bool IsOldMaid => isOldMaid;
        public bool IsFaceUp => isFaceUp;
        public bool IsSelectable => isSelectable;
        public override float Width => width;
        public override float Height => height;
        public override float Depth => depth;
        public override string DisplayName => isFaceUp ? GetFaceText() : "背面朝上的牌";

        public static TabletopRandomCard3D Create(string id, int index, int cardValue, bool oldMaid, bool oldMaidDeck, bool deathDeck, string deathFaceLabel, Transform parent, Vector3 localPosition, Vector3 size, Material back, Material front, TMP_FontAsset cardFont)
        {
            var gameObject = new GameObject($"Card_{id}");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<TabletopRandomCard3D>();
            card.cardId = id ?? string.Empty;
            card.cardIndex = index;
            card.value = cardValue;
            card.isOldMaid = oldMaid;
            card.isOldMaidDeck = oldMaidDeck;
            card.isDeathDeck = deathDeck;
            card.deathFaceLabel = deathFaceLabel ?? string.Empty;
            card.width = Mathf.Max(0.01f, size.x);
            card.depth = Mathf.Max(0.001f, size.y);
            card.height = Mathf.Max(0.01f, size.z);
            card.font = cardFont;
            card.InitView(localPosition);
            card.cardCollider = card.GetComponent<Collider>();
            card.ownedMaterial = card._bodyRenderer.material;
            card.backMaterial = back;
            card.frontMaterial = front;
            card.SetSelectable(false);
            return card;
        }

        public void SetSelectable(bool selectable)
        {
            isSelectable = selectable && !isFaceUp;
            if (cardCollider != null)
                cardCollider.enabled = isSelectable;
            ApplyVisuals();
        }

        public void Reveal(Vector3 localPosition)
        {
            isFaceUp = true;
            SetSelectable(false);
            MoveTo(localPosition);
            ApplyVisuals();
        }

        protected override void BuildTextFields()
        {
            if (faceLabel != null) return;
            faceLabel = MakeText("FaceLabel", new Vector3(0f, depth * 0.5f + 0.003f, 0f), 0.18f, TextAlignmentOptions.Center, new Vector2(width * 0.72f, height * 0.62f));
            if (font != null)
                faceLabel.font = font;
            faceLabel.fontStyle = FontStyles.Bold;
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null) return;
            Material projectedMaterial = isFaceUp ? frontMaterial : backMaterial;
            if (projectedMaterial != null)
                _bodyRenderer.sharedMaterial = projectedMaterial;
            else if (ownedMaterial != null)
            {
                _bodyRenderer.sharedMaterial = ownedMaterial;
                ownedMaterial.color = isFaceUp ? new Color(0.72f, 0.62f, 0.42f) : isSelectable ? new Color(0.22f, 0.11f, 0.17f) : new Color(0.13f, 0.07f, 0.10f);
            }
            if (faceLabel == null) return;
            faceLabel.text = isFaceUp ? GetFaceText() : "?";
            faceLabel.color = isFaceUp ? new Color(0.12f, 0.08f, 0.05f) : new Color(0.92f, 0.82f, 0.62f);
        }

        protected override bool CanHover() => isSelectable;

        protected override void OnClickReleased()
        {
            if (isSelectable)
                Selected?.Invoke(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (ownedMaterial != null)
                Destroy(ownedMaterial);
        }

        private string GetFaceText()
        {
            if (isDeathDeck)
                return string.IsNullOrWhiteSpace(deathFaceLabel) ? "死亡判定牌" : deathFaceLabel;
            if (isOldMaid)
                return "鬼牌";
            if (isOldMaidDeck)
                return "安全";
            return value.ToString();
        }
    }
}
