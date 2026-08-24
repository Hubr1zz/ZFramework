using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    /// <summary>统一发明稳定身份、旧别名迁移和玩家可读名称解析。</summary>
    public static class PlayableSettlementInventionRegistry
    {
        public const int CurrentIdentitySchemaVersion = 1;
        private const string timelinePrefix = "invention:";

        public static IReadOnlyList<InventionData> Inventions => PlayableSettlementContentRuntime.Inventions;

        public static bool TryGet(string identifier, out InventionData invention)
        {
            return PlayableSettlementContentRuntime.RegistryBundle.TryGetInvention(identifier, out invention);
        }

        public static string ResolveContentId(string identifier) => TryGet(identifier, out InventionData invention) ? invention.ContentId : identifier?.Trim() ?? string.Empty;
        public static string GetDisplayName(string identifier) => TryGet(identifier, out InventionData invention) ? invention.inventionName : identifier?.Trim() ?? string.Empty;

        public static void Configure(IEnumerable<InventionData> inventions) => PlayableSettlementContentRuntime.ConfigureLegacyInventions(inventions);

        public static bool MigratePersistentState(SettlementInstance settlement)
        {
            if (settlement == null || settlement.InventionIdentitySchemaVersion > CurrentIdentitySchemaVersion) return false;
            settlement.UnlockedInventions ??= new List<StringBoolEntry>();
            settlement.Timeline ??= new List<AnnalEntry>();
            bool changed = MigrateUnlockedFlags(settlement.UnlockedInventions) | MigrateTimeline(settlement.Timeline);
            if (settlement.InventionIdentitySchemaVersion < CurrentIdentitySchemaVersion)
            {
                settlement.InventionIdentitySchemaVersion = CurrentIdentitySchemaVersion;
                changed = true;
            }
            return changed;
        }

        private static bool MigrateUnlockedFlags(List<StringBoolEntry> entries)
        {
            var values = new Dictionary<string, bool>(StringComparer.Ordinal);
            var order = new List<string>();
            bool changed = false;
            foreach (StringBoolEntry entry in entries)
            {
                if (entry == null) { changed = true; continue; }
                string original = entry.Key?.Trim() ?? string.Empty;
                if (original.Length == 0) { changed = true; continue; }
                string canonical = ResolveContentId(original);
                if (canonical != original || values.ContainsKey(canonical)) changed = true;
                if (!values.ContainsKey(canonical)) order.Add(canonical);
                values[canonical] = values.TryGetValue(canonical, out bool value) ? value || entry.Value : entry.Value;
            }
            if (!changed) return false;
            entries.Clear();
            foreach (string key in order)
                entries.Add(new StringBoolEntry { Key = key, Value = values[key] });
            return true;
        }

        private static bool MigrateTimeline(List<AnnalEntry> entries)
        {
            bool changed = false;
            var migratedIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < entries.Count; index++)
            {
                AnnalEntry entry = entries[index];
                if (entry == null || entry.EntryType != TimelineEntryType.Invention) continue;
                string eventId = entry.EventId?.Trim() ?? string.Empty;
                string identifier = eventId.StartsWith(timelinePrefix, StringComparison.Ordinal) ? eventId.Substring(timelinePrefix.Length) : eventId;
                if (!TryGet(identifier, out InventionData invention)) continue;
                string canonicalEventId = timelinePrefix + invention.ContentId;
                if (!migratedIds.Add(canonicalEventId))
                {
                    entries.RemoveAt(index--);
                    changed = true;
                    continue;
                }
                if (entry.EventId != canonicalEventId)
                {
                    entry.EventId = canonicalEventId;
                    changed = true;
                }
                if (entry.EventName != invention.inventionName)
                {
                    entry.EventName = invention.inventionName;
                    changed = true;
                }
            }
            return changed;
        }

    }
}
