using System;
using Core;
using Cards3D;
using GameplayBase;
using GameplayBase.CombatSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuntingInDarkness.ViewLayer.Combat
{
    public sealed class CombatScreenActionCardWidget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IScrollHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private Image facePanel;
        [SerializeField] private float hoverLift = 12f;
        [SerializeField] private float hoverDuration = 0.25f;

        public int CardInstanceId { get; private set; }
        private Action<int> previewAction;
        private Action clearPreviewAction;
        private Vector2 restPosition;
        private bool restPositionCaptured;
        private bool isHovered;
        private float hoverProgress;
        private CardInspectionContent inspectionContent;

        public void Bind(CharacterActionCardInstance card, bool selected, string availability, Action<int> onSelect, Action<int> onPreview, Action onPreviewEnded)
        {
            if (button == null) throw new MissingReferenceException($"[{nameof(CombatScreenActionCardWidget)}] button 未绑定。");
            if (cardRect == null) throw new MissingReferenceException($"[{nameof(CombatScreenActionCardWidget)}] cardRect 未绑定。");
            if (nameLabel == null) throw new MissingReferenceException($"[{nameof(CombatScreenActionCardWidget)}] nameLabel 未绑定。");
            if (costLabel == null) throw new MissingReferenceException($"[{nameof(CombatScreenActionCardWidget)}] costLabel 未绑定。");
            if (descriptionLabel == null) throw new MissingReferenceException($"[{nameof(CombatScreenActionCardWidget)}] descriptionLabel 未绑定。");
            if (facePanel == null) throw new MissingReferenceException($"[{nameof(CombatScreenActionCardWidget)}] facePanel 未绑定。");
            CardInstanceId = card.InstanceId;
            previewAction = onPreview;
            clearPreviewAction = onPreviewEnded;
            if (!restPositionCaptured)
            {
                restPosition = cardRect.anchoredPosition;
                restPositionCaptured = true;
            }
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onSelect?.Invoke(CardInstanceId));
            button.interactable = true;
            bool faceUp = card.CurrentFace == CardFace.FaceUp;
            nameLabel.text = $"{(selected ? "▶ " : string.Empty)}{(faceUp ? card.CardName : $"{card.CardName} · 背面")}";
            if (!faceUp)
                costLabel.text = string.IsNullOrWhiteSpace(availability) ? "× 背面 · 不可打出" : $"× {availability}";
            else
                costLabel.text = string.IsNullOrWhiteSpace(availability) ? card.CostDescription : $"× {availability} · {card.CostDescription}";
            descriptionLabel.text = faceUp ? card.FaceUpDescription : card.FaceDownDescription;
            string currentFaceDescription = faceUp ? card.FaceUpDescription : card.FaceDownDescription;
            string costDetails = faceUp ? card.CostDescription : "背面朝上，当前不可打出。";
            string availabilityDetails = string.IsNullOrWhiteSpace(availability) ? string.Empty : $"当前状态：{availability}";
            inspectionContent = new CardInspectionContent(card.CardName, $"{costDetails}\n\n{currentFaceDescription}", availabilityDetails);
            nameLabel.color = new Color(0.96f, 0.92f, 0.83f, 1f);
            costLabel.color = string.IsNullOrWhiteSpace(availability) ? new Color(0.85f, 0.81f, 0.73f, 1f) : new Color(1f, 0.66f, 0.48f, 1f);
            descriptionLabel.color = new Color(0.87f, 0.86f, 0.82f, 1f);
            facePanel.color = selected ? new Color(0.39f, 0.33f, 0.22f, 1f) : faceUp ? new Color(0.24f, 0.25f, 0.27f, 1f) : new Color(0.20f, 0.21f, 0.23f, 1f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            previewAction?.Invoke(CardInstanceId);
            CardInspectionOverlay.ShowHover(this, inspectionContent);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            clearPreviewAction?.Invoke();
            CardInspectionOverlay.HideHover(this);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (CardInspectionOverlay.ConsumesScroll || transform.parent == null) return;
            ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, ExecuteEvents.scrollHandler);
        }

        private void OnDisable()
        {
            CardInspectionOverlay.HideHover(this);
        }

        private void Update()
        {
            float direction = isHovered ? 1f : -1f;
            hoverProgress = Mathf.Clamp01(hoverProgress + direction * Time.unscaledDeltaTime / Mathf.Max(0.01f, hoverDuration));
            cardRect.anchoredPosition = restPosition + Vector2.up * (hoverLift * hoverProgress);
        }
    }
}
