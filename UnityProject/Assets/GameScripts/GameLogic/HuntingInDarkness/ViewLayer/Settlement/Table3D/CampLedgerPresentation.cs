using System.Collections.Generic;
using HuntingInDarkness.Settlement;

namespace UI
{
    /// <summary>把年鉴持久化标识投影成玩家可读文本，不持有场景对象。</summary>
    public static class CampLedgerPresentation
    {
        public static string FormatResources(IReadOnlyList<string> resources)
        {
            if (resources == null || resources.Count == 0) return "无";
            var counts = new Dictionary<string, int>();
            foreach (string resource in resources)
            {
                string displayName = PlayableSettlementItemRegistry.GetDisplayName(resource);
                if (string.IsNullOrWhiteSpace(displayName)) continue;
                counts.TryGetValue(displayName, out int count);
                counts[displayName] = count + 1;
            }
            if (counts.Count == 0) return "无";
            var labels = new List<string>();
            foreach (KeyValuePair<string, int> pair in counts)
                labels.Add($"{pair.Key}×{pair.Value}");
            return string.Join("、", labels);
        }
    }
}
