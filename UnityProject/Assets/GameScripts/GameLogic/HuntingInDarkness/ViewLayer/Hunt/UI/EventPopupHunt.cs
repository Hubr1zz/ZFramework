using UnityEngine;
using UnityEngine.UI;

namespace UI.Hunt
{
    /// <summary>
    /// 狩猎阶段简化版事件弹窗（直接显示文本 + 确认按钮）。
    /// 完整版在 Settlement EventPopup，此处仅展示文本后关闭。
    /// </summary>
    public class EventPopupHunt : MonoBehaviour
    {
        public System.Action OnClose;
        private Text _body;

        private void Awake()
        {
            gameObject.AddComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.98f);
            var bGo = new GameObject("Body"); bGo.transform.SetParent(transform, false);
            var rt = bGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, 0.15f); rt.anchorMax = new Vector2(0.95f, 0.9f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _body = bGo.AddComponent<Text>();
            _body.fontSize = 14; _body.alignment = TextAnchor.UpperLeft;
            _body.color = Color.white;
            _body.font  = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _body.verticalOverflow   = VerticalWrapMode.Overflow;

            var btnGo = new GameObject("Confirm"); btnGo.transform.SetParent(transform, false);
            var brt = btnGo.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.35f, 0.02f); brt.anchorMax = new Vector2(0.65f, 0.13f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            btnGo.AddComponent<Image>().color = new Color(0.2f, 0.28f, 0.35f, 1f);
            btnGo.AddComponent<Button>().onClick.AddListener(() => OnClose?.Invoke());
            HuntUIManager.MakeText(btnGo, "T", "确认", 14, TextAnchor.MiddleCenter);
        }

        public void Show(string text) => _body.text = text;
    }
}
