using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.ViewLayer.Tabletop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>只读营地年鉴。把狩猎记录与时间线统一投影为可滚动的屏幕条目。</summary>
    public sealed class CampLedgerPanel3D : MonoBehaviour
    {
        [Header("Screen UI 引用")]
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private ScrollRect entriesScrollRect;
        [SerializeField] private RectTransform entryContent;
        [SerializeField] private ScreenLedgerEntry entryTemplate;
        [SerializeField] private Button closeButton;

        private readonly List<ScreenLedgerEntry> pooledEntries = new();
        private readonly List<LedgerEntry> entries = new();
        private SettlementInstance settlement;
        private string seasonDisplayName = string.Empty;
        private IDisposable modalLease;
        private bool referencesValidated;

        public bool IsOpen => modalRoot != null && modalRoot.activeSelf;

        public static CampLedgerPanel3D Create(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            CampLedgerPanel3D prefab = TabletopPresentationAssets.LedgerPanelPrefab;
            CampLedgerPanel3D panel = Instantiate(prefab, parent, false);
            panel.name = prefab.name;
            panel.gameObject.SetActive(true);
            panel.modalRoot.SetActive(false);
            return panel;
        }

        public void EnsureBuilt()
        {
            ValidateReferences();
        }

        public void Open(SettlementInstance settlementData, Vector3 worldPosition)
        {
            if (settlementData == null) return;
            EnsureBuilt();
            settlement = settlementData;
            Rebuild();
            modalRoot.SetActive(true);
            modalLease?.Dispose();
            modalLease = ScreenModalInputGate.Acquire(this);
            Canvas.ForceUpdateCanvases();
            entriesScrollRect.verticalNormalizedPosition = 1f;
        }

        public void SetCalendarSeason(SeasonDefinition season)
        {
            seasonDisplayName = season?.DisplayName?.Trim() ?? string.Empty;
            if (IsOpen && settlement != null) Rebuild();
        }

        public void RefreshVisible()
        {
            if (!IsOpen || settlement == null) return;
            Rebuild();
        }

        public void Hide()
        {
            modalRoot?.SetActive(false);
            foreach (ScreenLedgerEntry entry in pooledEntries)
                if (entry != null)
                    entry.gameObject.SetActive(false);
            modalLease?.Dispose();
            modalLease = null;
        }

        private void Awake()
        {
            ValidateReferences();
            closeButton.onClick.AddListener(Hide);
            modalRoot.SetActive(false);
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Hide();
        }

        private void OnDestroy()
        {
            closeButton?.onClick.RemoveListener(Hide);
            modalLease?.Dispose();
            modalLease = null;
        }

        private void OnDisable() => Hide();

        private void ValidateReferences()
        {
            if (referencesValidated) return;
            if (rootCanvas == null || modalRoot == null || titleText == null || summaryText == null || entriesScrollRect == null || entryContent == null || entryTemplate == null || closeButton == null)
                throw new MissingReferenceException($"[{nameof(CampLedgerPanel3D)}] 年鉴屏幕面板引用未完整绑定。\n对象：{name}");
            referencesValidated = true;
        }

        private void Rebuild()
        {
            ClearEntries();
            BuildEntries();
            string currentSeason = string.IsNullOrWhiteSpace(seasonDisplayName) ? $"第 {settlement.CurrentSeasonIndex + 1} 季" : seasonDisplayName;
            titleText.text = $"无火营地年鉴 · 第 {settlement.CurrentYear} 年 · {currentSeason}";
            int lastHuntYear = settlement.HuntHistory != null && settlement.HuntHistory.Count > 0 ? settlement.HuntHistory[settlement.HuntHistory.Count - 1]?.Year ?? 0 : 0;
            summaryText.text = $"上次远征 {lastHuntYear} → 当前 {settlement.CurrentYear}年·{currentSeason}　总远征 {settlement.HuntHistory?.Count ?? 0}　时间线 {settlement.Timeline?.Count ?? 0}　存活猎人 {settlement.GetAliveHunters().Count}";
            for (int index = 0; index < entries.Count; index++)
            {
                LedgerEntry entry = entries[index];
                ScreenLedgerEntry view = GetEntry(index);
                view.Configure($"第 {entry.Year} 年 · {entry.Title}", entry.Detail, entry.Completed);
            }
            Canvas.ForceUpdateCanvases();
            entriesScrollRect.verticalNormalizedPosition = 1f;
        }

        private void BuildEntries()
        {
            entries.Clear();
            var linkedMemoryIds = new HashSet<string>(StringComparer.Ordinal);
            if (settlement.Timeline != null)
            {
                foreach (AnnalEntry entry in settlement.Timeline)
                {
                    if (entry == null) continue;
                    string eventName = string.IsNullOrWhiteSpace(entry.EventName) ? entry.EventId : entry.EventName;
                    string state = entry.IsCompleted ? "已发生" : "将发生";
                    string category = entry.EntryType == TimelineEntryType.Invention ? "发明 · 已掌握" : $"时间线 · {state}";
                    EventResolutionMemory memory = settlement.EventMemories?.Find(candidate => candidate != null && string.Equals(candidate.MemoryId, entry.ResolutionMemoryId, StringComparison.Ordinal));
                    if (memory != null)
                    {
                        linkedMemoryIds.Add(memory.MemoryId);
                        category += $"\n{CampLedgerPresentation.FormatEventMemory(memory)}";
                    }
                    entries.Add(new LedgerEntry(entry.Year, entry.IsMilestone ? $"★ {eventName}" : eventName, category, entry.IsCompleted, 0, entries.Count));
                }
            }
            if (settlement.EventMemories != null)
                foreach (EventResolutionMemory memory in settlement.EventMemories)
                {
                    if (memory == null || linkedMemoryIds.Contains(memory.MemoryId)) continue;
                    entries.Add(new LedgerEntry(memory.Year, string.IsNullOrWhiteSpace(memory.EventName) ? memory.EventId : memory.EventName, $"事件余波\n{CampLedgerPresentation.FormatEventMemory(memory)}", true, 1, entries.Count));
                }
            if (settlement.HuntHistory != null)
            {
                for (int huntIndex = 0; huntIndex < settlement.HuntHistory.Count; huntIndex++)
                {
                    HuntRecord record = settlement.HuntHistory[huntIndex];
                    if (record == null) continue;
                    int groupOrder = 1000 + huntIndex;
                    string outcome = record.BossDefeated ? "讨伐成功" : "从黑暗中归来";
                    entries.Add(new LedgerEntry(record.Year, outcome, $"狩猎 · 出发 {record.HuntersDeployed} · 损失 {record.HuntersLost} · 带回 {CampLedgerPresentation.FormatLoot(record.CollectedItems, record.CollectedResources)}", true, groupOrder, 0));
                    for (int memoryIndex = 0; memoryIndex < (record.Memories?.Count ?? 0); memoryIndex++)
                    {
                        EventResolutionMemory memory = record.Memories[memoryIndex];
                        if (memory != null)
                            entries.Add(new LedgerEntry(record.Year, $"└ {memory.EventName}", $"远征事件 · {CampLedgerPresentation.FormatEventMemory(memory)}", true, groupOrder, memoryIndex + 1));
                    }
                }
            }
            entries.Sort((left, right) =>
            {
                int yearComparison = right.Year.CompareTo(left.Year);
                if (yearComparison != 0) return yearComparison;
                int groupComparison = left.GroupOrder.CompareTo(right.GroupOrder);
                return groupComparison != 0 ? groupComparison : left.EntryOrder.CompareTo(right.EntryOrder);
            });
        }

        private ScreenLedgerEntry GetEntry(int index)
        {
            while (pooledEntries.Count <= index)
            {
                ScreenLedgerEntry entry = Instantiate(entryTemplate, entryContent);
                entry.name = $"ScreenLedgerEntry_{pooledEntries.Count}";
                pooledEntries.Add(entry);
            }
            return pooledEntries[index];
        }

        private void ClearEntries()
        {
            foreach (ScreenLedgerEntry entry in pooledEntries)
                if (entry != null)
                    entry.gameObject.SetActive(false);
        }

        private readonly struct LedgerEntry
        {
            public int Year { get; }
            public string Title { get; }
            public string Detail { get; }
            public bool Completed { get; }
            public int GroupOrder { get; }
            public int EntryOrder { get; }

            public LedgerEntry(int year, string title, string detail, bool completed, int groupOrder, int entryOrder)
            {
                Year = year;
                Title = title ?? string.Empty;
                Detail = detail ?? string.Empty;
                Completed = completed;
                GroupOrder = groupOrder;
                EntryOrder = entryOrder;
            }
        }
    }
}
