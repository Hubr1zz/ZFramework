using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Content;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.ContentTables
{
    [Serializable]
    public sealed class InventionCostTableRecord
    {
        public string itemId;
        public int count = 1;
    }

    [Serializable]
    public sealed class InventionEffectTableRecord
    {
        public string kind;
        public string target;
        public int value = 1;
    }

    [Serializable]
    public sealed class InventionTableRecord : IStableContentRecord
    {
        public string id;
        public string inventionName;
        public string description;
        public List<string> prerequisiteIds = new();
        public List<InventionCostTableRecord> costs = new();
        public List<string> exclusiveIds = new();
        public string effectDescription;
        public List<InventionEffectTableRecord> effects = new();
        public string category;

        public string Id => id;
    }

    [Serializable]
    public sealed class InventionTableDocument
    {
        public int version = 1;
        public List<InventionTableRecord> inventions = new();
    }

    public sealed class JsonInventionTableSource : IContentTableSource<InventionTableRecord>
    {
        private readonly TextAsset tableAsset;

        public JsonInventionTableSource(TextAsset tableAsset)
        {
            this.tableAsset = tableAsset;
        }

        public IReadOnlyList<InventionTableRecord> Load()
        {
            if (tableAsset == null) return Array.Empty<InventionTableRecord>();
            try
            {
                InventionTableDocument document = JsonUtility.FromJson<InventionTableDocument>(tableAsset.text);
                if (document?.inventions == null)
                {
                    Debug.LogError($"[ContentTable] 发明表格式无效：{tableAsset.name}");
                    return Array.Empty<InventionTableRecord>();
                }
                if (document.version != 1)
                {
                    Debug.LogError($"[ContentTable] 不支持发明表版本 {document.version}：{tableAsset.name}");
                    return Array.Empty<InventionTableRecord>();
                }
                return document.inventions;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ContentTable] 无法读取发明表 {tableAsset.name}：{exception.Message}");
                return Array.Empty<InventionTableRecord>();
            }
        }
    }

    /// <summary>把发明表转换为现有 InventionData，规则和表现继续复用 Settlement ActionQueue 与 3D 卡牌。</summary>
    public static class PlayableInventionTableRuntime
    {
        private sealed class ValidatedRecord
        {
            public InventionTableRecord Source;
            public string Id;
            public string Name;
            public InventionCategory Category;
            public List<InventionCost> Costs;
            public List<string> PrerequisiteIds;
            public List<string> ExclusiveIds;
            public List<InventionPassiveEffect> Effects;
        }

        private static string cachedTableText;
        private static string cachedDependencySignature;
        private static List<InventionData> cachedInventions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            cachedTableText = null;
            cachedDependencySignature = string.Empty;
            cachedInventions = null;
        }

        public static IReadOnlyList<InventionData> GetInventions(TextAsset tableAsset, IReadOnlyList<ItemData> items, IReadOnlyList<InventionData> baseInventions)
        {
            if (tableAsset == null) return Array.Empty<InventionData>();
            string tableText = tableAsset.text ?? string.Empty;
            string dependencySignature = BuildDependencySignature(items, baseInventions);
            if (string.Equals(cachedTableText, tableText, StringComparison.Ordinal) && cachedDependencySignature == dependencySignature && cachedInventions != null && cachedInventions.TrueForAll(invention => invention != null)) return cachedInventions;

            cachedInventions = Build(new JsonInventionTableSource(tableAsset).Load(), items, baseInventions, message => Debug.LogError($"[ContentTable] {message}"));
            cachedTableText = tableText;
            cachedDependencySignature = dependencySignature;
            return cachedInventions;
        }

        public static List<InventionData> Build(IReadOnlyList<InventionTableRecord> records, IReadOnlyList<ItemData> items, IReadOnlyList<InventionData> baseInventions = null, Action<string> reportError = null)
        {
            var result = new List<InventionData>();
            if (records == null) return result;

            Dictionary<string, ItemData> itemById = BuildUniqueIndex(items, item => item.ContentId, "物品", reportError);
            Dictionary<string, InventionData> baseById = BuildUniqueIndex(baseInventions, invention => invention.ContentId, "基础发明", reportError);
            var invalidIndexes = FindIdentityConflicts(records, baseInventions, reportError);
            var validatedById = new Dictionary<string, ValidatedRecord>(StringComparer.Ordinal);
            for (int index = 0; index < records.Count; index++)
            {
                if (invalidIndexes.Contains(index)) continue;
                if (!TryValidate(records[index], itemById, out ValidatedRecord validated, out string error))
                {
                    reportError?.Invoke(error);
                    invalidIndexes.Add(index);
                    continue;
                }
                validatedById.Add(validated.Id, validated);
            }

            RejectBrokenGraph(validatedById, baseById, reportError);
            Dictionary<string, InventionData> createdById = CreateAssets(records, validatedById, result);
            LinkGraph(validatedById, baseById, createdById);
            return result;
        }

        private static HashSet<int> FindIdentityConflicts(IReadOnlyList<InventionTableRecord> records, IReadOnlyList<InventionData> baseInventions, Action<string> reportError)
        {
            var owners = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
            var baseIdentities = new HashSet<string>(StringComparer.Ordinal);
            if (baseInventions != null)
            {
                foreach (InventionData invention in baseInventions)
                {
                    if (invention == null) continue;
                    AddIdentity(baseIdentities, invention.ContentId);
                    AddIdentity(baseIdentities, invention.inventionName);
                    AddIdentity(baseIdentities, invention.name);
                }
            }

            for (int index = 0; index < records.Count; index++)
            {
                AddOwner(owners, records[index]?.id, index);
                AddOwner(owners, records[index]?.inventionName, index);
            }

            var invalid = new HashSet<int>();
            var reported = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, HashSet<int>> pair in owners)
            {
                bool conflictsWithBase = baseIdentities.Contains(pair.Key);
                if (pair.Value.Count <= 1 && !conflictsWithBase) continue;
                foreach (int owner in pair.Value) invalid.Add(owner);
                if (reported.Add(pair.Key)) reportError?.Invoke($"发明表身份冲突：{pair.Key}");
            }
            return invalid;
        }

        private static bool TryValidate(InventionTableRecord record, IReadOnlyDictionary<string, ItemData> itemById, out ValidatedRecord validated, out string error)
        {
            validated = null;
            string id = record?.id?.Trim() ?? string.Empty;
            string name = record?.inventionName?.Trim() ?? string.Empty;
            if (id.Length == 0 || name.Length == 0)
            {
                error = "发明记录缺少稳定 id 或名称。";
                return false;
            }
            if (!Enum.TryParse(record.category, true, out InventionCategory category) || !Enum.IsDefined(typeof(InventionCategory), category))
            {
                error = $"发明 {id} 的类别无效：{record.category}";
                return false;
            }
            if (!TryBuildCosts(record, itemById, out List<InventionCost> costs, out error)) return false;
            if (!TryBuildEffects(record, out List<InventionPassiveEffect> effects, out error)) return false;

            List<string> prerequisiteIds = NormalizeIds(record.prerequisiteIds);
            List<string> exclusiveIds = NormalizeIds(record.exclusiveIds);
            if (prerequisiteIds.Contains(id) || exclusiveIds.Contains(id))
            {
                error = $"发明 {id} 不能引用自身。";
                return false;
            }

            validated = new ValidatedRecord { Source = record, Id = id, Name = name, Category = category, Costs = costs, PrerequisiteIds = prerequisiteIds, ExclusiveIds = exclusiveIds, Effects = effects };
            error = string.Empty;
            return true;
        }

        private static bool TryBuildEffects(InventionTableRecord record, out List<InventionPassiveEffect> effects, out string error)
        {
            effects = new List<InventionPassiveEffect>();
            error = string.Empty;
            if (record.effects == null) return true;
            foreach (InventionEffectTableRecord effect in record.effects)
            {
                if (effect == null || !Enum.TryParse(effect.kind, true, out InventionEffectKind kind) || kind == InventionEffectKind.None || !Enum.IsDefined(typeof(InventionEffectKind), kind))
                {
                    error = $"发明 {record.id} 含无效效果类型：{effect?.kind}";
                    return false;
                }
                if (!Enum.TryParse(effect.target, true, out InventionEffectTarget target) || !Enum.IsDefined(typeof(InventionEffectTarget), target))
                {
                    error = $"发明 {record.id} 含无效效果目标：{effect.target}";
                    return false;
                }
                if (effect.value == 0)
                {
                    error = $"发明 {record.id} 的效果数值不能为 0。";
                    return false;
                }
                effects.Add(new InventionPassiveEffect { kind = kind, target = target, value = effect.value });
            }
            return true;
        }

        private static bool TryBuildCosts(InventionTableRecord record, IReadOnlyDictionary<string, ItemData> itemById, out List<InventionCost> costs, out string error)
        {
            costs = new List<InventionCost>();
            error = string.Empty;
            var amounts = new Dictionary<ItemData, int>();
            var order = new List<ItemData>();
            if (record.costs == null) return true;
            foreach (InventionCostTableRecord cost in record.costs)
            {
                string itemId = cost?.itemId?.Trim() ?? string.Empty;
                if (cost == null || cost.count <= 0 || !itemById.TryGetValue(itemId, out ItemData item) || item == null)
                {
                    error = $"发明 {record.id} 含无效成本：{itemId}";
                    return false;
                }
                long combined = (amounts.TryGetValue(item, out int amount) ? amount : 0L) + cost.count;
                if (combined > int.MaxValue)
                {
                    error = $"发明 {record.id} 的成本数量溢出：{itemId}";
                    return false;
                }
                if (!amounts.ContainsKey(item)) order.Add(item);
                amounts[item] = (int)combined;
            }
            foreach (ItemData item in order)
                costs.Add(new InventionCost { resource = item, count = amounts[item] });
            return true;
        }

        private static void RejectBrokenGraph(Dictionary<string, ValidatedRecord> records, IReadOnlyDictionary<string, InventionData> baseById, Action<string> reportError)
        {
            var rejected = new HashSet<string>(StringComparer.Ordinal);
            foreach (ValidatedRecord record in records.Values)
            {
                if (HasUnknownReference(record.PrerequisiteIds, records, baseById, out string missing) || HasUnknownReference(record.ExclusiveIds, records, baseById, out missing))
                {
                    rejected.Add(record.Id);
                    reportError?.Invoke($"发明 {record.Id} 引用了未知发明：{missing}");
                }
            }

            var states = new Dictionary<string, int>(StringComparer.Ordinal);
            var stack = new List<string>();
            foreach (string id in records.Keys)
                FindCycles(id, records, rejected, states, stack, reportError);

            bool changed;
            do
            {
                changed = false;
                foreach (ValidatedRecord record in records.Values)
                {
                    if (rejected.Contains(record.Id)) continue;
                    if (!ContainsAny(record.PrerequisiteIds, rejected) && !ContainsAny(record.ExclusiveIds, rejected)) continue;
                    rejected.Add(record.Id);
                    reportError?.Invoke($"发明 {record.Id} 依赖了已拒绝的发明记录。");
                    changed = true;
                }
            } while (changed);

            foreach (string id in rejected)
                records.Remove(id);
        }

        private static void FindCycles(string id, IReadOnlyDictionary<string, ValidatedRecord> records, HashSet<string> rejected, Dictionary<string, int> states, List<string> stack, Action<string> reportError)
        {
            if (rejected.Contains(id) || states.TryGetValue(id, out int state) && state == 2) return;
            if (state == 1)
            {
                int start = stack.IndexOf(id);
                if (start < 0) return;
                for (int index = start; index < stack.Count; index++) rejected.Add(stack[index]);
                reportError?.Invoke($"发明依赖存在循环：{string.Join(" -> ", stack.GetRange(start, stack.Count - start))} -> {id}");
                return;
            }

            states[id] = 1;
            stack.Add(id);
            foreach (string prerequisiteId in records[id].PrerequisiteIds)
                if (records.ContainsKey(prerequisiteId))
                    FindCycles(prerequisiteId, records, rejected, states, stack, reportError);
            stack.RemoveAt(stack.Count - 1);
            states[id] = 2;
        }

        private static Dictionary<string, InventionData> CreateAssets(IReadOnlyList<InventionTableRecord> sourceOrder, IReadOnlyDictionary<string, ValidatedRecord> records, List<InventionData> result)
        {
            var createdById = new Dictionary<string, InventionData>(StringComparer.Ordinal);
            foreach (InventionTableRecord source in sourceOrder)
            {
                string id = source?.id?.Trim() ?? string.Empty;
                if (!records.TryGetValue(id, out ValidatedRecord record)) continue;
                InventionData invention = ScriptableObject.CreateInstance<InventionData>();
                invention.name = record.Id;
                invention.ConfigureContentId(record.Id);
                invention.inventionName = record.Name;
                invention.description = record.Source.description ?? string.Empty;
                invention.costs = record.Costs;
                invention.effectDescription = record.Source.effectDescription ?? string.Empty;
                invention.unlockEffects = record.Effects;
                invention.category = record.Category;
                createdById.Add(record.Id, invention);
                result.Add(invention);
            }
            return createdById;
        }

        private static void LinkGraph(IReadOnlyDictionary<string, ValidatedRecord> records, IReadOnlyDictionary<string, InventionData> baseById, IReadOnlyDictionary<string, InventionData> createdById)
        {
            foreach (KeyValuePair<string, InventionData> pair in createdById)
            {
                ValidatedRecord record = records[pair.Key];
                pair.Value.prerequisites = ResolveReferences(record.PrerequisiteIds, baseById, createdById);
                pair.Value.exclusiveWith = ResolveReferences(record.ExclusiveIds, baseById, createdById);
            }
        }

        private static List<InventionData> ResolveReferences(IReadOnlyList<string> ids, IReadOnlyDictionary<string, InventionData> baseById, IReadOnlyDictionary<string, InventionData> createdById)
        {
            var result = new List<InventionData>();
            foreach (string id in ids)
            {
                if (createdById.TryGetValue(id, out InventionData created)) result.Add(created);
                else if (baseById.TryGetValue(id, out InventionData existing)) result.Add(existing);
            }
            return result;
        }

        private static Dictionary<string, T> BuildUniqueIndex<T>(IReadOnlyList<T> assets, Func<T, string> selectId, string label, Action<string> reportError) where T : UnityEngine.Object
        {
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            if (assets == null) return result;
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (T asset in assets)
            {
                string id = asset != null ? selectId(asset)?.Trim() ?? string.Empty : string.Empty;
                if (id.Length == 0) continue;
                counts[id] = counts.TryGetValue(id, out int count) ? count + 1 : 1;
            }
            foreach (T asset in assets)
            {
                string id = asset != null ? selectId(asset)?.Trim() ?? string.Empty : string.Empty;
                if (id.Length == 0 || result.ContainsKey(id)) continue;
                if (counts[id] > 1)
                {
                    reportError?.Invoke($"{label}目录存在重复稳定 id：{id}");
                    continue;
                }
                result.Add(id, asset);
            }
            return result;
        }

        private static List<string> NormalizeIds(IReadOnlyList<string> values)
        {
            var result = new List<string>();
            var known = new HashSet<string>(StringComparer.Ordinal);
            if (values == null) return result;
            foreach (string value in values)
            {
                string id = value?.Trim() ?? string.Empty;
                if (id.Length > 0 && known.Add(id)) result.Add(id);
            }
            return result;
        }

        private static bool HasUnknownReference(IReadOnlyList<string> ids, IReadOnlyDictionary<string, ValidatedRecord> records, IReadOnlyDictionary<string, InventionData> baseById, out string missing)
        {
            foreach (string id in ids)
            {
                if (records.ContainsKey(id) || baseById.ContainsKey(id)) continue;
                missing = id;
                return true;
            }
            missing = string.Empty;
            return false;
        }

        private static bool ContainsAny(IReadOnlyList<string> ids, HashSet<string> values)
        {
            foreach (string id in ids)
                if (values.Contains(id)) return true;
            return false;
        }

        private static void AddOwner(Dictionary<string, HashSet<int>> owners, string value, int owner)
        {
            string key = value?.Trim() ?? string.Empty;
            if (key.Length == 0) return;
            if (!owners.TryGetValue(key, out HashSet<int> values))
            {
                values = new HashSet<int>();
                owners.Add(key, values);
            }
            values.Add(owner);
        }

        private static void AddIdentity(HashSet<string> identities, string value)
        {
            string key = value?.Trim() ?? string.Empty;
            if (key.Length > 0) identities.Add(key);
        }

        private static string BuildDependencySignature(IReadOnlyList<ItemData> items, IReadOnlyList<InventionData> inventions)
        {
            var values = new List<string>();
            if (items != null)
                foreach (ItemData item in items)
                    if (item != null) values.Add($"item:{item.ContentId}:{RuntimeHelpers.GetHashCode(item)}");
            if (inventions != null)
                foreach (InventionData invention in inventions)
                    if (invention != null) values.Add($"invention:{invention.ContentId}:{RuntimeHelpers.GetHashCode(invention)}");
            return string.Join("\n", values);
        }
    }
}
