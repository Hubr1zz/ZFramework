using System;
using HuntingInDarkness.Data;

namespace HuntingInDarkness.Settlement
{
    /// <summary>把已提交的长期营地事实投影到持久化年鉴，避免 View 参与记录写入。</summary>
    public static class SettlementTimelineJournal
    {
        private const string inventionPrefix = "invention:";

        public static bool RecordInvention(SettlementInstance settlement, string inventionId, string displayName)
        {
            if (settlement == null) return false;
            string normalizedId = inventionId?.Trim() ?? string.Empty;
            if (normalizedId.Length == 0) return false;
            settlement.Timeline ??= new System.Collections.Generic.List<AnnalEntry>();
            string entryId = inventionPrefix + normalizedId;
            if (settlement.Timeline.Exists(entry => entry != null && entry.EntryType == TimelineEntryType.Invention && string.Equals(entry.EventId, entryId, StringComparison.Ordinal))) return false;

            settlement.Timeline.Add(new AnnalEntry
            {
                Year = settlement.CurrentYear,
                EventId = entryId,
                EventName = string.IsNullOrWhiteSpace(displayName) ? normalizedId : displayName.Trim(),
                IsCompleted = true,
                EntryType = TimelineEntryType.Invention
            });
            return true;
        }
    }
}
