using System;
using System.Collections.Generic;
using System.Linq;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.ViewLayer.Hunt
{
    /// <summary>把狩猎携带物投影成紧凑、稳定且可供多个 3D 桌面复用的玩家文本。</summary>
    public readonly struct HuntCollectiblePresentation
    {
        private HuntCollectiblePresentation(int totalCount, int distinctCount, string summary)
        {
            TotalCount = totalCount;
            DistinctCount = distinctCount;
            Summary = summary;
        }

        public int TotalCount { get; }
        public int DistinctCount { get; }
        public string Summary { get; }

        public static HuntCollectiblePresentation Create(IEnumerable<ItemInstance> collectibles, int maximumLabels = 2)
        {
            if (collectibles == null) return new HuntCollectiblePresentation(0, 0, "无");
            var counts = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (ItemInstance collectible in collectibles)
            {
                if (collectible?.Data == null || collectible.Count <= 0) continue;
                string identifier = collectible.Data.ContentId;
                if (string.IsNullOrWhiteSpace(identifier)) continue;
                string displayName = collectible.Data.itemName;
                if (string.IsNullOrWhiteSpace(displayName)) displayName = PlayableSettlementItemRegistry.GetDisplayName(identifier);
                if (string.IsNullOrWhiteSpace(displayName)) displayName = "未知素材";
                counts.TryGetValue(identifier, out Entry entry);
                counts[identifier] = new Entry(displayName, (int)Math.Min(int.MaxValue, (long)entry.Count + collectible.Count));
            }
            if (counts.Count == 0) return new HuntCollectiblePresentation(0, 0, "无");

            int totalCount = (int)Math.Min(int.MaxValue, counts.Values.Sum(entry => (long)entry.Count));
            int visibleCount = Math.Max(0, maximumLabels);
            List<Entry> ordered = counts.Values.OrderBy(entry => entry.DisplayName, StringComparer.Ordinal).ThenBy(entry => entry.Count).ToList();
            var labels = ordered.Take(visibleCount).Select(entry => $"{entry.DisplayName}×{entry.Count}").ToList();
            if (ordered.Count > visibleCount) labels.Add($"另 {ordered.Count - visibleCount} 类");
            string summary = labels.Count > 0 ? string.Join("、", labels) : $"{ordered.Count} 类素材";
            return new HuntCollectiblePresentation(totalCount, ordered.Count, summary);
        }

        private readonly struct Entry
        {
            public Entry(string displayName, int count)
            {
                DisplayName = displayName;
                Count = count;
            }

            public string DisplayName { get; }
            public int Count { get; }
        }
    }
}
