using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Content;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.ContentTables
{
    [Serializable]
    public sealed class EventEffectTableRecord
    {
        public string effectType;
        public string targetName;
        public int value = 1;
        public string description;
    }

    [Serializable]
    public sealed class EventOptionConditionTableRecord
    {
        public string conditionKind;
        public string key;
        public int value;
        public bool inverted;
    }

    [Serializable]
    public sealed class EventOptionTableRecord
    {
        public string optionText;
        public string checkType;
        public int checkTarget;
        public string successText;
        public List<EventEffectTableRecord> successEffects = new();
        public string failText;
        public List<EventEffectTableRecord> failEffects = new();
        public bool alwaysAvailable = true;
        public List<EventOptionConditionTableRecord> conditions = new();
    }

    [Serializable]
    public sealed class EventTableRecord : IStableContentRecord
    {
        public string id;
        public string eventName;
        public string eventType;
        public string displayText;
        public string hiddenText;
        public List<EventOptionTableRecord> options = new();
        public List<EventEffectTableRecord> immediateEffects = new();
        public int minYear = 1;
        public int maxYear = 99;
        public int drawWeight = 1;
        public string category;

        public string Id => id;
    }

    [Serializable]
    public sealed class EventTableDocument
    {
        public int version = 1;
        public List<EventTableRecord> events = new();
    }

    public sealed class JsonEventTableSource : IContentTableSource<EventTableRecord>
    {
        private readonly string resourcePath;

        public JsonEventTableSource(string resourcePath)
        {
            this.resourcePath = resourcePath;
        }

        public IReadOnlyList<EventTableRecord> Load()
        {
            TextAsset tableAsset = Resources.Load<TextAsset>(resourcePath);
            if (tableAsset == null)
            {
                Debug.LogWarning($"[ContentTable] 未找到事件表 Resources/{resourcePath}.json");
                return Array.Empty<EventTableRecord>();
            }

            EventTableDocument document = JsonUtility.FromJson<EventTableDocument>(tableAsset.text);
            if (document?.events == null)
            {
                Debug.LogError($"[ContentTable] 事件表格式无效：{resourcePath}");
                return Array.Empty<EventTableRecord>();
            }
            if (document.version != 1)
                Debug.LogWarning($"[ContentTable] 事件表版本 {document.version} 尚未显式支持，将按版本 1 读取。");
            return document.events;
        }
    }

    /// <summary>把表数据映射为旧 EventData，并按稳定 ID 支持内容注入与覆盖。</summary>
    public static class PlayableEventTableRuntime
    {
        private const string TablePath = "HuntingInDarkness/Tables/events";
        private static List<EventTableRecord> cachedRecords;
        private static List<EventData> cachedEvents;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            cachedRecords = null;
            cachedEvents = null;
        }

        public static void Extend(IReadOnlyList<EventData> baseRandomEvents, IReadOnlyList<EventData> baseMainStoryEvents, out List<EventData> randomEvents, out List<EventData> mainStoryEvents)
        {
            randomEvents = CopyValid(baseRandomEvents);
            mainStoryEvents = CopyValid(baseMainStoryEvents);
            foreach (EventData tableEvent in GetEvents())
            {
                randomEvents.RemoveAll(gameEvent => gameEvent.name == tableEvent.name);
                mainStoryEvents.RemoveAll(gameEvent => gameEvent.name == tableEvent.name);
                if (tableEvent.category == EventCategory.MainStory)
                    mainStoryEvents.Add(tableEvent);
                else
                    randomEvents.Add(tableEvent);
            }
        }

        public static IReadOnlyList<EventData> GetEvents()
        {
            if (cachedEvents != null && cachedEvents.TrueForAll(gameEvent => gameEvent != null))
                return cachedEvents;

            cachedEvents = new List<EventData>();
            if (cachedRecords == null)
            {
                var source = new JsonEventTableSource(TablePath);
                cachedRecords = new List<EventTableRecord>(source.Load());
            }
            var knownIds = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, EventTableRecord> targetRecords = BuildUniqueTargetRecords(cachedRecords, out HashSet<string> duplicateIds);
            var reportedDuplicateIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (EventTableRecord record in cachedRecords)
            {
                if (record != null && duplicateIds.Contains(record.id))
                {
                    if (reportedDuplicateIds.Add(record.id))
                        Debug.LogError($"[ContentTable] 事件表存在重复 id：{record.id}");
                    continue;
                }
                if (!ValidateScheduleReferences(record, targetRecords, out string referenceError))
                {
                    Debug.LogError($"[ContentTable] {referenceError}");
                    continue;
                }
                if (!TryCreateEvent(record, knownIds, out EventData gameEvent, out string error))
                {
                    Debug.LogError($"[ContentTable] {error}");
                    continue;
                }
                cachedEvents.Add(gameEvent);
            }
            return cachedEvents;
        }

        private static bool TryCreateEvent(EventTableRecord record, HashSet<string> knownIds, out EventData gameEvent, out string error)
        {
            gameEvent = null;
            if (record == null || string.IsNullOrWhiteSpace(record.id) || string.IsNullOrWhiteSpace(record.eventName))
            {
                error = "事件记录缺少稳定 id 或名称。";
                return false;
            }
            if (!knownIds.Add(record.id))
            {
                error = $"事件表存在重复 id：{record.id}";
                return false;
            }
            if (!TryParse(record.eventType, out GameEventType eventType) || !TryParse(record.category, out EventCategory category))
            {
                error = $"事件 {record.id} 的类型或类别无效。";
                return false;
            }
            if (!ValidateOptions(record.options) || !ValidateEffects(record.immediateEffects))
            {
                error = $"事件 {record.id} 含无效选项或效果。";
                return false;
            }
            if (eventType == GameEventType.Choice && (record.options == null || record.options.Count == 0))
            {
                error = $"抉择事件 {record.id} 没有可用选项。";
                return false;
            }
            if (eventType == GameEventType.Choice && !record.options.Exists(option => option != null && option.alwaysAvailable))
            {
                error = $"抉择事件 {record.id} 缺少不受条件限制的保底选项。";
                return false;
            }

            gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = record.id;
            gameEvent.eventName = record.eventName;
            gameEvent.eventType = eventType;
            gameEvent.displayText = record.displayText ?? string.Empty;
            gameEvent.hiddenText = record.hiddenText ?? string.Empty;
            gameEvent.minYear = Mathf.Max(1, record.minYear);
            gameEvent.maxYear = record.maxYear;
            gameEvent.drawWeight = Mathf.Max(1, record.drawWeight);
            gameEvent.category = category;
            gameEvent.options = ConvertOptions(record.options, record.id);
            gameEvent.immediateEffects = ConvertEffects(record.immediateEffects, record.id);
            error = string.Empty;
            return true;
        }

        private static List<EventOption> ConvertOptions(IReadOnlyList<EventOptionTableRecord> records, string eventId)
        {
            var options = new List<EventOption>();
            if (records == null)
                return options;
            foreach (EventOptionTableRecord record in records)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.optionText) || !TryParse(record.checkType, out CheckType checkType))
                {
                    Debug.LogError($"[ContentTable] 事件 {eventId} 含无效选项。");
                    continue;
                }
                options.Add(new EventOption
                {
                    optionText = record.optionText,
                    checkType = checkType,
                    checkTarget = record.checkTarget,
                    successText = record.successText ?? string.Empty,
                    successEffects = ConvertEffects(record.successEffects, eventId),
                    failText = record.failText ?? string.Empty,
                    failEffects = ConvertEffects(record.failEffects, eventId),
                    alwaysAvailable = record.alwaysAvailable,
                    conditions = ConvertConditions(record.conditions)
                });
            }
            return options;
        }

        private static bool ValidateOptions(IReadOnlyList<EventOptionTableRecord> records)
        {
            if (records == null)
                return true;
            foreach (EventOptionTableRecord record in records)
                if (record == null || string.IsNullOrWhiteSpace(record.optionText) || !TryParse(record.checkType, out CheckType _) || !ValidateEffects(record.successEffects) || !ValidateEffects(record.failEffects) || !ValidateConditions(record.alwaysAvailable, record.conditions))
                    return false;
            return true;
        }

        private static List<EventOptionCondition> ConvertConditions(IReadOnlyList<EventOptionConditionTableRecord> records)
        {
            var conditions = new List<EventOptionCondition>();
            if (records == null) return conditions;
            foreach (EventOptionConditionTableRecord record in records)
            {
                if (record == null || !TryParse(record.conditionKind, out EventOptionConditionKind conditionKind)) continue;
                conditions.Add(new EventOptionCondition { conditionKind = conditionKind, key = record.key ?? string.Empty, value = Mathf.Max(0, record.value), inverted = record.inverted });
            }
            return conditions;
        }

        private static bool ValidateConditions(bool alwaysAvailable, IReadOnlyList<EventOptionConditionTableRecord> records)
        {
            if (alwaysAvailable) return records == null || records.Count == 0;
            if (records == null || records.Count == 0) return false;
            foreach (EventOptionConditionTableRecord record in records)
            {
                if (record == null || !TryParse(record.conditionKind, out EventOptionConditionKind conditionKind)) return false;
                bool requiresKey = conditionKind == EventOptionConditionKind.HasTrait || conditionKind == EventOptionConditionKind.HasAilment || conditionKind == EventOptionConditionKind.MinimumResource || conditionKind == EventOptionConditionKind.HasEquippedItem || conditionKind == EventOptionConditionKind.HasKeyword;
                if (requiresKey && string.IsNullOrWhiteSpace(record.key)) return false;
                if (record.value < 0) return false;
            }
            return true;
        }

        private static bool ValidateEffects(IReadOnlyList<EventEffectTableRecord> records)
        {
            if (records == null)
                return true;
            foreach (EventEffectTableRecord record in records)
            {
                if (record == null || !TryParse(record.effectType, out EventEffectType effectType))
                    return false;
                if (effectType == EventEffectType.ScheduleEvent && !DelayedEventRules.TryCreatePlan(1, record.value, record.targetName, out _, out _))
                    return false;
            }
            return true;
        }

        private static Dictionary<string, EventTableRecord> BuildUniqueTargetRecords(IReadOnlyList<EventTableRecord> records, out HashSet<string> duplicateIds)
        {
            var result = new Dictionary<string, EventTableRecord>(StringComparer.Ordinal);
            duplicateIds = new HashSet<string>(StringComparer.Ordinal);
            if (records == null)
                return result;

            foreach (EventTableRecord record in records)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.id))
                    continue;
                if (!result.TryAdd(record.id, record))
                    duplicateIds.Add(record.id);
            }
            foreach (string duplicateId in duplicateIds)
                result.Remove(duplicateId);
            return result;
        }

        private static bool ValidateScheduleReferences(EventTableRecord source, IReadOnlyDictionary<string, EventTableRecord> targetRecords, out string error)
        {
            error = string.Empty;
            if (source == null)
                return true;

            foreach (EventEffectTableRecord effect in EnumerateEffects(source))
            {
                if (effect == null || !TryParse(effect.effectType, out EventEffectType effectType) || effectType != EventEffectType.ScheduleEvent)
                    continue;
                if (!targetRecords.TryGetValue(effect.targetName ?? string.Empty, out EventTableRecord target) || !TryParse(target.category, out EventCategory category) || category != EventCategory.Scheduled)
                {
                    error = $"事件 {source.id} 引用的延时事件不存在、重复或不是 Scheduled 类别：{effect.targetName}";
                    return false;
                }
            }
            return true;
        }

        private static IEnumerable<EventEffectTableRecord> EnumerateEffects(EventTableRecord record)
        {
            if (record?.immediateEffects != null)
                foreach (EventEffectTableRecord effect in record.immediateEffects)
                    yield return effect;
            if (record?.options == null)
                yield break;

            foreach (EventOptionTableRecord option in record.options)
            {
                if (option?.successEffects != null)
                    foreach (EventEffectTableRecord effect in option.successEffects)
                        yield return effect;
                if (option?.failEffects != null)
                    foreach (EventEffectTableRecord effect in option.failEffects)
                        yield return effect;
            }
        }

        private static List<EventEffect> ConvertEffects(IReadOnlyList<EventEffectTableRecord> records, string eventId)
        {
            var effects = new List<EventEffect>();
            if (records == null)
                return effects;
            foreach (EventEffectTableRecord record in records)
            {
                if (record == null || !TryParse(record.effectType, out EventEffectType effectType))
                {
                    Debug.LogError($"[ContentTable] 事件 {eventId} 含无效效果类型。 ");
                    continue;
                }
                effects.Add(new EventEffect { effectType = effectType, targetName = record.targetName ?? string.Empty, value = record.value, description = record.description ?? string.Empty });
            }
            return effects;
        }

        private static List<EventData> CopyValid(IReadOnlyList<EventData> source)
        {
            var result = new List<EventData>();
            if (source == null)
                return result;
            foreach (EventData gameEvent in source)
                if (gameEvent != null)
                    result.Add(gameEvent);
            return result;
        }

        private static bool TryParse<TEnum>(string value, out TEnum result) where TEnum : struct
        {
            return Enum.TryParse(value, true, out result) && Enum.IsDefined(typeof(TEnum), result);
        }
    }
}
