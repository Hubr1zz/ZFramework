using TMPro;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    /// <summary>年鉴滚动条目的持久化模板。</summary>
    public sealed class ScreenLedgerEntry : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;

        public string Title => titleText != null ? titleText.text : string.Empty;
        public string Body => bodyText != null ? bodyText.text : string.Empty;

        public void Configure(string title, string body, bool completed)
        {
            if (titleText == null || bodyText == null)
                throw new MissingReferenceException($"[{nameof(ScreenLedgerEntry)}] 年鉴条目引用未完整绑定。\n对象：{name}");
            titleText.text = title ?? string.Empty;
            bodyText.text = body ?? string.Empty;
            Color color = completed ? new Color(0.90f, 0.87f, 0.78f) : new Color(0.75f, 0.82f, 0.98f);
            titleText.color = color;
            bodyText.color = color;
            gameObject.SetActive(true);
        }
    }
}
