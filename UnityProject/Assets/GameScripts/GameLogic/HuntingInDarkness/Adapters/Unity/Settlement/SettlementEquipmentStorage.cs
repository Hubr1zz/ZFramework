using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    /// <summary>装备仓库的字符串存档 Adapter，避免在 JSON 中直接持有 Unity 资产引用。</summary>
    public static class SettlementEquipmentStorage
    {
        public static int GetStoredEquipment(this SettlementInstance settlement, string itemId)
        {
            if (settlement == null || string.IsNullOrEmpty(itemId)) return 0;
            settlement.EquipmentStorage ??= new List<ResourceEntry>();
            return ResourceRules.Get(settlement.EquipmentStorage, itemId);
        }

        public static int GetStoredEquipment(this SettlementInstance settlement, ItemData item) => item == null ? 0 : settlement.GetStoredEquipment(item.ContentId);

        public static void AddStoredEquipment(this SettlementInstance settlement, string itemId, int amount)
        {
            if (settlement == null || string.IsNullOrEmpty(itemId) || amount <= 0) return;
            settlement.EquipmentStorage ??= new List<ResourceEntry>();
            ResourceRules.Add(settlement.EquipmentStorage, itemId, amount, () => new ResourceEntry());
        }

        public static void AddStoredEquipment(this SettlementInstance settlement, ItemData item, int amount)
        {
            if (item != null) settlement.AddStoredEquipment(item.ContentId, amount);
        }

        public static bool SpendStoredEquipment(this SettlementInstance settlement, string itemId, int amount)
        {
            if (settlement == null || string.IsNullOrEmpty(itemId) || amount <= 0) return false;
            settlement.EquipmentStorage ??= new List<ResourceEntry>();
            return ResourceRules.Spend(settlement.EquipmentStorage, itemId, amount, () => new ResourceEntry());
        }

        public static bool SpendStoredEquipment(this SettlementInstance settlement, ItemData item, int amount) => item != null && settlement.SpendStoredEquipment(item.ContentId, amount);
    }

    /// <summary>由组合根内容目录配置，负责稳定 ID、旧名称迁移和运行时 ItemData 恢复。</summary>
    public static class PlayableSettlementItemRegistry
    {
        public const int CurrentIdentitySchemaVersion = 1;

        public static IReadOnlyList<ItemData> Items => PlayableSettlementContentRuntime.Items;

        public static bool TryGet(string identifier, out ItemData item)
        {
            return PlayableSettlementContentRuntime.RegistryBundle.TryGetItem(identifier, out item);
        }

        public static string ResolveContentId(string identifier) => TryGet(identifier, out ItemData item) ? item.ContentId : identifier?.Trim() ?? string.Empty;

        public static string GetDisplayName(string identifier) => TryGet(identifier, out ItemData item) ? item.itemName : identifier?.Trim() ?? string.Empty;

        public static IReadOnlyCollection<string> CollectAliases(IReadOnlyCollection<string> itemIds, IReadOnlyCollection<string> legacyNames = null)
        {
            var aliases = new HashSet<string>(System.StringComparer.Ordinal);
            AddAliases(aliases, itemIds);
            AddAliases(aliases, legacyNames);
            return aliases;
        }

        public static IReadOnlyCollection<string> CollectKeywords(IReadOnlyCollection<string> equippedItemIds, IReadOnlyCollection<string> traits = null, IReadOnlyCollection<string> ailments = null)
        {
            var keywords = new HashSet<string>(System.StringComparer.Ordinal);
            PlayableTraitRegistry.AddKeywords(keywords, traits);
            AddKeywords(keywords, ailments);
            if (equippedItemIds == null) return keywords;
            foreach (string itemId in equippedItemIds)
            {
                if (!TryGet(itemId, out ItemData item) || item == null) continue;
                if (item.tags != null)
                    foreach (ItemTag tag in item.tags)
                        KeywordRules.TryAdd(keywords, tag.ToString());
                AddKeywords(keywords, item.keywords);
            }
            return keywords;
        }

        public static void Configure(IEnumerable<ItemData> items) => PlayableSettlementContentRuntime.ConfigureLegacyItems(items);

        public static bool MigratePersistentState(SettlementInstance settlement)
        {
            if (settlement == null) return false;
            if (settlement.ItemIdentitySchemaVersion > CurrentIdentitySchemaVersion) return false;
            settlement.Resources ??= new List<ResourceEntry>();
            settlement.EquipmentStorage ??= new List<ResourceEntry>();
            settlement.Hunters ??= new List<HunterInstance>();
            bool changed = MigrateEntries(settlement.Resources) | MigrateEntries(settlement.EquipmentStorage);
            foreach (HunterInstance hunter in settlement.Hunters)
                changed |= MigrateHunterEquipment(hunter);
            if (settlement.ItemIdentitySchemaVersion < CurrentIdentitySchemaVersion)
            {
                settlement.ItemIdentitySchemaVersion = CurrentIdentitySchemaVersion;
                changed = true;
            }
            return changed;
        }

        public static void RestoreEquipment(SettlementInstance settlement)
        {
            if (settlement == null) return;
            MigratePersistentState(settlement);
            if (settlement.Hunters == null) return;
            foreach (var hunter in settlement.Hunters)
            {
                if (hunter == null) continue;
                hunter.Equipment ??= new List<ItemInstance>();
                hunter.Collectibles ??= new List<ItemInstance>();
                hunter.Equipment.Clear();
                if (hunter.EquippedItemIds == null) continue;
                foreach (string itemId in hunter.EquippedItemIds)
                    if (TryGet(itemId, out ItemData item) && item.itemType != ItemType.Resource)
                        hunter.Equipment.Add(new ItemInstance(item));
            }
        }

        private static bool MigrateEntries(List<ResourceEntry> entries)
        {
            if (entries == null) return false;
            var amounts = new Dictionary<string, long>(System.StringComparer.Ordinal);
            var order = new List<string>();
            bool changed = false;
            foreach (ResourceEntry entry in entries)
            {
                if (entry == null) { changed = true; continue; }
                string original = entry.Key?.Trim() ?? string.Empty;
                if (original.Length == 0) { changed = true; continue; }
                string canonical = ResolveContentId(original);
                if (canonical != original || entry.Value < 0 || amounts.ContainsKey(canonical)) changed = true;
                if (!amounts.ContainsKey(canonical)) order.Add(canonical);
                amounts[canonical] = System.Math.Min(int.MaxValue, (amounts.TryGetValue(canonical, out long amount) ? amount : 0L) + System.Math.Max(0, entry.Value));
            }
            if (!changed) return false;
            entries.Clear();
            foreach (string key in order)
                entries.Add(new ResourceEntry { Key = key, Value = (int)amounts[key] });
            return true;
        }

        private static bool MigrateHunterEquipment(HunterInstance hunter)
        {
            if (hunter == null) return false;
            hunter.EquippedItemIds ??= new List<string>();
            hunter.EquippedItemNames ??= new List<string>();
            List<string> source = hunter.EquippedItemIds.Count > 0 ? hunter.EquippedItemIds : hunter.EquippedItemNames;
            var migrated = new List<string>(source.Count);
            bool changed = hunter.EquippedItemNames.Count > 0;
            foreach (string identifier in source)
            {
                string canonical = ResolveContentId(identifier);
                if (canonical.Length == 0) { changed = true; continue; }
                if (canonical != identifier) changed = true;
                migrated.Add(canonical);
            }
            if (changed || !ReferenceEquals(source, hunter.EquippedItemIds))
                hunter.EquippedItemIds = migrated;
            hunter.EquippedItemNames.Clear();
            return changed;
        }

        private static void AddKeywords(ISet<string> target, IReadOnlyCollection<string> source)
        {
            if (source == null) return;
            foreach (string keyword in source)
                KeywordRules.TryAdd(target, keyword);
        }

        private static void AddAliases(ISet<string> target, IReadOnlyCollection<string> source)
        {
            if (source == null) return;
            foreach (string identifier in source)
            {
                string original = identifier?.Trim() ?? string.Empty;
                if (original.Length == 0) continue;
                target.Add(original);
                if (!TryGet(original, out ItemData item)) continue;
                target.Add(item.ContentId);
                target.Add(item.itemName);
            }
        }
    }
}
