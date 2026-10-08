using TMPro;
using UnityEngine.EventSystems;
using HuntingInDarkness.ViewLayer.Tabletop;
using UnityEngine;
using UnityEngine.UI;

namespace Cards3D
{
    public readonly struct CardInspectionContent
    {
        public CardInspectionContent(string title, string body, string footer)
        {
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
            Footer = footer ?? string.Empty;
        }

        public string Title { get; }
        public string Body { get; }
        public string Footer { get; }
    }

    /// <summary>桌面卡牌和 UI 选项共用的非模态悬停详情面板。</summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class CardInspectionOverlay : MonoBehaviour
    {
        [Header("正式 UI 引用")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private RectTransform panelBackgroundRect;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text footerText;
        [SerializeField] private ScrollRect bodyScrollRect;
        [SerializeField] private Button closeButton;

        [Header("悬停布局")]
        [SerializeField, Min(0f)] private float hoverDelay = 0.18f;
        [SerializeField, Min(160f)] private float preferredWidth = 420f;
        [SerializeField, Min(200f)] private float maximumHeight = 620f;
        [SerializeField, Min(0f)] private float screenMargin = 12f;
        [SerializeField, Min(0f)] private float cursorOffset = 12f;
        [SerializeField, Min(0f)] private float scrollWheelSpeed = 0.12f;
        [SerializeField, Min(12f)] private float titleFontSize = 24f;
        [SerializeField, Min(12f)] private float bodyFontSize = 20f;
        [SerializeField, Min(12f)] private float footerFontSize = 16f;

        private static CardInspectionOverlay instance;
        private static UnityEngine.Object hoverOwner;
        private static CardInspectionContent hoverContent;
        private static float hoverStartedAt;
        private static bool hoverPending;

        private RectTransform bodyViewportRect;
        private RectTransform footerRect;
        private RectTransform titleRect;
        private RectTransform backgroundRect;
        private bool contentApplied;
        private Vector2 lastCanvasSize;
        private float lastCanvasScale;

        public static bool BlocksWorldInput => ScreenModalInputGate.IsBlocked;
        public static bool IsShowingHover => instance != null && instance.modalRoot != null && instance.modalRoot.activeSelf;
        public static bool ConsumesScroll => IsShowingHover && instance.contentApplied && instance.bodyScrollRect != null && instance.bodyText.preferredHeight > instance.bodyScrollRect.viewport.rect.height + 1f;

        public static void Scroll(float amount)
        {
            if (!ConsumesScroll || instance == null) return;
            instance.bodyScrollRect.verticalNormalizedPosition = Mathf.Clamp01(instance.bodyScrollRect.verticalNormalizedPosition + amount * instance.scrollWheelSpeed);
        }

        public static void ShowHover(UnityEngine.Object owner, CardInspectionContent content)
        {
            if (owner == null) return;
            if (instance == null) return;
            if (hoverOwner == owner && hoverContent.Title == content.Title && hoverContent.Body == content.Body && hoverContent.Footer == content.Footer && instance.contentApplied && IsShowingHover) return;

            hoverOwner = owner;
            hoverContent = content;
            hoverStartedAt = Time.unscaledTime;
            hoverPending = true;
            instance.contentApplied = false;
            instance.modalRoot.SetActive(false);
        }

        public static void HideHover(UnityEngine.Object owner)
        {
            if (owner == null || hoverOwner != owner) return;
            hoverOwner = null;
            hoverPending = false;
            if (instance != null)
            {
                instance.contentApplied = false;
                instance.modalRoot?.SetActive(false);
            }
        }

        public static void SetHovered(CardView3D card)
        {
            if (card == null || !card.isActiveAndEnabled || card.IsDragging) return;
            if (card.TryGetInspectionContent(out CardInspectionContent content)) ShowHover(card, content);
            else HideHover(card);
        }

        public static void ClearHovered(CardView3D card) => HideHover(card);

        public static void ClearCard(CardView3D card) => HideHover(card);

        /// <summary>兼容旧调用：请求打开时立即显示悬停详情，不进入模态状态。</summary>
        public static bool Open(CardView3D card)
        {
            if (card == null || !card.isActiveAndEnabled || card.IsDragging || instance == null) return false;
            if (!card.TryGetInspectionContent(out CardInspectionContent content)) return false;
            ShowHover(card, content);
            instance.ApplyContent(content);
            instance.ShowPanel();
            return true;
        }

        public static void Close()
        {
            if (hoverOwner != null) HideHover(hoverOwner);
        }

        private void Awake()
        {
            if (canvas == null || modalRoot == null || panelRect == null || panelBackgroundRect == null || panelBackgroundRect.GetComponent<Image>() == null || titleText == null || bodyText == null || footerText == null || bodyScrollRect == null || bodyScrollRect.viewport == null)
            {
                Debug.LogError($"[{nameof(CardInspectionOverlay)}] 正式详情面板引用未完整绑定。", this);
                enabled = false;
                return;
            }

            titleRect = titleText.rectTransform;
            bodyViewportRect = bodyScrollRect.viewport;
            footerRect = footerText.rectTransform;
            backgroundRect = panelBackgroundRect;

            closeButton?.gameObject.SetActive(false);
            bodyScrollRect.horizontal = false;
            bodyScrollRect.vertical = true;
            if (bodyScrollRect.verticalScrollbar != null)
            {
                bodyScrollRect.verticalScrollbar.gameObject.SetActive(false);
                bodyScrollRect.verticalScrollbar = null;
            }
            titleText.alignment = TextAlignmentOptions.Left;
            bodyText.alignment = TextAlignmentOptions.Left;
            footerText.alignment = TextAlignmentOptions.Left;
            bodyText.raycastTarget = false;
            titleText.raycastTarget = false;
            footerText.raycastTarget = false;
            foreach (Graphic graphic in modalRoot.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            modalRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (instance != null && instance != this)
            {
                Debug.LogError($"[{nameof(CardInspectionOverlay)}] 场景中存在多个详情层实例。", this);
                enabled = false;
                return;
            }
            instance = this;
        }

        private void Update()
        {
            if (hoverOwner == null)
            {
                if (IsShowingHover) modalRoot.SetActive(false);
                contentApplied = false;
                hoverPending = false;
                return;
            }

            if (hoverOwner is Component component && (component == null || !component.gameObject.activeInHierarchy) || hoverOwner is GameObject gameObject && !gameObject.activeInHierarchy)
            {
                hoverOwner = null;
                hoverPending = false;
                contentApplied = false;
                modalRoot.SetActive(false);
                return;
            }

            if (hoverOwner is CardView3D worldCard && (BlocksWorldInput || worldCard.IsDragging || EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            {
                HideHover(worldCard);
                return;
            }

            if (hoverPending && Time.unscaledTime - hoverStartedAt >= hoverDelay)
            {
                hoverPending = false;
                ApplyContent(hoverContent);
                ShowPanel();
            }

            if (!IsShowingHover) return;
            RectTransform canvasRect = canvas.transform as RectTransform;
            if (canvasRect != null && (canvasRect.rect.size != lastCanvasSize || !Mathf.Approximately(canvas.scaleFactor, lastCanvasScale))) LayoutPanel();
            UpdatePanelPosition();
            ReadScrollWheel();
        }

        private void ApplyContent(CardInspectionContent content)
        {
            titleText.text = content.Title;
            bodyText.text = content.Body;
            footerText.text = content.Footer;
            titleText.fontSize = titleFontSize;
            bodyText.fontSize = bodyFontSize;
            footerText.fontSize = footerFontSize;
            contentApplied = true;
            Canvas.ForceUpdateCanvases();
            LayoutPanel();
            bodyScrollRect.verticalNormalizedPosition = 1f;
        }

        private void LayoutPanel()
        {
            RectTransform canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null) return;
            float width = Mathf.Min(preferredWidth, canvasRect.rect.width - screenMargin * 2f);
            float heightLimit = Mathf.Min(maximumHeight, canvasRect.rect.height - screenMargin * 2f);
            const float padding = 18f;
            const float sectionGap = 10f;
            float contentWidth = Mathf.Max(40f, width - padding * 2f);
            float titleHeight = Mathf.Max(24f, titleText.GetPreferredValues(titleText.text, contentWidth, 0f).y);
            float footerHeight = string.IsNullOrWhiteSpace(footerText.text) ? 0f : Mathf.Max(20f, footerText.GetPreferredValues(footerText.text, contentWidth, 0f).y);
            float preferredBodyHeight = bodyText.GetPreferredValues(bodyText.text, contentWidth, 0f).y;
            float availableBodyHeight = Mathf.Max(48f, heightLimit - padding * 2f - titleHeight - footerHeight - sectionGap * (footerHeight > 0f ? 2f : 1f));
            float bodyHeight = Mathf.Min(Mathf.Max(48f, preferredBodyHeight), availableBodyHeight);
            float height = padding * 2f + titleHeight + bodyHeight + footerHeight + sectionGap * (footerHeight > 0f ? 2f : 1f);

            SetTopLeftRect(panelRect, 0f, 0f, width, height);
            if (backgroundRect != null) SetTopLeftRect(backgroundRect, 0f, 0f, width, height);
            SetTopLeftRect(titleRect, padding, padding, contentWidth, titleHeight);
            SetTopLeftRect(bodyViewportRect, padding, padding + titleHeight + sectionGap, contentWidth, bodyHeight);
            SetTopLeftRect(bodyText.rectTransform, 0f, 0f, contentWidth, Mathf.Max(bodyHeight, preferredBodyHeight));
            if (footerHeight > 0f) SetTopLeftRect(footerRect, padding, height - padding - footerHeight, contentWidth, footerHeight);
            lastCanvasSize = canvasRect.rect.size;
            lastCanvasScale = canvas.scaleFactor;
        }

        private static void SetTopLeftRect(RectTransform rect, float left, float top, float width, float height)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private void ShowPanel()
        {
            modalRoot.SetActive(true);
            UpdatePanelPosition();
        }

        private void UpdatePanelPosition()
        {
            PositionAt(Input.mousePosition);
        }

        private void PositionAt(Vector2 screenPosition)
        {
            RectTransform canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out Vector2 mouseLocal)) return;

            Vector2 canvasSize = canvasRect.rect.size;
            float margin = screenMargin;
            float offset = cursorOffset;
            float mouseX = mouseLocal.x - canvasRect.rect.xMin;
            float mouseY = canvasRect.rect.yMax - mouseLocal.y;
            float width = panelRect.rect.width;
            float height = panelRect.rect.height;
            float left = mouseX + offset;
            float top = mouseY + offset;
            if (left + width > canvasSize.x - margin) left = mouseX - offset - width;
            if (top + height > canvasSize.y - margin) top = mouseY - offset - height;
            left = Mathf.Clamp(left, margin, Mathf.Max(margin, canvasSize.x - width - margin));
            top = Mathf.Clamp(top, margin, Mathf.Max(margin, canvasSize.y - height - margin));
            panelRect.anchoredPosition = new Vector2(left, -top);
            if (backgroundRect != null) backgroundRect.anchoredPosition = panelRect.anchoredPosition;
        }

        private void ReadScrollWheel()
        {
            if (Mathf.Abs(Input.mouseScrollDelta.y) < 0.01f || !ConsumesScroll) return;
            Scroll(Input.mouseScrollDelta.y);
        }

        private void OnDisable()
        {
            if (instance != this) return;
            ResetState();
            instance = null;
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            ResetState();
            instance = null;
        }

        private void ResetState()
        {
            hoverOwner = null;
            hoverPending = false;
            contentApplied = false;
            modalRoot?.SetActive(false);
        }
    }
}
