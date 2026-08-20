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

        private static readonly Dictionary<string, InventionData> inventionById = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, InventionData> inventionByAlias = new(StringComparer.Ordinal);
        private static readonly List<InventionData> registeredInventions = new();

        public static IReadOnlyList<InventionData> Inventions => registeredInventions;

        public static bool TryGet(string identifier, out InventionData invention)
        {
            string key = identifier?.Trim() ?? string.Empty;
            return inventionById.TryGetValue(key, out invention) || inventionByAlias.TryGetValue(key, out invention);
        }

        public static string ResolveContentId(string identifier) => TryGet(identifier, out InventionData invention) ? invention.ContentId : identifier?.Trim() ?? string.Empty;
        public static string GetDisplayName(string identifier) => TryGet(identifier, out InventionData invention) ? invention.inventionName : identifier?.Trim() ?? string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState() => Configure(null);

        public static void Configure(IEnumerable<InventionData> inventions)
        {
            inventionById.Clear();
            inventionByAlias.Clear();
            registeredInventions.Clear();
            if (inventions == null) return;

            var candidates = new List<InventionData>();
            var owners = new Dictionary<string, HashSet<InventionData>>(StringComparer.Ordinal);
            foreach (InventionData invention in inventions)
            {
                if (invention == null || !invention.HasExplicitContentId || string.IsNullOrWhiteSpace(invention.ContentId) || string.IsNullOrWhiteSpace(invention.inventionName)) continue;
                candidates.Add(invention);
                AddOwner(owners, invention.ContentId, invention);
                AddOwner(owners, invention.inventionName, invention);
                AddOwner(owners, invention.name, invention);
            }

            foreach (InventionData invention in candidates)
            {
                bool assetAliasIsValid = string.IsNullOrWhiteSpace(invention.name) || IsUnambiguous(owners, invention.name, invention);
                if (!IsUnambiguous(owners, invention.ContentId, invention) || !IsUnambiguous(owners, invention.inventionName, invention) || !assetAliasIsValid) continue;
                inventionById.Add(invention.ContentId, invention);
                AddAlias(invention.inventionName, invention);
                AddAlias(invention.name, invention);
                registeredInventions.Add(invention);
            }
        }

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

        private static void AddOwner(IDictionary<string, HashSet<InventionData>> owners, string identifier, InventionData invention)
        {
            string key = identifier?.Trim() ?? string.Empty;
            if (key.Length == 0) return;
            if (!owners.TryGetValue(key, out HashSet<InventionData> values))
            {
                values = new HashSet<InventionData>();
                owners.Add(key, values);
            }
            values.Add(invention);
        }

        private static bool IsUnambiguous(IReadOnlyDictionary<string, HashSet<InventionData>> owners, string identifier, InventionData invention)
        {
            string key = identifier?.Trim() ?? string.Empty;
            return key.Length > 0 && owners.TryGetValue(key, out HashSet<InventionData> values) && values.Count == 1 && values.Contains(invention);
        }

        private static void AddAlias(string identifier, InventionData invention)
        {
            string key = identifier?.Trim() ?? string.Empty;
            if (key.Length > 0 && key != invention.ContentId && !inventionByAlias.ContainsKey(key))
                inventionByAlias.Add(key, invention);
        }
    }
}
