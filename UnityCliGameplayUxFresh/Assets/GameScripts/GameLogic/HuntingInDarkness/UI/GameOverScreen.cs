using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 游戏结束画面（全猎人死亡时显示）。
    /// 由 GameManager 在收到 GameOverEvent 时创建并激活。
    /// </summary>
    public class GameOverScreen : MonoBehaviour
    {
        public System.Action OnRestart;

        private Text _reasonText;

        private void Awake() => BuildLayout();

        private void BuildLayout()
        {
            // 半透明深色全屏遮罩
            var img = gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.88f);

            var rt = GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // 中心卡片
            var cardGo = new GameObject("Card");
            cardGo.transform.SetParent(transform, false);
            var crt = cardGo.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.25f, 0.3f);
            crt.anchorMax = new Vector2(0.75f, 0.75f);
            crt.offsetMin = crt.offsetMax = Vector2.zero;
            cardGo.AddComponent<Image>().color = new Color(0.08f, 0.04f, 0.04f, 1f);

            // 标题
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(cardGo.transform, false);
            var trt = titleGo.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, 0.68f); trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            titleGo.AddComponent<Image>().color = new Color(0.25f, 0.04f, 0.04f, 1f);
            MakeText(titleGo, "T", "── 黑暗吞噬一切 ──", 20, TextAnchor.MiddleCenter)
                .color = new Color(1f, 0.25f, 0.2f);

            // 原因文字
            var reasonGo = new GameObject("Reason");
            reasonGo.transform.SetParent(cardGo.transform, false);
            var rrt = reasonGo.AddComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0.05f, 0.32f);
            rrt.anchorMax = new Vector2(0.95f, 0.67f);
            rrt.offsetMin = rrt.offsetMax = Vector2.zero;
            _reasonText = reasonGo.AddComponent<Text>();
            _reasonText.fontSize  = 13;
            _reasonText.alignment = TextAnchor.MiddleCenter;
            _reasonText.color     = new Color(0.85f, 0.75f, 0.7f);
            _reasonText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _reasonText.horizontalOverflow = HorizontalWrapMode.Wrap;

            // 重新开始按钮
            var btnGo = new GameObject("RestartBtn");
            btnGo.transform.SetParent(cardGo.transform, false);
            var brt = btnGo.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.15f, 0.06f);
            brt.anchorMax = new Vector2(0.85f, 0.28f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            btnGo.AddComponent<Image>().color = new Color(0.3f, 0.08f, 0.08f, 1f);
            var btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(() => OnRestart?.Invoke());
            MakeText(btnGo, "T", "↺ 重新开始", 15, TextAnchor.MiddleCenter)
                .color = new Color(1f, 0.65f, 0.6f);
        }

        public void Show(string reason)
        {
            gameObject.SetActive(true);
            if (_reasonText != null)
                _reasonText.text = string.IsNullOrEmpty(reason)
                    ? "全部猎人已经倒下。"
                    : reason;
        }

        private static Text MakeText(GameObject parent, string name, string text,
            int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            if (!go.TryGetComponent<RectTransform>(out var rt))
            {
                rt = go.AddComponent<RectTransform>();
            }
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.text = text; t.fontSize = fontSize; t.alignment = anchor;
            t.color = Color.white;
            t.font  = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return t;
        }
    }
}
