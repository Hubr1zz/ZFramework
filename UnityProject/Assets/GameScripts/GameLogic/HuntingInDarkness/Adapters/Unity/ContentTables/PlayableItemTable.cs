using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Content;
using HuntingInDarkness.GameCore.Foundation;
using UnityEngine;

namespace HuntingInDarkness.ContentTables
{
    [Serializable]
    public sealed class WeaponStatsTableRecord
    {
        public int speed = 1;
        public int power = 1;
        public int accuracy;
        public int range = 1;
        public string specialRule;
    }

    [Serializable]
    public sealed class ArmorStatsTableRecord
    {
        public int armorHead;
        public int armorBody;
        public int armorArms;
        public int armorLegs;
    }

    [Serializable]
    public sealed class ItemTableRecord : IStableContentRecord
    {
        public string id;
        public string itemName;
        public string itemType;
        public string description;
        public List<string> tags = new();
        public List<string> keywords = new();
        public WeaponStatsTableRecord weaponStats = new();
        public ArmorStatsTableRecord armorStats = new();
        public int stackLimit = 99;
        public int huntNoise;
        public string consumableEffect;
        public int consumableEffectAmount;

        public string Id => id;
    }

    [Serializable]
    public sealed class ItemTableDocument
    {
        public int version = 1;
        public List<ItemTableRecord> items = new();
    }

    public sealed class JsonItemTableSource : IContentTableSource<ItemTableRecord>
    {
        private readonly string resourcePath;

        public JsonItemTableSource(string resourcePath)
        {
            this.resourcePath = resourcePath;
        }

        public IReadOnlyList<ItemTableRecord> Load()
        {
            TextAsset tableAsset = Resources.Load<TextAsset>(resourcePath);
            if (tableAsset == null)
            {
                Debug.LogWarning($"[ContentTable] 未找到物品表 Resources/{resourcePath}.json");
                return Array.Empty<ItemTableRecord>();
            }

            ItemTableDocument document = JsonUtility.FromJson<ItemTableDocument>(tableAsset.text);
            if (document?.items == null)
            {
                Debug.LogError($"[ContentTable] 物品表格式无效：{resourcePath}");
                return Array.Empty<ItemTableRecord>();
            }
            if (document.version != 1)
                Debug.LogWarning($"[ContentTable] 物品表版本 {document.version} 尚未显式支持，将按版本 1 读取。");
            return document.items;
        }
    }

    /// <summary>把稳定表记录映射为现有 ItemData，使装备 View 与 Action 契约不依赖表实现。</summary>
    public static class PlayableItemTableRuntime
    {
        private const string TablePath = "HuntingInDarkness/Tables/items";
        private static List<ItemData> cachedItems;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            cachedItems = null;
        }

        public static IReadOnlyList<ItemData> GetItems()
        {
            if (cachedItems != null && cachedItems.TrueForAll(item => item != null)) return cachedItems;
            cachedItems = Build(new JsonItemTableSource(TablePath).Load(), message => Debug.LogError($"[ContentTable] {message}"));
            return cachedItems;
        }

        internal static List<ItemData> BuildTable(Action<string> reportError) => Build(new JsonItemTableSource(TablePath).Load(), reportError);

        public static List<ItemData> Build(IReadOnlyList<ItemTableRecord> records, Action<string> reportError = null)
        {
            var result = new List<ItemData>();
            if (records == null) return result;

            Dictionary<string, int> idCounts = Count(records, record => record?.id);
            Dictionary<string, int> nameCounts = Count(records, record => record?.itemName);
            var reportedDuplicates = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemTableRecord record in records)
            {
                string id = record?.id?.Trim() ?? string.Empty;
                string itemName = record?.itemName?.Trim() ?? string.Empty;
                bool duplicateId = idCounts.TryGetValue(id, out int idCount) && idCount > 1;
                bool duplicateName = nameCounts.TryGetValue(itemName, out int nameCount) && nameCount > 1;
                if (duplicateId || duplicateName)
                {
                    if (duplicateId && reportedDuplicates.Add($"id:{id}")) reportError?.Invoke($"物品表存在重复记录：id:{id}");
                    if (duplicateName && reportedDuplicates.Add($"name:{itemName}")) reportError?.Invoke($"物品表存在重复记录：name:{itemName}");
                    continue;
                }
                if (!TryCreate(record, out ItemData item, out string error))
                {
                    reportError?.Invoke(error);
                    continue;
                }
                result.Add(item);
            }
            return result;
        }

        private static bool TryCreate(ItemTableRecord record, out ItemData item, out string error)
        {
            item = null;
            if (record == null || string.IsNullOrWhiteSpace(record.id) || string.IsNullOrWhiteSpace(record.itemName))
            {
                error = "物品记录缺少稳定 id 或名称。";
                return false;
            }
            if (!Enum.TryParse(record.itemType, true, out ItemType itemType) || !Enum.IsDefined(typeof(ItemType), itemType))
            {
                error = $"物品 {record.id} 的类型无效：{record.itemType}";
                return false;
            }
            bool effectSpecified = !string.IsNullOrWhiteSpace(record.consumableEffect);
            if (!Enum.TryParse(record.consumableEffect, true, out ConsumableEffectKind consumableEffect) || !Enum.IsDefined(typeof(ConsumableEffectKind), consumableEffect))
            {
                if (effectSpecified)
                {
                    error = $"物品 {record.id} 的消耗品效果无效：{record.consumableEffect}";
                    return false;
                }
                consumableEffect = ConsumableEffectKind.None;
            }
            int consumableEffectAmount = record.consumableEffectAmount;
            if (itemType == ItemType.Consumable && (consumableEffect == ConsumableEffectKind.None || consumableEffectAmount < 1 || consumableEffectAmount > 99 || record.huntNoise != 0))
            {
                error = $"消耗品 {record.id} 的效果、数量或狩猎噪音配置无效。";
                return false;
            }
            if (itemType != ItemType.Consumable && (consumableEffect != ConsumableEffectKind.None || consumableEffectAmount != 0))
            {
                error = $"非消耗品 {record.id} 不得声明消耗品效果。";
                return false;
            }
            if (!TryParseTags(record.tags, out List<ItemTag> tags, out error))
            {
                error = $"物品 {record.id} {error}";
                return false;
            }

            item = ScriptableObject.CreateInstance<ItemData>();
            item.name = record.id.Trim();
            item.ConfigureContentId(record.id);
            item.itemName = record.itemName.Trim();
            item.itemType = itemType;
            item.description = record.description ?? string.Empty;
            item.tags = tags;
            item.keywords = NormalizeKeywords(record.keywords);
            item.weaponStats = ToWeaponStats(record.weaponStats);
            item.armorStats = ToArmorStats(record.armorStats);
            item.stackLimit = Mathf.Max(1, record.stackLimit);
            item.ConfigureHuntNoise(record.huntNoise);
            item.ConfigureConsumableEffect(consumableEffect, consumableEffectAmount);
            error = string.Empty;
            return true;
        }

        private static bool TryParseTags(IReadOnlyList<string> values, out List<ItemTag> tags, out string error)
        {
            tags = new List<ItemTag>();
            error = string.Empty;
            if (values == null) return true;
            foreach (string value in values)
            {
                if (!Enum.TryParse(value, true, out ItemTag tag) || !Enum.IsDefined(typeof(ItemTag), tag))
                {
                    error = $"含无效兼容标签：{value}";
                    return false;
                }
                if (!tags.Contains(tag)) tags.Add(tag);
            }
            return true;
        }

        private static List<string> NormalizeKeywords(IReadOnlyList<string> values)
        {
            var result = new List<string>();
            var known = new HashSet<string>(StringComparer.Ordinal);
            if (values == null) return result;
            foreach (string value in values)
            {
                string keyword = KeywordRules.Normalize(value);
                if (keyword.Length > 0 && known.Add(keyword)) result.Add(keyword);
            }
            return result;
        }

        private static WeaponStats ToWeaponStats(WeaponStatsTableRecord record)
        {
            record ??= new WeaponStatsTableRecord();
            return new WeaponStats { speed = Mathf.Max(1, record.speed), power = Mathf.Max(0, record.power), accuracy = record.accuracy, range = Mathf.Max(1, record.range), specialRule = record.specialRule ?? string.Empty };
        }

        private static ArmorStats ToArmorStats(ArmorStatsTableRecord record)
        {
            record ??= new ArmorStatsTableRecord();
            return new ArmorStats { armorHead = Mathf.Max(0, record.armorHead), armorBody = Mathf.Max(0, record.armorBody), armorArms = Mathf.Max(0, record.armorArms), armorLegs = Mathf.Max(0, record.armorLegs) };
        }

        private static Dictionary<string, int> Count(IReadOnlyList<ItemTableRecord> records, Func<ItemTableRecord, string> selector)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (ItemTableRecord record in records)
            {
                string key = selector(record)?.Trim() ?? string.Empty;
                if (key.Length == 0) continue;
                counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
            }
            return counts;
        }
    }
}
