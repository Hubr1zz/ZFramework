using System.Collections.Generic;
using UnityEngine;

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
    public sealed class CardInspectionOverlay : MonoBehaviour
    {
        private static CardInspectionOverlay instance;
        private static CardView3D hoveredCard;
        private static CardView3D activeCard;
        private CardInspectionContent content;
        private Vector2 scrollPosition;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle footerStyle;
        private GUIStyle hintStyle;
        private readonly List<Collider> blockedColliders = new();

        public static bool BlocksWorldInput => activeCard != null;

        public static void SetHovered(CardView3D card)
        {
            hoveredCard = card;
            EnsureInstance();
        }

        public static void ClearHovered(CardView3D card)
        {
            if (hoveredCard == card)
                hoveredCard = null;
        }

        public static void ClearCard(CardView3D card)
        {
            ClearHovered(card);
            if (activeCard == card)
                Close();
        }

        public static bool Open(CardView3D card)
        {
            if (card == null || !card.isActiveAndEnabled || !card.TryGetInspectionContent(out CardInspectionContent nextContent)) return false;
            EnsureInstance();
            if (instance == null) return false;
            hoveredCard = card;
            activeCard = card;
            instance.content = nextContent;
            instance.scrollPosition = Vector2.zero;
            instance.BlockWorldColliders();
            return true;
        }

        private static void EnsureInstance()
        {
            if (instance != null) return;
            Camera camera = Camera.main;
            if (camera == null) return;
            instance = camera.GetComponent<CardInspectionOverlay>() ?? camera.gameObject.AddComponent<CardInspectionOverlay>();
        }

        private void Update()
        {
            if (activeCard != null)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(1))
                    Close();
                return;
            }
            if (hoveredCard == null || !hoveredCard.isActiveAndEnabled || !Input.GetKeyDown(KeyCode.F)) return;
            Open(hoveredCard);
        }

        private static void Close()
        {
            activeCard = null;
            if (instance != null)
            {
                instance.scrollPosition = Vector2.zero;
                instance.RestoreWorldColliders();
            }
        }

        private void BlockWorldColliders()
        {
            RestoreWorldColliders();
            foreach (Collider collider in FindObjectsByType<Collider>())
            {
                if (collider == null || !collider.enabled) continue;
                blockedColliders.Add(collider);
                collider.enabled = false;
            }
        }

        private void RestoreWorldColliders()
        {
            foreach (Collider collider in blockedColliders)
                if (collider != null)
                    collider.enabled = true;
            blockedColliders.Clear();
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (activeCard == null)
            {
                DrawHoverHint();
                return;
            }

            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
            GUI.color = previousColor;

            float width = Mathf.Min(760f, Screen.width * 0.72f);
            float height = Mathf.Min(680f, Screen.height * 0.78f);
            var area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(area, GUI.skin.window);
            GUILayout.Label(content.Title, titleStyle);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
            GUILayout.Label(content.Body, bodyStyle);
            GUILayout.EndScrollView();
            if (!string.IsNullOrWhiteSpace(content.Footer))
                GUILayout.Label(content.Footer, footerStyle);
            GUILayout.Label("F / Esc / 鼠标右键关闭", hintStyle);
            GUILayout.EndArea();
        }

        private void DrawHoverHint()
        {
            if (hoveredCard == null || !hoveredCard.isActiveAndEnabled || !hoveredCard.TryGetInspectionContent(out _)) return;
            Vector3 mouse = Input.mousePosition;
            var rect = new Rect(mouse.x + 18f, Screen.height - mouse.y + 14f, 130f, 30f);
            GUI.Box(rect, "F  查看详情", hintStyle);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 28, fontStyle = FontStyle.Bold, wordWrap = true };
            bodyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = 20, wordWrap = true, richText = true };
            footerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, wordWrap = true };
            hintStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 15 };
        }

        private void OnDestroy()
        {
            RestoreWorldColliders();
            if (instance != this) return;
            instance = null;
            hoveredCard = null;
            activeCard = null;
        }
    }
}
