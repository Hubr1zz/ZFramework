using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;

namespace HuntingInDarkness.Settlement
{
    /// <summary>营地内容的不可变稳定身份索引；Plan 与旧兼容配置都只发布一个 Bundle 引用。</summary>
    internal sealed class PlayableSettlementRegistryBundle
    {
        private readonly Dictionary<string, ItemData> itemByIdentifier;
        private readonly Dictionary<string, InventionData> inventionByIdentifier;
        private readonly Dictionary<string, EventData> eventByIdentifier;
        private readonly Dictionary<string, EventData> eventByCanonicalId;

        private PlayableSettlementRegistryBundle(List<ItemData> items, Dictionary<string, ItemData> itemByIdentifier, List<InventionData> inventions, Dictionary<string, InventionData> inventionByIdentifier, List<EventData> events, Dictionary<string, EventData> eventByIdentifier, Dictionary<string, EventData> eventByCanonicalId, bool eventsConfigured, bool eventsValid, string eventDiagnostic)
        {
            Items = items.AsReadOnly();
            Inventions = inventions.AsReadOnly();
            Events = events.AsReadOnly();
            this.itemByIdentifier = itemByIdentifier;
            this.inventionByIdentifier = inventionByIdentifier;
            this.eventByIdentifier = eventByIdentifier;
            this.eventByCanonicalId = eventByCanonicalId;
            EventsConfigured = eventsConfigured;
            EventsValid = eventsValid;
            EventDiagnostic = eventDiagnostic ?? string.Empty;
        }

        public IReadOnlyList<ItemData> Items { get; }
        public IReadOnlyList<InventionData> Inventions { get; }
        public IReadOnlyList<EventData> Events { get; }
        public bool EventsConfigured { get; }
        public bool EventsValid { get; }
        public string EventDiagnostic { get; }

        public bool TryGetItem(string identifier, out ItemData item) => itemByIdentifier.TryGetValue(Normalize(identifier), out item);
        public bool TryGetInvention(string identifier, out InventionData invention) => inventionByIdentifier.TryGetValue(Normalize(identifier), out invention);
        public bool TryGetEvent(string identifier, out EventData gameEvent) => eventByIdentifier.TryGetValue(Normalize(identifier), out gameEvent);
        public bool TryGetCanonicalEvent(string identifier, out EventData gameEvent) => eventByCanonicalId.TryGetValue(Normalize(identifier), out gameEvent);

        public static PlayableSettlementRegistryBundle CreateLegacy(IEnumerable<ItemData> items, IEnumerable<InventionData> inventions, IEnumerable<EventData> events, bool eventsConfigured)
        {
            BuildItems(items, out List<ItemData> registeredItems, out Dictionary<string, ItemData> itemIndex);
            BuildInventions(inventions, out List<InventionData> registeredInventions, out Dictionary<string, InventionData> inventionIndex);
            BuildEvents(events, eventsConfigured, out List<EventData> registeredEvents, out Dictionary<string, EventData> eventIndex, out Dictionary<string, EventData> canonicalEventIndex, out bool eventsValid, out string diagnostic);
            return new PlayableSettlementRegistryBundle(registeredItems, itemIndex, registeredInventions, inventionIndex, registeredEvents, eventIndex, canonicalEventIndex, eventsConfigured, eventsValid, diagnostic);
        }

        public static bool TryCreate(IReadOnlyList<ItemData> items, IReadOnlyList<InventionData> inventions, IReadOnlyList<EventData> events, out PlayableSettlementRegistryBundle bundle, out string reason)
        {
            bundle = CreateLegacy(items, inventions, events, true);
            if (bundle.Items.Count != CountNonNull(items))
            {
                reason = "营地物品稳定身份索引不完整。";
                bundle = null;
                return false;
            }
            if (bundle.Inventions.Count != CountNonNull(inventions))
            {
                reason = "营地发明稳定身份索引不完整。";
                bundle = null;
                return false;
            }
            if (!bundle.EventsValid || bundle.Events.Count != CountNonNull(events))
            {
                reason = string.IsNullOrWhiteSpace(bundle.EventDiagnostic) ? "营地事件稳定身份索引不完整。" : bundle.EventDiagnostic;
                bundle = null;
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private static void BuildItems(IEnumerable<ItemData> source, out List<ItemData> items, out Dictionary<string, ItemData> index)
        {
            items = new List<ItemData>();
            index = new Dictionary<string, ItemData>(StringComparer.Ordinal);
            var candidates = new List<ItemData>();
            var owners = new Dictionary<string, HashSet<ItemData>>(StringComparer.Ordinal);
            var occurrences = new Dictionary<ItemData, int>();
            foreach (ItemData item in source ?? Array.Empty<ItemData>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ContentId) || string.IsNullOrWhiteSpace(item.itemName)) continue;
                candidates.Add(item);
                occurrences[item] = occurrences.TryGetValue(item, out int count) ? count + 1 : 1;
                AddOwner(owners, item.ContentId, item);
                AddOwner(owners, item.itemName, item);
            }
            foreach (ItemData item in candidates)
            {
                if (occurrences[item] != 1 || !IsUniqueOwner(owners, item.ContentId, item) || !IsUniqueOwner(owners, item.itemName, item)) continue;
                index.Add(item.ContentId, item);
                if (item.itemName != item.ContentId) index.Add(item.itemName, item);
                items.Add(item);
            }
        }

        private static void BuildInventions(IEnumerable<InventionData> source, out List<InventionData> inventions, out Dictionary<string, InventionData> index)
        {
            inventions = new List<InventionData>();
            index = new Dictionary<string, InventionData>(StringComparer.Ordinal);
            var candidates = new List<InventionData>();
            var stableOwners = new Dictionary<string, HashSet<InventionData>>(StringComparer.Ordinal);
            var aliasOwners = new Dictionary<string, HashSet<InventionData>>(StringComparer.Ordinal);
            var effectOwners = new Dictionary<string, HashSet<InventionData>>(StringComparer.Ordinal);
            var invalid = new HashSet<InventionData>();
            var occurrences = new Dictionary<InventionData, int>();
            foreach (InventionData invention in source ?? Array.Empty<InventionData>())
            {
                if (invention == null || !invention.HasExplicitContentId || string.IsNullOrWhiteSpace(invention.inventionName)) continue;
                candidates.Add(invention);
                occurrences[invention] = occurrences.TryGetValue(invention, out int count) ? count + 1 : 1;
                AddOwner(stableOwners, invention.ContentId, invention);
                var localEffects = new HashSet<string>(StringComparer.Ordinal);
                foreach (InventionActiveEffect effect in invention.activeEffects ?? new List<InventionActiveEffect>())
                {
                    string effectId = Normalize(effect?.effectId);
                    if (effect == null || effectId.Length == 0 || string.IsNullOrWhiteSpace(effect.eventId) || effect.maxUsesPerYear < 0 || !localEffects.Add(effectId))
                    {
                        invalid.Add(invention);
                        continue;
                    }
                    AddOwner(effectOwners, effectId, invention);
                }
            }
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (InventionData invention in candidates)
            {
                if (occurrences[invention] != 1 || invalid.Contains(invention) || !IsUniqueOwner(stableOwners, invention.ContentId, invention) || HasSharedEffect(invention, effectOwners)) continue;
                AddAlias(index, invention.ContentId, invention);
                stableIds.Add(Normalize(invention.ContentId));
                inventions.Add(invention);
            }
            foreach (InventionData invention in inventions)
            {
                AddOwner(aliasOwners, invention.inventionName, invention);
                AddOwner(aliasOwners, invention.name, invention);
            }
            foreach (InventionData invention in inventions)
            {
                AddLegacyAlias(index, aliasOwners, stableIds, invention.inventionName, invention);
                AddLegacyAlias(index, aliasOwners, stableIds, invention.name, invention);
            }
        }

        private static void BuildEvents(IEnumerable<EventData> source, bool configured, out List<EventData> events, out Dictionary<string, EventData> index, out Dictionary<string, EventData> canonicalIndex, out bool valid, out string diagnostic)
        {
            events = new List<EventData>();
            index = new Dictionary<string, EventData>(StringComparer.Ordinal);
            canonicalIndex = new Dictionary<string, EventData>(StringComparer.Ordinal);
            valid = configured;
            diagnostic = string.Empty;
            if (!configured) return;
            var candidates = new List<EventData>();
            var owners = new Dictionary<string, HashSet<EventData>>(StringComparer.Ordinal);
            var canonicalOwners = new Dictionary<string, HashSet<EventData>>(StringComparer.Ordinal);
            foreach (EventData gameEvent in source ?? Array.Empty<EventData>())
            {
                if (gameEvent == null) continue;
                events.Add(gameEvent);
                if (!gameEvent.HasExplicitContentId)
                {
                    valid = false;
                    diagnostic = $"营地事件缺少显式稳定 ContentId：{gameEvent.name}";
                    continue;
                }
                candidates.Add(gameEvent);
                AddOwner(owners, gameEvent.ContentId, gameEvent);
                AddOwner(owners, gameEvent.name, gameEvent);
                AddOwner(canonicalOwners, gameEvent.ContentId, gameEvent);
            }
            foreach (KeyValuePair<string, HashSet<EventData>> pair in owners)
                if (pair.Value.Count > 1)
                {
                    valid = false;
                    diagnostic = $"事件稳定身份或资产名别名冲突：{pair.Key}";
                    break;
                }
            foreach (EventData gameEvent in candidates)
            {
                if (IsUniqueOwner(owners, gameEvent.ContentId, gameEvent)) AddAlias(index, gameEvent.ContentId, gameEvent);
                if (IsUniqueOwner(owners, gameEvent.name, gameEvent)) AddAlias(index, gameEvent.name, gameEvent);
                if (IsUniqueOwner(canonicalOwners, gameEvent.ContentId, gameEvent)) AddAlias(canonicalIndex, gameEvent.ContentId, gameEvent);
            }
        }

        private static bool HasSharedEffect(InventionData invention, IReadOnlyDictionary<string, HashSet<InventionData>> owners)
        {
            foreach (InventionActiveEffect effect in invention.activeEffects ?? new List<InventionActiveEffect>())
                if (owners.TryGetValue(Normalize(effect?.effectId), out HashSet<InventionData> values) && values.Count > 1)
                    return true;
            return false;
        }

        private static int CountNonNull<T>(IReadOnlyList<T> values) where T : class
        {
            int count = 0;
            foreach (T value in values ?? Array.Empty<T>())
                if (value != null)
                    count++;
            return count;
        }

        private static void AddOwner<T>(IDictionary<string, HashSet<T>> owners, string identifier, T owner) where T : class
        {
            string key = Normalize(identifier);
            if (key.Length == 0) return;
            if (!owners.TryGetValue(key, out HashSet<T> values))
            {
                values = new HashSet<T>();
                owners.Add(key, values);
            }
            values.Add(owner);
        }

        private static bool IsUniqueOwner<T>(IReadOnlyDictionary<string, HashSet<T>> owners, string identifier, T owner) where T : class
        {
            string key = Normalize(identifier);
            return key.Length > 0 && owners.TryGetValue(key, out HashSet<T> values) && values.Count == 1 && values.Contains(owner);
        }

        private static void AddAlias<T>(IDictionary<string, T> index, string identifier, T value) where T : class
        {
            string key = Normalize(identifier);
            if (key.Length > 0 && !index.ContainsKey(key)) index.Add(key, value);
        }

        private static void AddLegacyAlias<T>(IDictionary<string, T> index, IReadOnlyDictionary<string, HashSet<T>> owners, ISet<string> stableIds, string identifier, T value) where T : class
        {
            string key = Normalize(identifier);
            if (stableIds.Contains(key) || !IsUniqueOwner(owners, key, value)) return;
            AddAlias(index, key, value);
        }

        private static string Normalize(string value) => value?.Trim() ?? string.Empty;
    }
}
