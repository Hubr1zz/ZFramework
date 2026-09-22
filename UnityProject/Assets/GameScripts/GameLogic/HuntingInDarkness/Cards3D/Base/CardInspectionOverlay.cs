using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

    /// <summary>悬停实体卡后按 F 打开的屏幕详情层；卡面保持实体表现，长文本在此完整显示。</summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class CardInspectionOverlay : MonoBehaviour
    {
        private const string CloseHint = "F / Esc / 鼠标右键关闭";
        private const float HoverHintMargin = 12f;

        [Header("UI 引用")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text footerText;
        [SerializeField] private ScrollRect bodyScrollRect;
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform hoverHintRect;
        [SerializeField] private TMP_Text hoverHintText;

        [Header("输入")]
        [SerializeField] private Camera inputCamera;

        private static CardInspectionOverlay instance;
        private static CardView3D hoveredCard;
        private static CardView3D activeCard;
        private static CardView3D cachedContentCard;
        private static bool hasCachedContent;
        private static bool blockUntilMouseReleased;
        private static int closeFrame = -1;

        private int cachedEventMask;
        private bool hasCachedEventMask;

        public static bool BlocksWorldInput => activeCard != null || blockUntilMouseReleased;

        public static void SetHovered(CardView3D card)
        {
            if (hoveredCard == card) return;
            hoveredCard = card;
            CacheHoveredContent(card);
        }

        public static void ClearHovered(CardView3D card)
        {
            if (hoveredCard != card) return;
            hoveredCard = null;
            cachedContentCard = null;
            hasCachedContent = false;
        }

        public static void ClearCard(CardView3D card)
        {
            if (activeCard == card) Close();
            ClearHovered(card);
        }

        public static bool Open(CardView3D card)
        {
            if (card == null || !card.isActiveAndEnabled || card.IsDragging) return false;
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2)) return false;
            if (BlocksWorldInput) return false;
            foreach (CardView3D currentCard in CardView3D.AllCards)
                if (currentCard != null && currentCard.IsDragging) return false;

            if (instance == null)
            {
                Debug.LogError($"[{nameof(CardInspectionOverlay)}] 场景中没有已注册的详情层 Prefab 实例。", card);
                return false;
            }

            if (!instance.TryGetContent(card, out CardInspectionContent nextContent)) return false;
            if (!instance.ValidateRuntimeReferences(card)) return false;

            hoveredCard = card;
            cachedContentCard = card;
            hasCachedContent = true;
            activeCard = card;
            blockUntilMouseReleased = false;
            closeFrame = -1;
            instance.modalRoot.SetActive(true);
            instance.ApplyContent(nextContent);
            instance.SetHoverHintVisible(false);
            instance.CacheAndBlockInputCamera();
            return true;
        }

        public static void Close()
        {
            if (activeCard == null) return;
            CardView3D closedCard = activeCard;
            activeCard = null;
            blockUntilMouseReleased = true;
            closeFrame = Time.frameCount;
            ClearHovered(closedCard);
            if (instance != null)
            {
                instance.modalRoot?.SetActive(false);
                instance.SetHoverHintVisible(false);
            }
        }

        private void Awake()
        {
            closeButton?.onClick.AddListener(Close);
            modalRoot?.SetActive(false);
            SetHoverHintVisible(false);
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
            ReleaseInputBlockIfSafe();

            if (activeCard != null)
            {
                if (!activeCard.isActiveAndEnabled)
                {
                    Close();
                    return;
                }

                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(1))
                    Close();
                return;
            }

            if (hoveredCard == null)
            {
                SetHoverHintVisible(false);
                return;
            }

            if (!hoveredCard.isActiveAndEnabled)
            {
                ClearHovered(hoveredCard);
                return;
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                Open(hoveredCard);
                return;
            }

            TryShowHoverHint();
            UpdateHoverHintPosition();
        }

        private static void CacheHoveredContent(CardView3D card)
        {
            cachedContentCard = card;
            if (card == null || !card.isActiveAndEnabled)
            {
                hasCachedContent = false;
                return;
            }

            hasCachedContent = card.TryGetInspectionContent(out _);
        }

        private bool TryGetContent(CardView3D card, out CardInspectionContent content)
        {
            if (!card.TryGetInspectionContent(out content)) return false;
            cachedContentCard = card;
            hasCachedContent = true;
            return true;
        }

        private bool ValidateRuntimeReferences(CardView3D card)
        {
            if (inputCamera == null)
            {
                Debug.LogError($"[{nameof(CardInspectionOverlay)}] inputCamera 未绑定，无法打开详情层。", card);
                return false;
            }

            if (canvas == null || modalRoot == null || titleText == null || bodyText == null || footerText == null || bodyScrollRect == null || closeButton == null || hoverHintRect == null || hoverHintText == null)
            {
                Debug.LogError($"[{nameof(CardInspectionOverlay)}] uGUI 引用未完整绑定，无法打开详情层。", this);
                return false;
            }

            return true;
        }

        private void ApplyContent(CardInspectionContent nextContent)
        {
            titleText.text = nextContent.Title;
            bodyText.text = nextContent.Body;
            footerText.text = string.IsNullOrWhiteSpace(nextContent.Footer) ? CloseHint : $"{nextContent.Footer}\n{CloseHint}";
            bodyScrollRect.verticalNormalizedPosition = 1f;
            Canvas.ForceUpdateCanvases();
            bodyScrollRect.verticalNormalizedPosition = 1f;
        }

        private void CacheAndBlockInputCamera()
        {
            if (hasCachedEventMask) return;
            cachedEventMask = inputCamera.eventMask;
            hasCachedEventMask = true;
            inputCamera.eventMask = 0;
        }

        private void RestoreInputCameraMask()
        {
            if (!hasCachedEventMask) return;
            if (inputCamera != null) inputCamera.eventMask = cachedEventMask;
            hasCachedEventMask = false;
        }

        private void ReleaseInputBlockIfSafe()
        {
            if (!blockUntilMouseReleased || Time.frameCount <= closeFrame || Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2) || Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2)) return;
            blockUntilMouseReleased = false;
            closeFrame = -1;
            RestoreInputCameraMask();
        }

        private void TryShowHoverHint()
        {
            bool canShow = !BlocksWorldInput && !Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetMouseButton(2) && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()) && hoveredCard != null && hoveredCard.isActiveAndEnabled && hasCachedContent && cachedContentCard == hoveredCard;
            SetHoverHintVisible(canShow);
        }

        private void SetHoverHintVisible(bool visible)
        {
            if (hoverHintRect != null && hoverHintRect.gameObject.activeSelf != visible)
                hoverHintRect.gameObject.SetActive(visible);
            if (visible && hoverHintText != null) hoverHintText.text = "F  查看详情";
        }

        private void UpdateHoverHintPosition()
        {
            if (hoverHintRect == null || canvas == null || !hoverHintRect.gameObject.activeInHierarchy) return;
            RectTransform canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null) return;

            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, eventCamera, out Vector2 localPoint)) return;

            Vector2 size = hoverHintRect.rect.size;
            Vector2 pivot = hoverHintRect.pivot;
            float minX = canvasRect.rect.xMin + HoverHintMargin + size.x * pivot.x;
            float maxX = canvasRect.rect.xMax - HoverHintMargin - size.x * (1f - pivot.x);
            float minY = canvasRect.rect.yMin + HoverHintMargin + size.y * pivot.y;
            float maxY = canvasRect.rect.yMax - HoverHintMargin - size.y * (1f - pivot.y);
            localPoint.x = Mathf.Clamp(localPoint.x + size.x * 0.5f, minX, maxX);
            localPoint.y = Mathf.Clamp(localPoint.y + size.y * 0.5f, minY, maxY);
            hoverHintRect.anchoredPosition = localPoint;
        }

        private void OnDisable()
        {
            if (instance != this) return;
            ResetState();
            instance = null;
        }

        private void OnDestroy()
        {
            closeButton?.onClick.RemoveListener(Close);
            if (instance != this) return;
            ResetState();
            instance = null;
        }

        private void ResetState()
        {
            RestoreInputCameraMask();
            activeCard = null;
            hoveredCard = null;
            cachedContentCard = null;
            hasCachedContent = false;
            blockUntilMouseReleased = false;
            closeFrame = -1;
            modalRoot?.SetActive(false);
            SetHoverHintVisible(false);
        }
    }
}
