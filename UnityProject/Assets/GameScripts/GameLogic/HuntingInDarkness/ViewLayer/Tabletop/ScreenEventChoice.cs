using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Cards3D;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    /// <summary>持久化 uGUI 选项模板的运行时状态。</summary>
    public sealed class ScreenEventChoice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IScrollHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text statusText;

        private Action selected;
        private bool clicked;
        private CardInspectionContent? inspectionContent;

        public string DisplayName => titleText != null ? titleText.text : string.Empty;
        public string Body => bodyText != null ? bodyText.text : string.Empty;
        public string Status => statusText != null ? statusText.text : string.Empty;
        public bool IsInteractable { get; private set; }
        public Button Button => button;

        public void Configure(string title, string body, bool interactable, string status, Action onSelected, bool readOnly = false)
        {
            ValidateReferences();
            clicked = false;
            selected = onSelected;
            IsInteractable = interactable;
            titleText.text = title ?? string.Empty;
            bodyText.text = body ?? string.Empty;
            statusText.text = status ?? string.Empty;
            button.interactable = interactable;
            Color titleColor = interactable || readOnly ? new Color(0.98f, 0.82f, 0.45f) : new Color(0.50f, 0.50f, 0.50f);
            Color bodyColor = interactable || readOnly ? new Color(0.90f, 0.88f, 0.82f) : new Color(0.48f, 0.48f, 0.48f);
            Color statusColor = interactable || readOnly ? new Color(0.72f, 0.78f, 0.82f) : new Color(0.72f, 0.42f, 0.40f);
            titleText.color = titleColor;
            bodyText.color = bodyColor;
            statusText.color = statusColor;
            inspectionContent = null;
            gameObject.SetActive(true);
        }

        public void SetInspectionContent(CardInspectionContent content) => inspectionContent = content;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (inspectionContent.HasValue)
            {
                CardInspectionOverlay.ShowHover(this, inspectionContent.Value);
                return;
            }

            string body = bodyText.text;
            if (!string.IsNullOrWhiteSpace(statusText.text)) body = string.IsNullOrWhiteSpace(body) ? statusText.text : $"{body}\n\n{statusText.text}";
            CardInspectionOverlay.ShowHover(this, new CardInspectionContent(titleText.text, body, string.Empty));
        }

        public void OnPointerExit(PointerEventData eventData) => CardInspectionOverlay.HideHover(this);

        public void OnScroll(PointerEventData eventData)
        {
            if (CardInspectionOverlay.ConsumesScroll || transform.parent == null) return;
            ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, ExecuteEvents.scrollHandler);
        }

        public void FitCampaignContent(float availableWidth)
        {
            if (availableWidth <= 32f) throw new ArgumentOutOfRangeException(nameof(availableWidth));
            RectTransform row = transform as RectTransform;
            LayoutElement layout = GetComponent<LayoutElement>();
            if (row == null || layout == null) throw new MissingReferenceException($"[{nameof(ScreenEventChoice)}] 列表模板缺少行 RectTransform 或 LayoutElement：{name}");

            const float horizontalPadding = 16f;
            const float verticalPadding = 10f;
            const float gap = 5f;
            float textWidth = availableWidth - horizontalPadding * 2f;
            float titleHeight = Mathf.Max(24f, titleText.GetPreferredValues(titleText.text, textWidth, 0f).y);
            float bodyHeight = string.IsNullOrEmpty(bodyText.text) ? 0f : Mathf.Max(20f, bodyText.GetPreferredValues(bodyText.text, textWidth, 0f).y);
            float statusHeight = string.IsNullOrEmpty(statusText.text) ? 0f : Mathf.Max(18f, statusText.GetPreferredValues(statusText.text, textWidth, 0f).y);
            float rowHeight = verticalPadding * 2f + titleHeight + gap + bodyHeight;
            if (statusHeight > 0f) rowHeight += gap + statusHeight;

            SetTextRect(titleText.rectTransform, horizontalPadding, verticalPadding, textWidth, titleHeight);
            SetTextRect(bodyText.rectTransform, horizontalPadding, verticalPadding + titleHeight + gap, textWidth, bodyHeight);
            SetTextRect(statusText.rectTransform, horizontalPadding, verticalPadding + titleHeight + gap + bodyHeight + gap, textWidth, statusHeight);
            layout.minHeight = rowHeight;
            layout.preferredHeight = rowHeight;
            LayoutRebuilder.MarkLayoutForRebuild(row);
        }

        private static void SetTextRect(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        public void Click()
        {
            if (!IsInteractable || clicked) return;
            clicked = true;
            selected?.Invoke();
        }

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(Click);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Click);
            CardInspectionOverlay.HideHover(this);
        }

        private void OnDisable() => CardInspectionOverlay.HideHover(this);

        private void ValidateReferences()
        {
            if (button == null || titleText == null || bodyText == null || statusText == null)
                throw new MissingReferenceException($"[{nameof(ScreenEventChoice)}] 选项模板引用未完整绑定。\n对象：{name}");
        }
    }

}
