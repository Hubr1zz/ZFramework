using System;
using System.Collections.Generic;
using Cards3D;
using HuntingInDarkness.Data;
using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>只读营地年鉴。把狩猎记录与时间线统一投影为分页的世界空间实体条目。</summary>
    public sealed class CampLedgerPanel3D : WorldSpaceViewPanel
    {
        private const int EntriesPerPage = 8;
        private readonly List<GameObject> entryObjects = new();
        private readonly List<LedgerEntry> entries = new();
        private TextMeshPro summaryText;
        private TextMeshPro pageText;
        private SettlementInstance settlement;
        private int pageIndex;
        private bool isBuilt;

        public static CampLedgerPanel3D Create(Transform parent)
        {
            var gameObject = new GameObject("CampLedgerPanel3D");
            gameObject.transform.SetParent(parent, false);
            var panel = gameObject.AddComponent<CampLedgerPanel3D>();
            panel.EnsureBuilt();
            panel.Hide();
            return panel;
        }

        private void Awake() => EnsureBuilt();

        public void EnsureBuilt()
        {
            if (isBuilt) return;
            isBuilt = true;
            BuildBase();
            SetSize(6.4f, 4.5f);
            summaryText = BuildText("Summary", new Vector3(0f, 0.015f, 1.78f), 0.08f, new Vector2(5.5f, 0.34f));
            pageText = BuildText("Page", new Vector3(0f, 0.015f, -1.82f), 0.07f, new Vector2(3.8f, 0.26f));
            BuildButton("PreviousPage", "上一页", new Vector3(-1.55f, 0.03f, -1.82f), new Vector3(0.62f, 0.04f, 0.30f), PreviousPage, new Color(0.20f, 0.24f, 0.30f));
            BuildButton("NextPage", "下一页", new Vector3(1.55f, 0.03f, -1.82f), new Vector3(0.62f, 0.04f, 0.30f), NextPage, new Color(0.20f, 0.24f, 0.30f));
            BuildButton("Close", "合上年鉴", new Vector3(2.70f, 0.03f, 2.02f), new Vector3(0.72f, 0.04f, 0.24f), Hide, new Color(0.38f, 0.14f, 0.13f));
        }

        public void Open(SettlementInstance settlementData, Vector3 worldPosition)
        {
            if (settlementData == null) return;
            settlement = settlementData;
            pageIndex = 0;
            Rebuild();
            ShowAt(worldPosition);
        }

        public void RefreshVisible()
        {
            if (!gameObject.activeSelf || settlement == null) return;
            Rebuild();
        }

        private void Rebuild()
        {
            ClearEntries();
            BuildEntries();
            Title.text = $"无火营地年鉴 · 第 {settlement.CurrentYear} 年";
            summaryText.text = $"本年狩猎 {settlement.HuntsCompletedThisYear}/{Mathf.Max(1, settlement.HuntsPerYear)}　总狩猎 {settlement.HuntHistory?.Count ?? 0}　时间线 {settlement.Timeline?.Count ?? 0}　存活猎人 {settlement.GetAliveHunters().Count}";
            int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)entries.Count / EntriesPerPage));
            pageIndex = Mathf.Clamp(pageIndex, 0, pageCount - 1);
            pageText.text = entries.Count == 0 ? "尚无年鉴记录" : $"第 {pageIndex + 1}/{pageCount} 页 · 共 {entries.Count} 条";
            int startIndex = pageIndex * EntriesPerPage;
            int endIndex = Mathf.Min(startIndex + EntriesPerPage, entries.Count);
            for (int index = startIndex; index < endIndex; index++)
                BuildEntryPlaque(entries[index], index - startIndex);
        }

        private void BuildEntries()
        {
            entries.Clear();
            if (settlement.Timeline != null)
            {
                foreach (AnnalEntry entry in settlement.Timeline)
                {
                    if (entry == null) continue;
                    string eventName = string.IsNullOrWhiteSpace(entry.EventName) ? entry.EventId : entry.EventName;
                    string state = entry.IsCompleted ? "已发生" : "将发生";
                    entries.Add(new LedgerEntry(entry.Year, entry.IsMilestone ? $"★ {eventName}" : eventName, $"时间线 · {state}", entry.IsCompleted));
                }
            }
            if (settlement.HuntHistory != null)
            {
                foreach (HuntRecord record in settlement.HuntHistory)
                {
                    if (record == null) continue;
                    string outcome = record.BossDefeated ? "讨伐成功" : "从黑暗中归来";
                    entries.Add(new LedgerEntry(record.Year, outcome, $"狩猎 · 出发 {record.HuntersDeployed} · 损失 {record.HuntersLost} · 带回 {FormatResources(record.CollectedResources)}", true));
                }
            }
            entries.Sort((left, right) =>
            {
                int yearComparison = right.Year.CompareTo(left.Year);
                return yearComparison != 0 ? yearComparison : string.Compare(left.Title, right.Title, StringComparison.Ordinal);
            });
        }

        private void BuildEntryPlaque(LedgerEntry entry, int visualIndex)
        {
            int column = visualIndex % 2;
            int row = visualIndex / 2;
            float x = column == 0 ? -1.48f : 1.48f;
            float z = 1.25f - row * 0.70f;
            GameObject plaque = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plaque.name = $"LedgerEntry_{entry.Year}_{visualIndex}";
            plaque.transform.SetParent(ContentRoot, false);
            plaque.transform.localPosition = new Vector3(x, 0.02f, z);
            plaque.transform.localScale = new Vector3(2.70f, 0.035f, 0.56f);
            Destroy(plaque.GetComponent<Collider>());
            plaque.GetComponent<Renderer>().material.color = entry.Completed ? new Color(0.19f, 0.18f, 0.16f) : new Color(0.18f, 0.20f, 0.28f);
            var textObject = new GameObject("Text");
            textObject.transform.SetParent(plaque.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            textObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            textObject.transform.localScale = new Vector3(1f / 2.70f, 1f, 1f / 0.56f);
            TextMeshPro text = textObject.AddComponent<TextMeshPro>();
            text.text = $"第 {entry.Year} 年 · {entry.Title}\n{entry.Detail}";
            text.fontSize = 0.072f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = entry.Completed ? new Color(0.86f, 0.82f, 0.72f) : new Color(0.74f, 0.80f, 0.95f);
            text.rectTransform.sizeDelta = new Vector2(2.52f, 0.46f);
#if UNITY_6000_0_OR_NEWER
            text.textWrappingMode = TextWrappingModes.Normal;
#else
            text.enableWordWrapping = true;
#endif
            text.overflowMode = TextOverflowModes.Ellipsis;
            entryObjects.Add(plaque);
        }

        private void PreviousPage()
        {
            if (pageIndex <= 0) return;
            pageIndex--;
            Rebuild();
        }

        private void NextPage()
        {
            if ((pageIndex + 1) * EntriesPerPage >= entries.Count) return;
            pageIndex++;
            Rebuild();
        }

        private TextMeshPro BuildText(string name, Vector3 position, float fontSize, Vector2 size)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(ContentRoot, false);
            textObject.transform.localPosition = position;
            textObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TextMeshPro text = textObject.AddComponent<TextMeshPro>();
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.82f, 0.82f, 0.78f);
            text.rectTransform.sizeDelta = size;
#if UNITY_6000_0_OR_NEWER
            text.textWrappingMode = TextWrappingModes.Normal;
#else
            text.enableWordWrapping = true;
#endif
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private void BuildButton(string name, string labelText, Vector3 position, Vector3 scale, Action onClick, Color color)
        {
            GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = name;
            button.transform.SetParent(transform, false);
            button.transform.localPosition = position;
            button.transform.localScale = scale;
            button.GetComponent<Renderer>().material.color = color;
            button.AddComponent<ClickProxy>().OnClick = onClick;
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(button.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            labelObject.transform.localScale = new Vector3(1f / scale.x, 1f, 1f / scale.z);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = labelText;
            label.fontSize = 0.082f;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(scale.x - 0.06f, scale.z - 0.04f);
            label.overflowMode = TextOverflowModes.Ellipsis;
        }

        private void ClearEntries()
        {
            foreach (GameObject entryObject in entryObjects)
                if (entryObject != null) Destroy(entryObject);
            entryObjects.Clear();
        }

        private static string FormatResources(IReadOnlyList<string> resources)
        {
            if (resources == null || resources.Count == 0) return "无";
            var counts = new Dictionary<string, int>();
            foreach (string resource in resources)
            {
                if (string.IsNullOrWhiteSpace(resource)) continue;
                counts.TryGetValue(resource, out int count);
                counts[resource] = count + 1;
            }
            if (counts.Count == 0) return "无";
            var labels = new List<string>();
            foreach (KeyValuePair<string, int> pair in counts)
                labels.Add($"{pair.Key}×{pair.Value}");
            return string.Join("、", labels);
        }

        private readonly struct LedgerEntry
        {
            public int Year { get; }
            public string Title { get; }
            public string Detail { get; }
            public bool Completed { get; }

            public LedgerEntry(int year, string title, string detail, bool completed)
            {
                Year = year;
                Title = title ?? string.Empty;
                Detail = detail ?? string.Empty;
                Completed = completed;
            }
        }
    }
}
