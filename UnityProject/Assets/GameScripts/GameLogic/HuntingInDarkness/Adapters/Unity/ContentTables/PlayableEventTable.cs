using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Content;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace HuntingInDarkness.ContentTables
{
    [Serializable]
    public sealed class EventEffectTableRecord
    {
        public string effectType;
        public string targetName;
        public string bodyPart;
        public string fatalDeckId;
        public string survivalEventId;
        public int value = 1;
        public string description;
    }

    [Serializable]
    public sealed class EventOptionConditionTableRecord
    {
        public string conditionKind;
        public string key;
        public string displayName;
        public int value;
        public bool inverted;
    }

    [Serializable]
    public sealed class EventOptionTableRecord
    {
        public string optionId;
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

    internal sealed class PlayableEventTableGeneration : IDisposable
    {
        private readonly HashSet<EventData> ownedEvents = new();
        private bool disposed;

        public PlayableEventTableGeneration(List<EventTableRecord> records, PlayableSymptomCatalog symptomCatalog, IHunterBloodlineContent bloodlineContent)
        {
            Records = records ?? new List<EventTableRecord>();
            Events = new List<EventData>();
            SymptomCatalog = symptomCatalog;
            BloodlineContent = bloodlineContent;
        }

        public List<EventTableRecord> Records { get; }
        public List<EventData> Events { get; }
        public PlayableSymptomCatalog SymptomCatalog { get; }
        public IHunterBloodlineContent BloodlineContent { get; }
        public bool IsUsable => !disposed && Events.TrueForAll(gameEvent => gameEvent != null);
        public bool HasErrors => ErrorCount > 0;
        public int ErrorCount { get; private set; }
        public string Diagnostic => HasErrors ? $"事件表世代包含 {ErrorCount} 个无效记录或引用。" : string.Empty;

        public void ReportError(string message)
        {
            ErrorCount++;
            Debug.LogError($"[ContentTable] {message}");
        }

        public void Own(EventData gameEvent)
        {
            if (gameEvent != null)
                ownedEvents.Add(gameEvent);
        }

        public void DestroyOwned(EventData gameEvent)
        {
            if (gameEvent == null || !ownedEvents.Remove(gameEvent)) return;
            DestroyTransientEvent(gameEvent);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Exception firstException = null;
            foreach (EventData gameEvent in new List<EventData>(ownedEvents))
            {
                try
                {
                    DestroyTransientEvent(gameEvent);
                }
                catch (Exception exception)
                {
                    firstException ??= exception;
                }
            }
            ownedEvents.Clear();
            if (firstException != null) throw firstException;
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
    }

    /// <summary>把表数据映射为旧 EventData，并按稳定 ID 支持内容注入与覆盖。</summary>
    public static class PlayableEventTableRuntime
    {
        private const string TablePath = "HuntingInDarkness/Tables/events";
        private const string BloodlineTablePath = "HuntingInDarkness/Tables/bloodline-events";
        private const string CardInteractionTablePath = "HuntingInDarkness/Tables/card-interaction-events";
        private const string HuntTablePath = "HuntingInDarkness/Tables/hunt-events";
        private static List<EventTableRecord> cachedRecords;
        private static PlayableEventTableGeneration currentGeneration;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            PlayableEventTableGeneration retired = SwapGeneration(null);
            RetireGeneration(retired);
            cachedRecords = null;
        }

        /// <summary>清理由事件表运行时创建的事件对象；不会触碰外部资产。</summary>
        public static void ClearCache()
        {
            if (PlayableSettlementContentRuntime.IsEventGenerationLeased(currentGeneration) || PlayableHuntContentRuntime.IsEventGenerationLeased(currentGeneration))
            {
                Debug.LogError("[PlayableEventTable] 活动营地内容计划仍在使用当前事件世代，拒绝清理缓存。");
                return;
            }
            PlayableEventTableGeneration retired = SwapGeneration(null);
            RetireGeneration(retired);
            cachedRecords = null;
        }

        /// <summary>在正式内容装配边界重建事件表缓存。</summary>
        public static IReadOnlyList<EventData> Rebuild()
        {
            if (PlayableSettlementContentRuntime.IsEventGenerationLeased(currentGeneration) || PlayableHuntContentRuntime.IsEventGenerationLeased(currentGeneration))
            {
                Debug.LogError("[PlayableEventTable] 活动营地内容计划仍在使用当前事件世代，拒绝重建缓存。");
                return currentGeneration.Events;
            }
            IReadOnlyList<EventTableRecord> retryRecords = currentGeneration != null && currentGeneration.HasErrors ? currentGeneration.Records : null;
            PlayableEventTableGeneration replacement = BuildGeneration(PlayableSymptomRuntime.Catalog, PlayableBloodlineRuntime.Content, retryRecords, true);
            if (replacement.HasErrors)
            {
                RetireGeneration(replacement);
                return currentGeneration != null ? currentGeneration.Events : Array.Empty<EventData>();
            }
            PlayableEventTableGeneration retired = SwapGeneration(replacement);
            RetireGeneration(retired);
            return replacement.Events;
        }

        public static void Extend(IReadOnlyList<EventData> baseRandomEvents, IReadOnlyList<EventData> baseMainStoryEvents, out List<EventData> randomEvents, out List<EventData> mainStoryEvents)
        {
            Extend(baseRandomEvents, baseMainStoryEvents, GetEvents(), out randomEvents, out mainStoryEvents);
        }

        internal static void Extend(IReadOnlyList<EventData> baseRandomEvents, IReadOnlyList<EventData> baseMainStoryEvents, IReadOnlyList<EventData> tableEvents, out List<EventData> randomEvents, out List<EventData> mainStoryEvents)
        {
            randomEvents = CopyValid(baseRandomEvents);
            mainStoryEvents = CopyValid(baseMainStoryEvents);
            foreach (EventData tableEvent in tableEvents ?? Array.Empty<EventData>())
            {
                randomEvents.RemoveAll(gameEvent => gameEvent.name == tableEvent.name);
                mainStoryEvents.RemoveAll(gameEvent => gameEvent.name == tableEvent.name);
                if (tableEvent.category == EventCategory.MainStory)
                    mainStoryEvents.Add(tableEvent);
                else
                    randomEvents.Add(tableEvent);
            }
        }

        public static List<EventData> ExtendHunt(IReadOnlyList<EventData> baseEvents)
        {
            return ExtendHunt(baseEvents, GetEvents());
        }

        internal static List<EventData> ExtendHunt(IReadOnlyList<EventData> baseEvents, IReadOnlyList<EventData> tableEvents)
        {
            var result = new List<EventData>();
            var knownIds = new HashSet<string>(StringComparer.Ordinal);
            var duplicateBaseIds = new HashSet<string>(StringComparer.Ordinal);
            if (baseEvents != null)
            {
                foreach (EventData baseEvent in baseEvents)
                {
                    if (baseEvent == null || baseEvent.category != EventCategory.Hunt || !baseEvent.HasExplicitContentId) continue;
                    if (!knownIds.Add(baseEvent.ContentId)) duplicateBaseIds.Add(baseEvent.ContentId);
                }
                knownIds.Clear();
                foreach (EventData baseEvent in baseEvents)
                {
                    if (baseEvent == null || baseEvent.category != EventCategory.Hunt || !baseEvent.HasExplicitContentId || duplicateBaseIds.Contains(baseEvent.ContentId) || !knownIds.Add(baseEvent.ContentId)) continue;
                    result.Add(baseEvent);
                }
            }
            foreach (EventData tableEvent in tableEvents ?? Array.Empty<EventData>())
            {
                if (tableEvent == null || tableEvent.category != EventCategory.Hunt || !tableEvent.HasExplicitContentId) continue;
                result.RemoveAll(gameEvent => string.Equals(gameEvent.ContentId, tableEvent.ContentId, StringComparison.Ordinal));
                knownIds.Add(tableEvent.ContentId);
                result.Add(tableEvent);
            }
            return result;
        }

        public static IReadOnlyList<EventData> GetEvents()
        {
            if (currentGeneration != null) return currentGeneration.Events;
            PlayableEventTableGeneration replacement = BuildGeneration(PlayableSymptomRuntime.Catalog, PlayableBloodlineRuntime.Content, cachedRecords, false);
            PlayableEventTableGeneration retired = SwapGeneration(replacement);
            RetireGeneration(retired);
            return replacement.Events;
        }

        internal static PlayableEventTableGeneration PrepareGeneration(PlayableSymptomCatalog symptomCatalog, IHunterBloodlineContent bloodlineContent) => BuildGeneration(symptomCatalog, bloodlineContent, null, true);
        internal static PlayableEventTableGeneration CurrentGeneration => currentGeneration;

        internal static PlayableEventTableGeneration SwapGeneration(PlayableEventTableGeneration replacement)
        {
            PlayableEventTableGeneration previous = currentGeneration;
            currentGeneration = replacement;
            if (replacement != null)
                cachedRecords = new List<EventTableRecord>(replacement.Records);
            return previous;
        }

        internal static void RetireGeneration(PlayableEventTableGeneration generation)
        {
            if (generation == null || ReferenceEquals(generation, currentGeneration)) return;
            generation.Dispose();
        }

        private static PlayableEventTableGeneration BuildGeneration(PlayableSymptomCatalog symptomCatalog, IHunterBloodlineContent bloodlineContent, IReadOnlyList<EventTableRecord> sourceRecords, bool forceReload)
        {
            List<EventTableRecord> records = sourceRecords != null ? new List<EventTableRecord>(sourceRecords) : LoadRecords(forceReload);
            var generation = new PlayableEventTableGeneration(records, symptomCatalog, bloodlineContent);
            try
            {
                var knownIds = new HashSet<string>(StringComparer.Ordinal);
                HashSet<string> duplicateIds = FindDuplicateIds(records);
                var validRecords = new Dictionary<string, EventTableRecord>(StringComparer.Ordinal);
                var eventsById = new Dictionary<string, EventData>(StringComparer.Ordinal);
                var orderedIds = new List<string>();
                var reportedDuplicateIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (EventTableRecord record in records)
                {
                    if (record != null && duplicateIds.Contains(record.id))
                    {
                        if (reportedDuplicateIds.Add(record.id))
                            generation.ReportError($"事件表存在重复 id：{record.id}");
                        continue;
                    }
                    if (!TryCreateEvent(record, knownIds, symptomCatalog, bloodlineContent, out EventData gameEvent, out string error))
                    {
                        generation.ReportError(error);
                        continue;
                    }
                    generation.Own(gameEvent);
                    validRecords.Add(record.id, record);
                    eventsById.Add(record.id, gameEvent);
                    orderedIds.Add(record.id);
                }

                RemoveRecordsWithInvalidReferences(orderedIds, validRecords, eventsById, generation);
                foreach (string eventId in orderedIds)
                {
                    if (!eventsById.TryGetValue(eventId, out EventData gameEvent)) continue;
                    BindEventChains(gameEvent, validRecords[eventId], eventsById);
                    generation.Events.Add(gameEvent);
                }
                return generation;
            }
            catch
            {
                generation.Dispose();
                throw;
            }
        }

        private static List<EventTableRecord> LoadRecords(bool forceReload)
        {
            if (!forceReload && cachedRecords != null) return new List<EventTableRecord>(cachedRecords);
            var records = new List<EventTableRecord>(new JsonEventTableSource(TablePath).Load());
            records.AddRange(new JsonEventTableSource(BloodlineTablePath).Load());
            records.AddRange(new JsonEventTableSource(CardInteractionTablePath).Load());
            records.AddRange(new JsonEventTableSource(HuntTablePath).Load());
            return records;
        }

        private static bool TryCreateEvent(EventTableRecord record, HashSet<string> knownIds, PlayableSymptomCatalog symptomCatalog, IHunterBloodlineContent bloodlineContent, out EventData gameEvent, out string error)
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
            bool allowHuntWorldEffects = category == EventCategory.Hunt;
            bool allowSettlementEventEffects = category == EventCategory.Settlement || category == EventCategory.Random || category == EventCategory.Scheduled || category == EventCategory.Triggered;
            if (!ValidateOptions(record.options, symptomCatalog, bloodlineContent, allowHuntWorldEffects, allowSettlementEventEffects, category != EventCategory.Hunt) || !ValidateEffects(record.immediateEffects, false, symptomCatalog, bloodlineContent, allowHuntWorldEffects, false))
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
            gameEvent.ConfigureContentId(record.id);
            gameEvent.eventName = record.eventName;
            gameEvent.eventType = eventType;
            gameEvent.displayText = record.displayText ?? string.Empty;
            gameEvent.hiddenText = record.hiddenText ?? string.Empty;
            gameEvent.minYear = Mathf.Max(1, record.minYear);
            gameEvent.maxYear = record.maxYear;
            gameEvent.drawWeight = Mathf.Max(1, record.drawWeight);
            gameEvent.category = category;
            gameEvent.options = ConvertOptions(record.options, record.id, symptomCatalog, bloodlineContent);
            gameEvent.immediateEffects = ConvertEffects(record.immediateEffects, record.id);
            error = string.Empty;
            return true;
        }

        private static List<EventOption> ConvertOptions(IReadOnlyList<EventOptionTableRecord> records, string eventId, PlayableSymptomCatalog symptomCatalog, IHunterBloodlineContent bloodlineContent)
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
                    optionId = record.optionId?.Trim() ?? string.Empty,
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
                    conditions = ConvertConditions(record.conditions, symptomCatalog, bloodlineContent)
                });
            }
            return options;
        }

        private static bool ValidateOptions(IReadOnlyList<EventOptionTableRecord> records, PlayableSymptomCatalog symptomCatalog, IHunterBloodlineContent bloodlineContent, bool allowHuntWorldEffects, bool allowSettlementEventEffects, bool requireOptionIds)
        {
            if (records == null)
                return true;
            var optionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (EventOptionTableRecord record in records)
                if (record == null || string.IsNullOrWhiteSpace(record.optionText) || (requireOptionIds && (string.IsNullOrWhiteSpace(record.optionId) || !optionIds.Add(record.optionId.Trim()))) || !TryParse(record.checkType, out CheckType checkType) || !TryParseCheckPresentation(record.checkPresentation, out EventCheckPresentationKind checkPresentation) || !ValidateCheckPresentation(record, checkType, checkPresentation) || !ValidateEffects(record.successEffects, true, symptomCatalog, bloodlineContent, allowHuntWorldEffects, allowSettlementEventEffects) || !ValidateEffects(record.failEffects, true, symptomCatalog, bloodlineContent, allowHuntWorldEffects, allowSettlementEventEffects) || !ValidateConditions(record.alwaysAvailable, record.conditions, symptomCatalog, bloodlineContent, allowHuntWorldEffects) || !ValidateCarriedItemCosts(record, allowHuntWorldEffects))
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
            if (presentation == EventCheckPresentationKind.OldMaid && count != 1) return false;
            return !string.IsNullOrWhiteSpace(record.checkDeckId);
        }

        private static List<EventOptionCondition> ConvertConditions(IReadOnlyList<EventOptionConditionTableRecord> records, PlayableSymptomCatalog symptomCatalog, IHunterBloodlineContent bloodlineContent)
        {
            var conditions = new List<EventOptionCondition>();
            if (records == null) return conditions;
            foreach (EventOptionConditionTableRecord record in records)
            {
                if (record == null || !TryParse(record.conditionKind, out EventOptionConditionKind conditionKind)) continue;
                string displayName = string.IsNullOrWhiteSpace(record.displayName) ? record.key ?? string.Empty : record.displayName.Trim();
                if (conditionKind == EventOptionConditionKind.HasAilment && symptomCatalog != null && symptomCatalog.TryGetById(record.key, out SymptomDefinition symptom))
                    displayName = symptom.DisplayName;
                if ((conditionKind == EventOptionConditionKind.HasBloodline || conditionKind == EventOptionConditionKind.HasActiveBloodline) && bloodlineContent != null && bloodlineContent.TryGet(record.key, out HunterBloodlineDefinition bloodline))
                    displayName = bloodline.DisplayName;
                conditions.Add(new EventOptionCondition { conditionKind = conditionKind, key = record.key ?? string.Empty, displayName = displayName, value = Mathf.Max(0, record.value), inverted = record.inverted });
            }
            return conditions;
        }

        private static bool ValidateConditions(bool alwaysAvailable, IReadOnlyList<EventOptionConditionTableRecord> records, PlayableSymptomCatalog symptomCatalog, IHunterBloodlineContent bloodlineContent, bool allowHuntItemConditions)
        {
            if (alwaysAvailable) return records == null || records.Count == 0;
            if (records == null || records.Count == 0) return false;
            foreach (EventOptionConditionTableRecord record in records)
            {
                if (record == null || !TryParse(record.conditionKind, out EventOptionConditionKind conditionKind)) return false;
                bool requiresKey = conditionKind == EventOptionConditionKind.HasTrait || conditionKind == EventOptionConditionKind.HasAilment || conditionKind == EventOptionConditionKind.MinimumResource || conditionKind == EventOptionConditionKind.HasEquippedItem || conditionKind == EventOptionConditionKind.HasKeyword || conditionKind == EventOptionConditionKind.HasBloodline || conditionKind == EventOptionConditionKind.HasActiveBloodline || conditionKind == EventOptionConditionKind.MinimumCarriedItem;
                if (requiresKey && string.IsNullOrWhiteSpace(record.key)) return false;
                if (conditionKind == EventOptionConditionKind.MinimumCarriedItem && (!allowHuntItemConditions || record.value <= 0)) return false;
                if (conditionKind == EventOptionConditionKind.HasAilment && (symptomCatalog == null || !symptomCatalog.TryGetById(record.key, out _))) return false;
                if ((conditionKind == EventOptionConditionKind.HasBloodline || conditionKind == EventOptionConditionKind.HasActiveBloodline) && (bloodlineContent == null || !bloodlineContent.TryGet(record.key, out _))) return false;
                if (record.value < 0) return false;
            }
            return true;
        }

        private static bool ValidateEffects(IReadOnlyList<EventEffectTableRecord> records, bool allowSelectedHunterEffects, PlayableSymptomCatalog symptomCatalog, IHunterBloodlineContent bloodlineContent, bool allowHuntWorldEffects, bool allowSettlementEventEffects)
        {
            if (records == null)
                return true;
            bool removesItem = false;
            bool killsHunter = false;
            foreach (EventEffectTableRecord record in records)
            {
                if (record == null || !TryParse(record.effectType, out EventEffectType effectType))
                    return false;
                if (effectType == EventEffectType.AdvanceYear)
                    return false;
                if (effectType == EventEffectType.ScheduleEvent && !DelayedEventRules.TryCreatePlan(1, record.value, record.targetName, out _, out _))
                    return false;
                if (effectType == EventEffectType.ActivateBloodline && (bloodlineContent == null || !bloodlineContent.TryGet(record.targetName, out _)))
                    return false;
                if (effectType == EventEffectType.AddAilment && (symptomCatalog == null || !symptomCatalog.TryGetById(record.targetName, out _)))
                    return false;
                if (effectType == EventEffectType.AddRecoverableWound && (!allowSelectedHunterEffects || !string.Equals(record.targetName?.Trim(), "selected", StringComparison.OrdinalIgnoreCase) || record.value <= 0 || !HunterRecoveryRules.TryParseBodyPart(record.bodyPart, out _)))
                    return false;
                if (effectType == EventEffectType.KillHunter && (!allowSelectedHunterEffects || !IsValidHunterDeathCauseId(record.targetName)))
                    return false;
                if (effectType == EventEffectType.KillHunter)
                    killsHunter = true;
                if (effectType == EventEffectType.FatalInjury && (!allowHuntWorldEffects || !allowSelectedHunterEffects || !string.Equals(record.targetName?.Trim(), "selected", StringComparison.OrdinalIgnoreCase) || record.value <= 0 || !HunterRecoveryRules.TryParseBodyPart(record.bodyPart, out _) || !EventFatalInjuryRules.IsValidDeckId(record.fatalDeckId)))
                    return false;
                if (effectType == EventEffectType.FatalInjury && records.Count != 1)
                    return false;
                if (effectType == EventEffectType.ExhaustCurrentHuntTileResources && (!allowHuntWorldEffects || !string.IsNullOrWhiteSpace(record.targetName) || !string.IsNullOrWhiteSpace(record.bodyPart) || record.value != 0))
                    return false;
                if (effectType == EventEffectType.CreateHuntNoiseLease && (!allowSettlementEventEffects || string.IsNullOrWhiteSpace(record.targetName) || !string.IsNullOrWhiteSpace(record.bodyPart) || record.value < 1 || record.value > 10))
                    return false;
                if (effectType == EventEffectType.AddItem && (!allowHuntWorldEffects || string.IsNullOrWhiteSpace(record.targetName) || !string.IsNullOrWhiteSpace(record.bodyPart) || record.value <= 0))
                    return false;
                if (effectType == EventEffectType.RemoveItem && (!allowHuntWorldEffects || !allowSelectedHunterEffects || string.IsNullOrWhiteSpace(record.targetName) || !string.IsNullOrWhiteSpace(record.bodyPart) || record.value <= 0))
                    return false;
                if (effectType == EventEffectType.RemoveItem)
                    removesItem = true;
                if (effectType == EventEffectType.RescuePopulation && (!allowHuntWorldEffects || !string.IsNullOrWhiteSpace(record.targetName) || !string.IsNullOrWhiteSpace(record.bodyPart) || record.value <= 0))
                    return false;
            }
            return !removesItem || !killsHunter;
        }

        private static bool ValidateCarriedItemCosts(EventOptionTableRecord record, bool allowHuntItemCosts)
        {
            if (!TryCollectItemCosts(record.successEffects, out Dictionary<string, int> successCosts) || !TryCollectItemCosts(record.failEffects, out Dictionary<string, int> failCosts)) return false;
            if (successCosts.Count == 0 && failCosts.Count == 0) return true;
            if (!allowHuntItemCosts || record.conditions == null) return false;
            var thresholds = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (EventOptionConditionTableRecord condition in record.conditions)
            {
                if (condition == null || condition.inverted || !TryParse(condition.conditionKind, out EventOptionConditionKind kind) || kind != EventOptionConditionKind.MinimumCarriedItem) continue;
                string itemId = condition.key?.Trim() ?? string.Empty;
                if (itemId.Length == 0) continue;
                int oldThreshold = thresholds.TryGetValue(itemId, out int value) ? value : 0;
                if (condition.value > oldThreshold) thresholds[itemId] = condition.value;
            }
            return CoversItemCosts(successCosts, thresholds) && CoversItemCosts(failCosts, thresholds);
        }

        private static bool TryCollectItemCosts(IReadOnlyList<EventEffectTableRecord> effects, out Dictionary<string, int> costs)
        {
            costs = new Dictionary<string, int>(StringComparer.Ordinal);
            if (effects == null) return true;
            foreach (EventEffectTableRecord effect in effects)
            {
                if (effect == null || !TryParse(effect.effectType, out EventEffectType effectType) || effectType != EventEffectType.RemoveItem) continue;
                string itemId = effect.targetName?.Trim() ?? string.Empty;
                int oldCost = costs.TryGetValue(itemId, out int value) ? value : 0;
                if (effect.value <= 0 || oldCost > int.MaxValue - effect.value) return false;
                costs[itemId] = oldCost + effect.value;
            }
            return true;
        }

        private static bool CoversItemCosts(IReadOnlyDictionary<string, int> costs, IReadOnlyDictionary<string, int> thresholds)
        {
            foreach (KeyValuePair<string, int> cost in costs)
                if (!thresholds.TryGetValue(cost.Key, out int threshold) || threshold < cost.Value)
                    return false;
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

        private static void RemoveRecordsWithInvalidReferences(IReadOnlyList<string> orderedIds, Dictionary<string, EventTableRecord> validRecords, Dictionary<string, EventData> eventsById, PlayableEventTableGeneration generation)
        {
            bool removedAny;
            do
            {
                removedAny = false;
                foreach (string eventId in orderedIds)
                {
                    if (!validRecords.TryGetValue(eventId, out EventTableRecord record)) continue;
                    if (ValidateReferences(record, validRecords, out string error)) continue;
                    generation.ReportError(error);
                    validRecords.Remove(eventId);
                    if (eventsById.Remove(eventId, out EventData invalidEvent))
                        generation.DestroyOwned(invalidEvent);
                    removedAny = true;
                }
            } while (removedAny);
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
            if (!ValidateFatalSurvivalReferences(source.id, EnumerateEffects(source), targetRecords, out error))
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

        private static bool ValidateFatalSurvivalReferences(string sourceId, IEnumerable<EventEffectTableRecord> effects, IReadOnlyDictionary<string, EventTableRecord> targetRecords, out string error)
        {
            foreach (EventEffectTableRecord effect in effects)
            {
                if (effect == null || !TryParse(effect.effectType, out EventEffectType effectType) || effectType != EventEffectType.FatalInjury) continue;
                string survivalEventId = effect.survivalEventId?.Trim() ?? string.Empty;
                if (survivalEventId.Length == 0 || string.Equals(sourceId, survivalEventId, StringComparison.Ordinal) || !targetRecords.TryGetValue(survivalEventId, out EventTableRecord target) || !TryParse(target.category, out EventCategory category) || category != EventCategory.Triggered)
                {
                    error = $"事件 {sourceId} 的致命伤存活事件引用缺失、重复、自引用或不是 Triggered 类别：{effect.survivalEventId}";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private static void BindEventChains(EventData gameEvent, EventTableRecord record, IReadOnlyDictionary<string, EventData> eventsById)
        {
            gameEvent.chainedEvents = ResolveEventChain(record.chainedEventIds, eventsById);
            BindFatalSurvivalEvents(gameEvent.immediateEffects, record.immediateEffects, eventsById);
            for (int optionIndex = 0; optionIndex < gameEvent.options.Count; optionIndex++)
            {
                EventOptionTableRecord optionRecord = record.options[optionIndex];
                gameEvent.options[optionIndex].successChain = ResolveEventChain(optionRecord.successChainIds, eventsById);
                gameEvent.options[optionIndex].failChain = ResolveEventChain(optionRecord.failChainIds, eventsById);
                BindFatalSurvivalEvents(gameEvent.options[optionIndex].successEffects, optionRecord.successEffects, eventsById);
                BindFatalSurvivalEvents(gameEvent.options[optionIndex].failEffects, optionRecord.failEffects, eventsById);
            }
        }

        private static void BindFatalSurvivalEvents(IReadOnlyList<EventEffect> effects, IReadOnlyList<EventEffectTableRecord> records, IReadOnlyDictionary<string, EventData> eventsById)
        {
            for (int index = 0; index < (effects?.Count ?? 0) && index < (records?.Count ?? 0); index++)
            {
                EventEffect effect = effects[index];
                EventEffectTableRecord record = records[index];
                if (effect?.effectType != EventEffectType.FatalInjury || string.IsNullOrWhiteSpace(record?.survivalEventId)) continue;
                if (eventsById.TryGetValue(record.survivalEventId.Trim(), out EventData survivalEvent))
                    effect.SurvivalEvent = survivalEvent;
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
                effects.Add(new EventEffect { effectType = effectType, targetName = record.targetName ?? string.Empty, bodyPart = record.bodyPart ?? string.Empty, fatalDeckId = record.fatalDeckId ?? string.Empty, survivalEventId = record.survivalEventId ?? string.Empty, value = record.value, description = record.description ?? string.Empty });
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
