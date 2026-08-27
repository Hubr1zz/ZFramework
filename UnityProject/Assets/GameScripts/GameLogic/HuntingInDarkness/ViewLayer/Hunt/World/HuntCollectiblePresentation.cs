using System;
using System.Collections.Generic;
using System.Linq;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.ViewLayer.Hunt
{
    public readonly struct HuntCollectibleStackPresentation
    {
        public HuntCollectibleStackPresentation(string contentId, string displayName, int count)
        {
            ContentId = contentId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Count = Math.Max(0, count);
        }

        public string ContentId { get; }
        public string DisplayName { get; }
        public int Count { get; }
    }

    /// <summary>把狩猎携带物投影成紧凑、稳定且可供多个 3D 桌面复用的玩家文本。</summary>
    public readonly struct HuntCollectiblePresentation
    {
        private HuntCollectiblePresentation(int totalCount, string summary, IReadOnlyList<HuntCollectibleStackPresentation> stacks)
        {
            TotalCount = totalCount;
            Summary = summary;
            Stacks = stacks ?? Array.Empty<HuntCollectibleStackPresentation>();
        }

        public int TotalCount { get; }
        public int DistinctCount => Stacks.Count;
        public string Summary { get; }
        public IReadOnlyList<HuntCollectibleStackPresentation> Stacks { get; }

        public static HuntCollectiblePresentation Create(IEnumerable<ItemInstance> collectibles, int maximumLabels = 2)
        {
            if (collectibles == null) return new HuntCollectiblePresentation(0, "无", Array.Empty<HuntCollectibleStackPresentation>());
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
                counts[identifier] = new Entry(identifier, displayName, (int)Math.Min(int.MaxValue, (long)entry.Count + collectible.Count));
            }
            if (counts.Count == 0) return new HuntCollectiblePresentation(0, "无", Array.Empty<HuntCollectibleStackPresentation>());

            int totalCount = (int)Math.Min(int.MaxValue, counts.Values.Sum(entry => (long)entry.Count));
            int visibleCount = Math.Max(0, maximumLabels);
            List<Entry> ordered = counts.Values.OrderBy(entry => entry.ContentId, StringComparer.Ordinal).ToList();
            var labels = ordered.Take(visibleCount).Select(entry => $"{entry.DisplayName}×{entry.Count}").ToList();
            if (ordered.Count > visibleCount) labels.Add($"另 {ordered.Count - visibleCount} 类");
            string summary = labels.Count > 0 ? string.Join("、", labels) : $"{ordered.Count} 类素材";
            List<HuntCollectibleStackPresentation> stacks = ordered.Select(entry => new HuntCollectibleStackPresentation(entry.ContentId, entry.DisplayName, entry.Count)).ToList();
            return new HuntCollectiblePresentation(totalCount, summary, stacks);
        }

        private readonly struct Entry
        {
            public Entry(string contentId, string displayName, int count)
            {
                ContentId = contentId;
                DisplayName = displayName;
                Count = count;
            }

            public string ContentId { get; }
            public string DisplayName { get; }
            public int Count { get; }
        }
    }
}
