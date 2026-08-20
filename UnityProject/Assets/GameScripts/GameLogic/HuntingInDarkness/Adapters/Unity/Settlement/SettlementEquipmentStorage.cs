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
        public static int GetStoredEquipment(this SettlementInstance settlement, string itemName)
        {
            if (settlement == null || string.IsNullOrEmpty(itemName)) return 0;
            settlement.EquipmentStorage ??= new List<ResourceEntry>();
            return ResourceRules.Get(settlement.EquipmentStorage, itemName);
        }

        public static void AddStoredEquipment(this SettlementInstance settlement, string itemName, int amount)
        {
            if (settlement == null || string.IsNullOrEmpty(itemName) || amount <= 0) return;
            settlement.EquipmentStorage ??= new List<ResourceEntry>();
            ResourceRules.Add(settlement.EquipmentStorage, itemName, amount, () => new ResourceEntry());
        }

        public static bool SpendStoredEquipment(this SettlementInstance settlement, string itemName, int amount)
        {
            if (settlement == null || string.IsNullOrEmpty(itemName) || amount <= 0) return false;
            settlement.EquipmentStorage ??= new List<ResourceEntry>();
            return ResourceRules.Spend(settlement.EquipmentStorage, itemName, amount, () => new ResourceEntry());
        }
    }

    /// <summary>由组合根内容目录配置，负责把存档中的物品名恢复为 ItemData。</summary>
    public static class PlayableSettlementItemRegistry
    {
        private static readonly Dictionary<string, ItemData> itemByName = new();
        private static readonly List<ItemData> registeredItems = new();

        public static IReadOnlyList<ItemData> Items => registeredItems;

        public static bool TryGet(string itemName, out ItemData item)
        {
            return itemByName.TryGetValue(itemName ?? string.Empty, out item);
        }

        public static IReadOnlyCollection<string> CollectKeywords(IReadOnlyCollection<string> equippedItemNames, IReadOnlyCollection<string> traits = null, IReadOnlyCollection<string> ailments = null)
        {
            var keywords = new HashSet<string>(System.StringComparer.Ordinal);
            AddKeywords(keywords, traits);
            AddKeywords(keywords, ailments);
            if (equippedItemNames == null) return keywords;
            foreach (string itemName in equippedItemNames)
            {
                if (!TryGet(itemName, out ItemData item) || item == null) continue;
                if (item.tags != null)
                    foreach (ItemTag tag in item.tags)
                        KeywordRules.TryAdd(keywords, tag.ToString());
                AddKeywords(keywords, item.keywords);
            }
            return keywords;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            itemByName.Clear();
            registeredItems.Clear();
        }

        public static void Configure(IEnumerable<ItemData> items)
        {
            itemByName.Clear();
            registeredItems.Clear();
            if (items == null) return;

            foreach (var item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.itemName) || itemByName.ContainsKey(item.itemName)) continue;
                itemByName.Add(item.itemName, item);
                registeredItems.Add(item);
            }
        }

        public static void RestoreEquipment(SettlementInstance settlement)
        {
            if (settlement == null) return;
            settlement.EquipmentStorage ??= new List<ResourceEntry>();
            foreach (var hunter in settlement.Hunters)
            {
                if (hunter == null) continue;
                hunter.EquippedItemNames ??= new List<string>();
                hunter.Equipment ??= new List<ItemInstance>();
                hunter.Collectibles ??= new List<ItemInstance>();
                hunter.Equipment.Clear();
                foreach (string itemName in hunter.EquippedItemNames)
                    if (itemByName.TryGetValue(itemName, out var item) && item.itemType != ItemType.Resource)
                        hunter.Equipment.Add(new ItemInstance(item));
            }
        }

        private static void AddKeywords(ISet<string> target, IReadOnlyCollection<string> source)
        {
            if (source == null) return;
            foreach (string keyword in source)
                KeywordRules.TryAdd(target, keyword);
        }
    }
}
