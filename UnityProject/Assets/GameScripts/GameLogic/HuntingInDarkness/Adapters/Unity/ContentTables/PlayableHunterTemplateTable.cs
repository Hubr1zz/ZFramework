using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Content;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace HuntingInDarkness.ContentTables
{
    [Serializable]
    public sealed class HunterStatsTableRecord
    {
        public int strength;
        public int accuracy;
        public int evasion;
        public int movement = 5;
        public int luck;
        public int speed = 1;
        public int armorHead;
        public int armorBody;
        public int armorArms;
        public int armorLegs;
    }

    [Serializable]
    public sealed class HunterTemplateTableRecord : IStableContentRecord
    {
        public string id;
        public string displayName;
        public bool starting;
        public bool recruitable = true;
        public HunterStatsTableRecord stats = new();
        public int willpower = 2;
        public int luck;
        public int insanity;
        public List<string> startingEquipmentIds = new();
        public List<string> traits = new();
        public List<string> ailments = new();

        public string Id => id;
    }

    [Serializable]
    public sealed class HunterTemplateTableDocument
    {
        public int version = 1;
        public List<HunterTemplateTableRecord> hunters = new();
    }

    public sealed class JsonHunterTemplateTableSource : IContentTableSource<HunterTemplateTableRecord>
    {
        private readonly string resourcePath;
        private readonly TextAsset tableAsset;

        public JsonHunterTemplateTableSource(string resourcePath, TextAsset tableAsset = null)
        {
            this.resourcePath = resourcePath;
            this.tableAsset = tableAsset;
        }

        public IReadOnlyList<HunterTemplateTableRecord> Load()
        {
            TextAsset source = tableAsset != null ? tableAsset : Resources.Load<TextAsset>(resourcePath);
            if (source == null)
            {
                Debug.LogWarning($"[ContentTable] 未找到猎人模板表 Resources/{resourcePath}.json");
                return Array.Empty<HunterTemplateTableRecord>();
            }

            HunterTemplateTableDocument document = JsonUtility.FromJson<HunterTemplateTableDocument>(source.text);
            if (document?.hunters == null)
            {
                Debug.LogError($"[ContentTable] 猎人模板表格式无效：{resourcePath}");
                return Array.Empty<HunterTemplateTableRecord>();
            }
            if (document.version != 1)
                Debug.LogWarning($"[ContentTable] 猎人模板表版本 {document.version} 尚未显式支持，将按版本 1 读取。");
            return document.hunters;
        }
    }

    public readonly struct HunterTemplateTableEntry
    {
        public HunterTemplateTableEntry(HunterData template, bool starting, bool recruitable)
        {
            Template = template;
            Starting = starting;
            Recruitable = recruitable;
        }

        public HunterData Template { get; }
        public bool Starting { get; }
        public bool Recruitable { get; }
    }

    /// <summary>把表记录映射为现有 HunterData；招募 View 与 ActionQueue 不依赖表来源。</summary>
    public static class PlayableHunterTemplateTableRuntime
    {
        private const string TablePath = "HuntingInDarkness/Tables/hunters";

        public static bool Extend(IReadOnlyList<HunterData> baseStarting, IReadOnlyList<HunterData> baseRecruitment, IReadOnlyList<ItemData> items, TextAsset tableAsset, out List<HunterData> allStarting, out List<HunterData> allRecruitment, Action<string> reportError = null)
        {
            return Extend(baseStarting, baseRecruitment, items, tableAsset, out allStarting, out allRecruitment, out _, reportError);
        }

        internal static bool Extend(IReadOnlyList<HunterData> baseStarting, IReadOnlyList<HunterData> baseRecruitment, IReadOnlyList<ItemData> items, TextAsset tableAsset, out List<HunterData> allStarting, out List<HunterData> allRecruitment, out List<HunterData> generatedTemplates, Action<string> reportError = null)
        {
            allStarting = Copy(baseStarting);
            allRecruitment = Copy(baseRecruitment);
            generatedTemplates = new List<HunterData>();
            if (!ValidateBaseIdentities(allStarting, allRecruitment, reportError)) return false;

            List<HunterTemplateTableEntry> entries = Build(new JsonHunterTemplateTableSource(TablePath, tableAsset).Load(), items, reportError);
            var identities = new Dictionary<string, HunterData>(StringComparer.Ordinal);
            IndexIdentities(allStarting, identities);
            IndexIdentities(allRecruitment, identities);
            foreach (HunterTemplateTableEntry entry in entries)
            {
                HunterData template = entry.Template;
                generatedTemplates.Add(template);
                if (identities.TryGetValue(template.ContentId, out HunterData idOwner) && !ReferenceEquals(idOwner, template) || identities.TryGetValue(template.hunterName, out HunterData nameOwner) && !ReferenceEquals(nameOwner, template))
                {
                    reportError?.Invoke($"猎人模板表与现有目录身份冲突：{template.ContentId}/{template.hunterName}");
                    DestroyTemplate(template);
                    continue;
                }
                identities[template.ContentId] = template;
                identities[template.hunterName] = template;
                if (entry.Starting) allStarting.Add(template);
                if (entry.Recruitable) allRecruitment.Add(template);
            }
            return true;
        }

        public static List<HunterTemplateTableEntry> Build(IReadOnlyList<HunterTemplateTableRecord> records, IReadOnlyList<ItemData> items, Action<string> reportError = null)
        {
            var result = new List<HunterTemplateTableEntry>();
            if (records == null) return result;

            Dictionary<string, ItemData> itemById = BuildItemIndex(items, reportError);
            Dictionary<string, int> idCounts = Count(records, record => record?.id);
            Dictionary<string, int> nameCounts = Count(records, record => record?.displayName);
            var reportedDuplicates = new HashSet<string>(StringComparer.Ordinal);
            foreach (HunterTemplateTableRecord record in records)
            {
                string id = record?.id?.Trim() ?? string.Empty;
                string displayName = record?.displayName?.Trim() ?? string.Empty;
                bool duplicateId = idCounts.TryGetValue(id, out int idCount) && idCount > 1;
                bool duplicateName = nameCounts.TryGetValue(displayName, out int nameCount) && nameCount > 1;
                if (duplicateId || duplicateName)
                {
                    if (duplicateId && reportedDuplicates.Add($"id:{id}")) reportError?.Invoke($"猎人模板表存在重复记录：id:{id}");
                    if (duplicateName && reportedDuplicates.Add($"name:{displayName}")) reportError?.Invoke($"猎人模板表存在重复记录：name:{displayName}");
                    continue;
                }
                if (!TryCreate(record, itemById, out HunterTemplateTableEntry entry, out string error))
                {
                    reportError?.Invoke(error);
                    continue;
                }
                result.Add(entry);
            }
            return result;
        }

        private static bool TryCreate(HunterTemplateTableRecord record, IReadOnlyDictionary<string, ItemData> itemById, out HunterTemplateTableEntry entry, out string error)
        {
            entry = default;
            if (record == null || string.IsNullOrWhiteSpace(record.id) || string.IsNullOrWhiteSpace(record.displayName))
            {
                error = "猎人模板记录缺少稳定 id 或名称。";
                return false;
            }
            if (!record.starting && !record.recruitable)
            {
                error = $"猎人模板 {record.id} 未进入初始或招募内容池。";
                return false;
            }
            if (!TryBuildStats(record, out HunterCombatStats stats, out error)) return false;
            if (record.willpower < 0 || record.luck < 0 || record.insanity < 0)
            {
                error = $"猎人模板 {record.id} 的意志、命运或压抑值不能为负数。";
                return false;
            }
            if (!TryResolveEquipment(record, itemById, out List<ItemData> equipment, out error)) return false;

            HunterData template = ScriptableObject.CreateInstance<HunterData>();
            template.name = record.id.Trim();
            template.ConfigureContentId(record.id);
            template.hunterName = record.displayName.Trim();
            template.initialStats = stats;
            template.initialWillpower = record.willpower;
            template.initialLuck = record.luck;
            template.initialInsanity = record.insanity;
            template.startingEquipment = equipment;
            template.startingTraits = Normalize(record.traits);
            template.startingAilments = Normalize(record.ailments);
            entry = new HunterTemplateTableEntry(template, record.starting, record.recruitable);
            error = string.Empty;
            return true;
        }

        private static bool TryBuildStats(HunterTemplateTableRecord record, out HunterCombatStats stats, out string error)
        {
            HunterStatsTableRecord source = record.stats ?? new HunterStatsTableRecord();
            if (source.strength < 0 || source.accuracy < 0 || source.evasion < 0 || source.movement <= 0 || source.luck < 0 || source.speed < 0 || source.armorHead < 0 || source.armorBody < 0 || source.armorArms < 0 || source.armorLegs < 0)
            {
                stats = null;
                error = $"猎人模板 {record.id} 含无效的负数属性或非正移动力。";
                return false;
            }
            stats = new HunterCombatStats { strength = source.strength, accuracy = source.accuracy, evasion = source.evasion, movement = source.movement, luck = source.luck, speed = source.speed, armorHead = source.armorHead, armorBody = source.armorBody, armorArms = source.armorArms, armorLegs = source.armorLegs };
            error = string.Empty;
            return true;
        }

        private static bool TryResolveEquipment(HunterTemplateTableRecord record, IReadOnlyDictionary<string, ItemData> itemById, out List<ItemData> equipment, out string error)
        {
            equipment = new List<ItemData>();
            error = string.Empty;
            if (record.startingEquipmentIds == null) return true;
            if (record.startingEquipmentIds.Count > EquipmentRules.MaximumEquipmentCount)
            {
                error = $"猎人模板 {record.id} 的初始装备超过 {EquipmentRules.MaximumEquipmentCount} 件。";
                return false;
            }
            foreach (string value in record.startingEquipmentIds)
            {
                string itemId = value?.Trim() ?? string.Empty;
                if (!itemById.TryGetValue(itemId, out ItemData item) || item == null || item.itemType == ItemType.Resource)
                {
                    error = $"猎人模板 {record.id} 引用了未知或不可装备物品：{itemId}";
                    return false;
                }
                int weaponCount = equipment.FindAll(existing => existing != null && existing.itemType == ItemType.Weapon).Count;
                if (!EquipmentRules.CanEquip(equipment.Count, weaponCount, item.itemType == ItemType.Weapon, out string reason))
                {
                    error = $"猎人模板 {record.id} 的初始装备无效：{reason}";
                    return false;
                }
                if (item.itemType == ItemType.Armor)
                {
                    ArmorCoverage occupied = ArmorCoverage.None;
                    foreach (ItemData existing in equipment)
                        if (existing != null && existing.itemType == ItemType.Armor)
                            occupied |= PlayableEquipmentRules.GetCoverage(existing);
                    if (!ArmorCoverageRules.CanEquip(occupied, PlayableEquipmentRules.GetCoverage(item), out reason))
                    {
                        error = $"猎人模板 {record.id} 的初始装备无效：{reason}";
                        return false;
                    }
                }
                equipment.Add(item);
            }
            return true;
        }

        private static void DestroyTemplate(HunterData template)
        {
            if (template == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(template);
            else
                UnityEngine.Object.DestroyImmediate(template);
        }

        private static Dictionary<string, ItemData> BuildItemIndex(IReadOnlyList<ItemData> items, Action<string> reportError)
        {
            var result = new Dictionary<string, ItemData>(StringComparer.Ordinal);
            var ambiguous = new HashSet<string>(StringComparer.Ordinal);
            if (items == null) return result;
            foreach (ItemData item in items)
            {
                string id = item?.ContentId ?? string.Empty;
                if (id.Length == 0) continue;
                if (!result.TryAdd(id, item)) ambiguous.Add(id);
            }
            foreach (string id in ambiguous)
            {
                result.Remove(id);
                reportError?.Invoke($"物品目录存在重复稳定 id，猎人装备无法解析：{id}");
            }
            return result;
        }

        private static bool ValidateBaseIdentities(IReadOnlyList<HunterData> starting, IReadOnlyList<HunterData> recruitment, Action<string> reportError)
        {
            var identities = new Dictionary<string, HunterData>(StringComparer.Ordinal);
            foreach (HunterData template in Unique(starting, recruitment))
            {
                if (!template.HasExplicitContentId || string.IsNullOrWhiteSpace(template.ContentId) || string.IsNullOrWhiteSpace(template.hunterName))
                {
                    reportError?.Invoke($"猎人资产缺少稳定 id 或名称：{template?.name}");
                    return false;
                }
                if (identities.TryGetValue(template.ContentId, out HunterData idOwner) && !ReferenceEquals(idOwner, template) || identities.TryGetValue(template.hunterName, out HunterData nameOwner) && !ReferenceEquals(nameOwner, template))
                {
                    reportError?.Invoke($"猎人目录存在重复或交叉冲突身份：{template.ContentId}/{template.hunterName}");
                    return false;
                }
                identities[template.ContentId] = template;
                identities[template.hunterName] = template;
            }
            return true;
        }

        private static List<HunterData> Unique(IReadOnlyList<HunterData> first, IReadOnlyList<HunterData> second)
        {
            var result = new List<HunterData>();
            AppendUnique(result, first);
            AppendUnique(result, second);
            return result;
        }

        private static List<HunterData> Copy(IReadOnlyList<HunterData> source)
        {
            var result = new List<HunterData>();
            AppendUnique(result, source);
            return result;
        }

        private static void AppendUnique(List<HunterData> target, IReadOnlyList<HunterData> source)
        {
            if (source == null) return;
            foreach (HunterData template in source)
                if (template != null && !target.Exists(existing => ReferenceEquals(existing, template)))
                    target.Add(template);
        }

        private static void IndexIdentities(IReadOnlyList<HunterData> templates, IDictionary<string, HunterData> identities)
        {
            foreach (HunterData template in templates)
            {
                identities[template.ContentId] = template;
                identities[template.hunterName] = template;
            }
        }

        private static List<string> Normalize(IReadOnlyList<string> values)
        {
            var result = new List<string>();
            var known = new HashSet<string>(StringComparer.Ordinal);
            if (values == null) return result;
            foreach (string value in values)
            {
                string normalized = value?.Trim() ?? string.Empty;
                if (normalized.Length > 0 && known.Add(normalized)) result.Add(normalized);
            }
            return result;
        }

        private static Dictionary<string, int> Count(IReadOnlyList<HunterTemplateTableRecord> records, Func<HunterTemplateTableRecord, string> selector)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (HunterTemplateTableRecord record in records)
            {
                string key = selector(record)?.Trim() ?? string.Empty;
                if (key.Length == 0) continue;
                counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
            }
            return counts;
        }
    }
}
