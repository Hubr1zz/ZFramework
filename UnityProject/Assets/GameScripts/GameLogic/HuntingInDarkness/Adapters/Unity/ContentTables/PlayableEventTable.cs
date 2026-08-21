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
        public string checkPresentation = "PhysicalDice";
        public int checkCount = 1;
        public int checkSides = 10;
        public string checkDeckId;
        public string checkInstruction;
        public string successText;
        public List<EventEffectTableRecord> successEffects = new();
        public List<string> successChainIds = new();
        public string failText;
        public List<EventEffectTableRecord> failEffects = new();
        public List<string> failChainIds = new();
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
        public List<string> chainedEventIds = new();
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
        private const string BloodlineTablePath = "HuntingInDarkness/Tables/bloodline-events";
        private const string CardInteractionTablePath = "HuntingInDarkness/Tables/card-interaction-events";
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

            if (cachedRecords == null)
            {
                var source = new JsonEventTableSource(TablePath);
                cachedRecords = new List<EventTableRecord>(source.Load());
                cachedRecords.AddRange(new JsonEventTableSource(BloodlineTablePath).Load());
                cachedRecords.AddRange(new JsonEventTableSource(CardInteractionTablePath).Load());
            }
            cachedEvents = new List<EventData>();
            var knownIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> duplicateIds = FindDuplicateIds(cachedRecords);
            var validRecords = new Dictionary<string, EventTableRecord>(StringComparer.Ordinal);
            var eventsById = new Dictionary<string, EventData>(StringComparer.Ordinal);
            var orderedIds = new List<string>();
            var reportedDuplicateIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (EventTableRecord record in cachedRecords)
            {
                if (record != null && duplicateIds.Contains(record.id))
                {
                    if (reportedDuplicateIds.Add(record.id))
                        Debug.LogError($"[ContentTable] 事件表存在重复 id：{record.id}");
                    continue;
                }
                if (!TryCreateEvent(record, knownIds, out EventData gameEvent, out string error))
                {
                    Debug.LogError($"[ContentTable] {error}");
                    continue;
                }
                validRecords.Add(record.id, record);
                eventsById.Add(record.id, gameEvent);
                orderedIds.Add(record.id);
            }

            RemoveRecordsWithInvalidReferences(orderedIds, validRecords, eventsById);
            foreach (string eventId in orderedIds)
            {
                if (!eventsById.TryGetValue(eventId, out EventData gameEvent)) continue;
                BindEventChains(gameEvent, validRecords[eventId], eventsById);
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
            if (!ValidateOptions(record.options) || !ValidateEffects(record.immediateEffects, false))
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
                if (record == null || string.IsNullOrWhiteSpace(record.optionText) || !TryParse(record.checkType, out CheckType checkType) || !TryParseCheckPresentation(record.checkPresentation, out EventCheckPresentationKind checkPresentation))
                {
                    Debug.LogError($"[ContentTable] 事件 {eventId} 含无效选项。");
                    continue;
                }
                options.Add(new EventOption
                {
                    optionText = record.optionText,
                    checkType = checkType,
                    checkTarget = record.checkTarget,
                    checkPresentation = checkPresentation,
                    checkCount = record.checkCount == 0 ? 1 : record.checkCount,
                    checkSides = record.checkSides == 0 ? 10 : record.checkSides,
                    checkDeckId = record.checkDeckId ?? string.Empty,
                    checkInstruction = record.checkInstruction ?? string.Empty,
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
                if (record == null || string.IsNullOrWhiteSpace(record.optionText) || !TryParse(record.checkType, out CheckType checkType) || !TryParseCheckPresentation(record.checkPresentation, out EventCheckPresentationKind checkPresentation) || !ValidateCheckPresentation(record, checkType, checkPresentation) || !ValidateEffects(record.successEffects, true) || !ValidateEffects(record.failEffects, true) || !ValidateConditions(record.alwaysAvailable, record.conditions))
                    return false;
            return true;
        }

        private static bool ValidateCheckPresentation(EventOptionTableRecord record, CheckType checkType, EventCheckPresentationKind presentation)
        {
            if (checkType == CheckType.None) return true;
            int count = record.checkCount == 0 ? 1 : record.checkCount;
            int sides = record.checkSides == 0 ? 10 : record.checkSides;
            if (count < 1 || count > 12 || sides < 2 || sides > 20 || count > sides) return false;
            if (presentation == EventCheckPresentationKind.PhysicalDice) return sides == 6 || sides == 10;
            return !string.IsNullOrWhiteSpace(record.checkDeckId);
        }

        private static List<EventOptionCondition> ConvertConditions(IReadOnlyList<EventOptionConditionTableRecord> records)
        {
            var conditions = new List<EventOptionCondition>();
            if (records == null) return conditions;
            foreach (EventOptionConditionTableRecord record in records)
            {
                if (record == null || !TryParse(record.conditionKind, out EventOptionConditionKind conditionKind)) continue;
                string displayName = record.key ?? string.Empty;
                if ((conditionKind == EventOptionConditionKind.HasBloodline || conditionKind == EventOptionConditionKind.HasActiveBloodline) && PlayableBloodlineRuntime.Content.TryGet(record.key, out HunterBloodlineDefinition bloodline))
                    displayName = bloodline.DisplayName;
                conditions.Add(new EventOptionCondition { conditionKind = conditionKind, key = record.key ?? string.Empty, displayName = displayName, value = Mathf.Max(0, record.value), inverted = record.inverted });
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
                bool requiresKey = conditionKind == EventOptionConditionKind.HasTrait || conditionKind == EventOptionConditionKind.HasAilment || conditionKind == EventOptionConditionKind.MinimumResource || conditionKind == EventOptionConditionKind.HasEquippedItem || conditionKind == EventOptionConditionKind.HasKeyword || conditionKind == EventOptionConditionKind.HasBloodline || conditionKind == EventOptionConditionKind.HasActiveBloodline;
                if (requiresKey && string.IsNullOrWhiteSpace(record.key)) return false;
                if ((conditionKind == EventOptionConditionKind.HasBloodline || conditionKind == EventOptionConditionKind.HasActiveBloodline) && !PlayableBloodlineRuntime.Content.TryGet(record.key, out _)) return false;
                if (record.value < 0) return false;
            }
            return true;
        }

        private static bool ValidateEffects(IReadOnlyList<EventEffectTableRecord> records, bool allowHunterDeath)
        {
            if (records == null)
                return true;
            foreach (EventEffectTableRecord record in records)
            {
                if (record == null || !TryParse(record.effectType, out EventEffectType effectType))
                    return false;
                if (effectType == EventEffectType.ScheduleEvent && !DelayedEventRules.TryCreatePlan(1, record.value, record.targetName, out _, out _))
                    return false;
                if (effectType == EventEffectType.ActivateBloodline && !PlayableBloodlineRuntime.Content.TryGet(record.targetName, out _))
                    return false;
                if (effectType == EventEffectType.KillHunter && (!allowHunterDeath || !IsValidHunterDeathCauseId(record.targetName)))
                    return false;
            }
            return true;
        }

        private static bool IsValidHunterDeathCauseId(string causeId)
        {
            if (string.IsNullOrWhiteSpace(causeId)) return false;
            string normalized = causeId.Trim();
            if (normalized.Length > 64) return false;
            foreach (char character in normalized)
                if (char.IsControl(character))
                    return false;
            return true;
        }

        private static HashSet<string> FindDuplicateIds(IReadOnlyList<EventTableRecord> records)
        {
            var knownIds = new HashSet<string>(StringComparer.Ordinal);
            var duplicateIds = new HashSet<string>(StringComparer.Ordinal);
            if (records == null)
                return duplicateIds;

            foreach (EventTableRecord record in records)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.id))
                    continue;
                if (!knownIds.Add(record.id))
                    duplicateIds.Add(record.id);
            }
            return duplicateIds;
        }

        private static void RemoveRecordsWithInvalidReferences(IReadOnlyList<string> orderedIds, Dictionary<string, EventTableRecord> validRecords, Dictionary<string, EventData> eventsById)
        {
            bool removedAny;
            do
            {
                removedAny = false;
                foreach (string eventId in orderedIds)
                {
                    if (!validRecords.TryGetValue(eventId, out EventTableRecord record)) continue;
                    if (ValidateReferences(record, validRecords, out string error)) continue;
                    Debug.LogError($"[ContentTable] {error}");
                    validRecords.Remove(eventId);
                    if (eventsById.Remove(eventId, out EventData invalidEvent))
                        DestroyTransientEvent(invalidEvent);
                    removedAny = true;
                }
            } while (removedAny);
        }

        private static void DestroyTransientEvent(EventData gameEvent)
        {
            if (gameEvent == null) return;
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(gameEvent);
                return;
            }
            UnityEngine.Object.DestroyImmediate(gameEvent);
        }

        private static bool ValidateReferences(EventTableRecord source, IReadOnlyDictionary<string, EventTableRecord> targetRecords, out string error)
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
            if (!ValidateChainReferences(source.id, "事件结束", source.chainedEventIds, targetRecords, out error))
                return false;
            if (source.options == null)
                return true;
            for (int optionIndex = 0; optionIndex < source.options.Count; optionIndex++)
            {
                EventOptionTableRecord option = source.options[optionIndex];
                if (option == null) continue;
                if (!ValidateChainReferences(source.id, $"选项 {optionIndex + 1} 成功", option.successChainIds, targetRecords, out error))
                    return false;
                if (!ValidateChainReferences(source.id, $"选项 {optionIndex + 1} 失败", option.failChainIds, targetRecords, out error))
                    return false;
            }
            return true;
        }

        private static bool ValidateChainReferences(string sourceId, string branchName, IReadOnlyList<string> targetIds, IReadOnlyDictionary<string, EventTableRecord> targetRecords, out string error)
        {
            error = string.Empty;
            if (targetIds == null)
                return true;
            foreach (string targetId in targetIds)
            {
                if (!string.IsNullOrWhiteSpace(targetId) && targetRecords.TryGetValue(targetId, out EventTableRecord target) && TryParse(target.category, out EventCategory category) && category == EventCategory.Triggered) continue;
                error = $"事件 {sourceId} 的{branchName}分支引用不存在、重复、无效或不是 Triggered 类别：{targetId}";
                return false;
            }
            return true;
        }

        private static void BindEventChains(EventData gameEvent, EventTableRecord record, IReadOnlyDictionary<string, EventData> eventsById)
        {
            gameEvent.chainedEvents = ResolveEventChain(record.chainedEventIds, eventsById);
            for (int optionIndex = 0; optionIndex < gameEvent.options.Count; optionIndex++)
            {
                EventOptionTableRecord optionRecord = record.options[optionIndex];
                gameEvent.options[optionIndex].successChain = ResolveEventChain(optionRecord.successChainIds, eventsById);
                gameEvent.options[optionIndex].failChain = ResolveEventChain(optionRecord.failChainIds, eventsById);
            }
        }

        private static List<EventData> ResolveEventChain(IReadOnlyList<string> targetIds, IReadOnlyDictionary<string, EventData> eventsById)
        {
            var result = new List<EventData>();
            if (targetIds == null)
                return result;
            foreach (string targetId in targetIds)
                result.Add(eventsById[targetId]);
            return result;
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

        private static bool TryParseCheckPresentation(string value, out EventCheckPresentationKind result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result = EventCheckPresentationKind.PhysicalDice;
                return true;
            }
            return TryParse(value, out result);
        }
    }
}
