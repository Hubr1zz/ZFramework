using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    /// <summary>
    /// 卡堆预览面板。继承 WorldSpaceViewPanel（平铺桌面、深色背板）。
    /// 由 CardSlot 在「Shift + 鼠标悬停」时创建/填充/隐藏。
    ///
    /// - 可预览卡堆：逐行列出每张卡的 DisplayName
    /// - 不可预览卡堆：仅显示一行占位文字（后续替换为「无法查看」特殊卡）
    /// </summary>
    public class StackPreviewPanel : WorldSpaceViewPanel
    {
        const float PanelWidth = 1.6f;   // 世界单位
        const float RowHeight  = 0.17f;
        const float TopPad     = 0.34f;  // 标题占用
        const float BotPad     = 0.12f;

        readonly List<TextMeshPro> _rows = new();

        // ─── 工厂 ─────────────────────────────────────────────────────────

        public static StackPreviewPanel Create(Transform parent)
        {
            var go = new GameObject("StackPreview");
            go.transform.SetParent(parent, false);
            var panel = go.AddComponent<StackPreviewPanel>();
            panel.BuildBase();
            panel.gameObject.SetActive(false);
            return panel;
        }

        // ─── 显示 ─────────────────────────────────────────────────────────

        /// <summary>在 worldPos 显示预览。title：标题；lines：内容行。</summary>
        public void Show(string title, IReadOnlyList<string> lines, Vector3 worldPos)
        {
            Title.text = title;

            int n = lines?.Count ?? 0;
            EnsureRows(n);

            // 行从标题下方开始，沿 -Z 往下排
            float h = TopPad + n * RowHeight + BotPad;
            SetSize(PanelWidth, h);

            float startZ = h * 0.5f - TopPad;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (i < n)
                {
                    var row = _rows[i];
                    row.gameObject.SetActive(true);
                    row.text = lines[i];
                    row.transform.localPosition = new Vector3(0f, 0.02f, startZ - i * RowHeight);
                }
                else _rows[i].gameObject.SetActive(false);
            }

            ShowAt(worldPos);
        }

        // ─── 行管理 ───────────────────────────────────────────────────────

        private void EnsureRows(int count)
        {
            while (_rows.Count < count)
            {
                var go = new GameObject($"Row_{_rows.Count}");
                go.transform.SetParent(ContentRoot, false);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var tmp = go.AddComponent<TextMeshPro>();
                tmp.fontSize  = 0.12f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color     = new Color(0.88f, 0.90f, 0.95f);
                tmp.rectTransform.sizeDelta = new Vector2(PanelWidth - 0.12f, RowHeight);
                _rows.Add(tmp);
            }
        }
    }
}
