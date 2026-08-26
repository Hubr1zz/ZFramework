using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using HuntingInDarkness.Data;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    internal sealed class PlayableSettlementContentOwnership : IDisposable
    {
        private readonly List<UnityEngine.Object> ownedObjects = new();
        private bool transferred;

        public void OwnRange<T>(IReadOnlyList<T> objects) where T : UnityEngine.Object
        {
            if (transferred || objects == null) return;
            foreach (T ownedObject in objects)
                if (ownedObject != null && !ownedObjects.Contains(ownedObject))
                    ownedObjects.Add(ownedObject);
        }

        public List<UnityEngine.Object> Objects => ownedObjects;

        public void Transfer()
        {
            transferred = true;
        }

        public void Dispose()
        {
            if (transferred) return;
            Exception firstException = null;
            foreach (UnityEngine.Object ownedObject in new List<UnityEngine.Object>(ownedObjects))
            {
                try
                {
                    if (ownedObject == null) continue;
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(ownedObject);
                    else
                        UnityEngine.Object.DestroyImmediate(ownedObject);
                }
                catch (Exception exception)
                {
                    firstException ??= exception;
                }
            }
            ownedObjects.Clear();
            if (firstException != null) throw firstException;
        }
    }

    /// <summary>战役级营地内容世代；只拥有表生成的瞬态 Unity 对象，不拥有序列化资产或事件世代。</summary>
    internal sealed class PlayableSettlementContentPlan : IDisposable
    {
        private readonly List<UnityEngine.Object> ownedObjects;
        private readonly List<StartingResourceSnapshot> startingResources;
        private readonly int deathInspirationGrowth;
        private readonly int deathInspirationMinimumAge;
        private bool retired;

        public PlayableSettlementContentPlan(PlayableSettlementContentCatalog sourceCatalog, PlayableSettlementRegistryBundle registryBundle, PlayableTraitCatalog traitCatalog, PlayableEventTableGeneration eventGeneration, CampaignCalendarDefinition calendar, IReadOnlyDictionary<string, CampaignCalendarDefinition> calendars, List<CraftRecipe> recipes, List<EventData> randomEvents, List<EventData> mainStoryEvents, List<HunterData> startingHunters, List<HunterData> recruitmentTemplates, IReadOnlyList<StartingResourceDefinition> resources, List<UnityEngine.Object> ownedObjects, int deathInspirationGrowth, int deathInspirationMinimumAge)
        {
            SourceCatalog = sourceCatalog;
            RegistryBundle = registryBundle ?? throw new ArgumentNullException(nameof(registryBundle));
            TraitCatalog = traitCatalog ?? throw new ArgumentNullException(nameof(traitCatalog));
            EventGeneration = eventGeneration;
            Calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
            if (calendars == null || !calendars.ContainsKey(Calendar.CalendarId)) throw new ArgumentException("战役日历支持列表缺少默认日历。", nameof(calendars));
            Calendars = new ReadOnlyDictionary<string, CampaignCalendarDefinition>(new Dictionary<string, CampaignCalendarDefinition>(calendars, StringComparer.Ordinal));
            Items = registryBundle.Items;
            Inventions = registryBundle.Inventions;
            Recipes = recipes.AsReadOnly();
            RandomEvents = randomEvents.AsReadOnly();
            MainStoryEvents = mainStoryEvents.AsReadOnly();
            Events = registryBundle.Events;
            StartingHunters = startingHunters.AsReadOnly();
            RecruitmentTemplates = recruitmentTemplates.AsReadOnly();
            startingResources = new List<StartingResourceSnapshot>();
            foreach (StartingResourceDefinition resource in resources ?? Array.Empty<StartingResourceDefinition>())
                if (resource?.Item != null && resource.Amount > 0)
                    startingResources.Add(new StartingResourceSnapshot(resource.Item, resource.Amount));
            this.ownedObjects = ownedObjects ?? new List<UnityEngine.Object>();
            this.deathInspirationGrowth = Mathf.Max(0, deathInspirationGrowth);
            this.deathInspirationMinimumAge = Mathf.Max(1, deathInspirationMinimumAge);
        }

        public PlayableSettlementContentCatalog SourceCatalog { get; }
        internal PlayableSettlementRegistryBundle RegistryBundle { get; }
        internal PlayableTraitCatalog TraitCatalog { get; }
        internal PlayableEventTableGeneration EventGeneration { get; }
        public CampaignCalendarDefinition Calendar { get; }
        public IReadOnlyDictionary<string, CampaignCalendarDefinition> Calendars { get; }
        public IReadOnlyList<ItemData> Items { get; }
        public IReadOnlyList<InventionData> Inventions { get; }
        public IReadOnlyList<CraftRecipe> Recipes { get; }
        public IReadOnlyList<EventData> RandomEvents { get; }
        public IReadOnlyList<EventData> MainStoryEvents { get; }
        public IReadOnlyList<EventData> Events { get; }
        public IReadOnlyList<HunterData> StartingHunters { get; }
        public IReadOnlyList<HunterData> RecruitmentTemplates { get; }
        public bool IsRetired => retired;

        public bool TryApplyTo(SettlementManager manager, out string reason)
        {
            if (retired)
            {
                reason = "营地内容世代已经退役。";
                return false;
            }
            if (manager == null)
            {
                reason = "营地管理器为空。";
                return false;
            }
            if (manager.Data.ItemIdentitySchemaVersion > PlayableSettlementItemRegistry.CurrentIdentitySchemaVersion || manager.Data.TraitIdentitySchemaVersion > PlayableTraitRegistry.CurrentIdentitySchemaVersion || manager.Data.InventionIdentitySchemaVersion > PlayableSettlementInventionRegistry.CurrentIdentitySchemaVersion || manager.Data.TimelineEventIdentitySchemaVersion > PlayableSettlementEventRegistry.CurrentIdentitySchemaVersion || manager.Data.CampaignPacingSchemaVersion > SettlementInstance.CurrentCampaignPacingSchemaVersion || manager.Data.SettlementModifierSchemaVersion > PlayableSettlementModifierRuntime.CurrentSchemaVersion || manager.Data.MaterialDiscoverySchemaVersion > SettlementInstance.CurrentMaterialDiscoverySchemaVersion || manager.Data.EventMemorySchemaVersion > SettlementInstance.CurrentEventMemorySchemaVersion)
            {
                reason = "营地存档 schema 高于当前内容版本。";
                return false;
            }

            if (!TryResolveCalendar(manager.Data, out CampaignCalendarDefinition selectedCalendar, out reason)) return false;
            if (!manager.Timeline.TryBindCalendar(selectedCalendar, out reason)) return false;
            if (!MigrateCampaignPacing(manager, selectedCalendar, out reason)) return false;
            manager.HunterMgmt.ConfigureDeathInspiration(deathInspirationGrowth, deathInspirationMinimumAge);
            PlayableSettlementItemRegistry.MigratePersistentState(manager.Data);
            PlayableTraitRegistry.MigratePersistentState(manager.Data);
            MigrateMaterialDiscovery(manager.Data);
            MigrateEventMemories(manager.Data);
            PlayableSettlementInventionRegistry.MigratePersistentState(manager.Data);
            PlayableSettlementEventRegistry.MigratePersistentState(manager.Data);
            manager.Timeline.RandomEventPool = new List<EventData>(RandomEvents);
            manager.Timeline.MainStoryEvents = new List<EventData>(MainStoryEvents);
            manager.Inventions.AllInventions = new List<InventionData>(Inventions);
            if (!PlayableSettlementModifierRuntime.Synchronize(manager.Data, manager.Inventions.AllInventions, message => Debug.LogError($"[SettlementManager] {message}")))
            {
                reason = "营地持续修正无法投影到当前存档。";
                return false;
            }
            manager.Workshop.AllRecipes = new List<CraftRecipe>(Recipes);
            if (manager.Data.Hunters.Count > 0)
            {
                SynchronizeExistingRoster(manager);
                reason = string.Empty;
                return true;
            }

            foreach (HunterData hunter in StartingHunters)
                if (hunter != null)
                    manager.HunterMgmt.AddStartingHunter(hunter.hunterName, hunter);
            foreach (StartingResourceSnapshot resource in startingResources)
            {
                manager.Data.AddResource(resource.Item, resource.Amount);
                manager.Data.DiscoverMaterial(resource.Item.ContentId);
            }
            SynchronizeExistingRoster(manager);
            reason = manager.Data.Hunters.Count > 0 ? string.Empty : "营地内容没有产生可用的初始猎人。";
            return manager.Data.Hunters.Count > 0;
        }

        public bool TryResolveCalendar(string calendarId, out CampaignCalendarDefinition definition)
        {
            definition = null;
            return !string.IsNullOrWhiteSpace(calendarId) && Calendars.TryGetValue(calendarId, out definition);
        }

        private bool TryResolveCalendar(SettlementInstance data, out CampaignCalendarDefinition definition, out string reason)
        {
            definition = null;
            reason = string.Empty;
            if (data == null)
            {
                reason = "营地存档为空。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(data.CampaignCalendarId))
            {
                definition = Calendar;
                return true;
            }
            if (TryResolveCalendar(data.CampaignCalendarId, out definition)) return true;
            reason = $"营地存档引用未知或不支持的战役日历：{data.CampaignCalendarId}";
            return false;
        }

        private static void MigrateMaterialDiscovery(SettlementInstance settlement)
        {
            settlement.DiscoveredMaterialIds ??= new List<string>();
            var normalizedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string materialId in settlement.DiscoveredMaterialIds)
            {
                string normalizedId = PlayableSettlementItemRegistry.ResolveContentId(materialId);
                if (!string.IsNullOrWhiteSpace(normalizedId)) normalizedIds.Add(normalizedId);
            }
            if (settlement.MaterialDiscoverySchemaVersion < SettlementInstance.CurrentMaterialDiscoverySchemaVersion)
                foreach (ResourceEntry resource in settlement.Resources ?? new List<ResourceEntry>())
                {
                    string normalizedId = PlayableSettlementItemRegistry.ResolveContentId(resource?.Key);
                    if (resource != null && resource.Value > 0 && !string.IsNullOrWhiteSpace(normalizedId)) normalizedIds.Add(normalizedId);
                }
            var orderedIds = new List<string>(normalizedIds);
            orderedIds.Sort(StringComparer.Ordinal);
            settlement.DiscoveredMaterialIds = orderedIds;
            settlement.MaterialDiscoverySchemaVersion = SettlementInstance.CurrentMaterialDiscoverySchemaVersion;
        }

        private static void MigrateEventMemories(SettlementInstance settlement)
        {
            settlement.EventMemories ??= new List<SettlementEventMemory>();
            if (settlement.EventMemorySchemaVersion < SettlementInstance.CurrentEventMemorySchemaVersion)
            {
                settlement.EventMemorySchemaVersion = SettlementInstance.CurrentEventMemorySchemaVersion;
                settlement.EventMemoryMigrationDiagnostic = string.Empty;
            }
        }

        public void Dispose()
        {
            if (retired) return;
            retired = true;
            Exception firstException = null;
            foreach (UnityEngine.Object ownedObject in new List<UnityEngine.Object>(ownedObjects))
            {
                try
                {
                    DestroyOwnedObject(ownedObject);
                }
                catch (Exception exception)
                {
                    firstException ??= exception;
                }
            }
            ownedObjects.Clear();
            if (firstException != null) throw firstException;
        }

        internal static bool ValidateContent(IReadOnlyList<ItemData> items, IReadOnlyList<InventionData> inventions, IReadOnlyList<CraftRecipe> recipes, IReadOnlyList<EventData> events, IReadOnlyList<HunterData> startingHunters, IReadOnlyList<HunterData> recruitmentTemplates, PlayableTraitCatalog traits, out string reason)
        {
            if (!ValidateHunterTraits(startingHunters, traits, out reason) || !ValidateHunterTraits(recruitmentTemplates, traits, out reason)) return false;
            foreach (ItemData item in items)
                if (item == null || !item.HasExplicitContentId)
                {
                    reason = $"营地物品缺少显式稳定 ContentId：{item?.name}";
                    return false;
                }
            foreach (ItemData item in items)
                if (item.itemType == ItemType.Consumable && (item.ConsumableEffect == ConsumableEffectKind.None || item.ConsumableEffectAmount < 1 || item.ConsumableEffectAmount > 99 || item.HuntNoise != 0) || item.itemType != ItemType.Consumable && (item.ConsumableEffect != ConsumableEffectKind.None || item.ConsumableEffectAmount != 0))
                {
                    reason = $"营地物品消耗品效果配置无效：{item.ContentId}";
                    return false;
                }
            foreach (InventionData invention in inventions)
                if (invention == null || !invention.HasExplicitContentId)
                {
                    reason = $"营地发明缺少显式稳定 ContentId：{invention?.name}";
                    return false;
                }
            foreach (EventData gameEvent in events)
                if (gameEvent == null || !gameEvent.HasExplicitContentId)
                {
                    reason = $"营地事件缺少显式稳定 ContentId：{gameEvent?.name}";
                    return false;
                }
            if (!ValidateAssets(items, item => item.ContentId, item => item.itemName, out reason)) return false;
            if (!ValidateAssets(inventions, invention => invention.ContentId, invention => invention.inventionName, invention => invention.name, out reason)) return false;
            if (!ValidateAssets(events, gameEvent => gameEvent.ContentId, gameEvent => gameEvent.name, out reason)) return false;
            var activeEffectOwners = new HashSet<string>(StringComparer.Ordinal);
            foreach (InventionData invention in inventions)
                foreach (InventionActiveEffect effect in invention.activeEffects ?? new List<InventionActiveEffect>())
                {
                    string effectId = effect?.effectId?.Trim() ?? string.Empty;
                    if (effect == null || effectId.Length == 0 || string.IsNullOrWhiteSpace(effect.eventId) || effect.maxUsesPerYear < 0 || !activeEffectOwners.Add(effectId))
                    {
                        reason = $"发明主动效果身份无效或冲突：{effectId}";
                        return false;
                    }
                }
            HashSet<string> itemAliases = BuildAliases(items, item => item.ContentId, item => item.itemName);
            HashSet<string> inventionAliases = BuildAliases(inventions, invention => invention.ContentId, invention => invention.inventionName, invention => invention.name);
            var itemReferences = new HashSet<ItemData>(items);
            var inventionReferences = new HashSet<InventionData>(inventions);
            var recipeNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (CraftRecipe recipe in recipes ?? Array.Empty<CraftRecipe>())
            {
                string recipeName = recipe?.recipeName?.Trim() ?? string.Empty;
                if (recipe == null || recipeName.Length == 0 || !recipeNames.Add(recipeName) || recipe.outputItem == null || !itemReferences.Contains(recipe.outputItem) || recipe.outputCount <= 0 || recipe.requiredInvention != null && !inventionReferences.Contains(recipe.requiredInvention))
                {
                    reason = $"营地配方无效或引用了计划外内容：{recipeName}";
                    return false;
                }
                foreach (RecipeIngredient ingredient in recipe.ingredients ?? new List<RecipeIngredient>())
                    if (ingredient?.item == null || ingredient.count <= 0 || !itemReferences.Contains(ingredient.item))
                    {
                        reason = $"营地配方 {recipeName} 含无效材料。";
                        return false;
                    }
            }
            foreach (InventionData invention in inventions)
            {
                foreach (InventionData prerequisite in invention.prerequisites ?? new List<InventionData>())
                    if (prerequisite == null || !inventionReferences.Contains(prerequisite))
                    {
                        reason = $"发明 {invention.ContentId} 引用了计划外前置发明。";
                        return false;
                    }
                foreach (InventionData exclusive in invention.exclusiveWith ?? new List<InventionData>())
                    if (exclusive == null || !inventionReferences.Contains(exclusive))
                    {
                        reason = $"发明 {invention.ContentId} 引用了计划外互斥发明。";
                        return false;
                    }
            }
            var inventionVisitStates = new Dictionary<InventionData, int>();
            foreach (InventionData invention in inventions)
                if (!ValidateInventionAcyclic(invention, inventionReferences, inventionVisitStates, out reason)) return false;
            var triggeredEventIds = new HashSet<string>(StringComparer.Ordinal);
            var eventByAlias = new Dictionary<string, EventData>(StringComparer.Ordinal);
            foreach (EventData gameEvent in events)
            {
                eventByAlias[gameEvent.ContentId] = gameEvent;
                eventByAlias[gameEvent.name] = gameEvent;
                if (gameEvent.category == EventCategory.Triggered)
                    triggeredEventIds.Add(gameEvent.ContentId);
            }
            foreach (InventionData invention in inventions)
                foreach (InventionActiveEffect effect in invention.activeEffects ?? new List<InventionActiveEffect>())
                    if (!triggeredEventIds.Contains(effect.eventId?.Trim() ?? string.Empty))
                    {
                        reason = $"发明 {invention.ContentId} 引用了未知或非 Triggered 事件：{effect.eventId}";
                        return false;
                    }
            foreach (EventData gameEvent in events)
            {
                var optionIds = new HashSet<string>(StringComparer.Ordinal);
                if (gameEvent.eventType == GameEventType.Choice && gameEvent.category != EventCategory.Hunt)
                    foreach (EventOption option in gameEvent.options ?? new List<EventOption>())
                    {
                        string optionId = option?.optionId?.Trim() ?? string.Empty;
                        if (optionId.Length == 0 || !optionIds.Add(optionId))
                        {
                            reason = $"营地事件 {gameEvent.ContentId} 的 Choice optionId 缺失或重复：{optionId}";
                            return false;
                        }
                    }
                foreach (EventEffect effect in EnumerateEffects(gameEvent))
                {
                    string target = effect?.targetName?.Trim() ?? string.Empty;
                    if (effect?.effectType == EventEffectType.AdvanceYear)
                    {
                        reason = $"事件 {gameEvent.ContentId} 使用了已禁用的推进年份效果；年份只能由回营日历提交。";
                        return false;
                    }
                    if ((effect?.effectType == EventEffectType.AddResource || effect?.effectType == EventEffectType.RemoveResource) && !itemAliases.Contains(target))
                    {
                        reason = $"事件 {gameEvent.ContentId} 引用了未知物品：{target}";
                        return false;
                    }
                    if (effect?.effectType == EventEffectType.UnlockInvention && !inventionAliases.Contains(target))
                    {
                        reason = $"事件 {gameEvent.ContentId} 引用了未知发明：{target}";
                        return false;
                    }
                    if (effect?.effectType == EventEffectType.AddTrait && !traits.ContainsCanonicalId(target))
                    {
                        reason = $"事件 {gameEvent.ContentId} 引用了未知或非稳定特性 ID：{target}";
                        return false;
                    }
                    if (effect?.effectType == EventEffectType.ScheduleEvent && (!eventByAlias.TryGetValue(target, out EventData scheduledEvent) || scheduledEvent.category != EventCategory.Scheduled))
                    {
                        reason = $"事件 {gameEvent.ContentId} 引用了未知或非 Scheduled 事件：{target}";
                        return false;
                    }
                }
                if (!ValidateEventChain(gameEvent.ContentId, gameEvent.chainedEvents, eventByAlias, out reason)) return false;
                foreach (EventOption option in gameEvent.options ?? new List<EventOption>())
                {
                    if (!ValidateEventChain(gameEvent.ContentId, option?.successChain, eventByAlias, out reason) || !ValidateEventChain(gameEvent.ContentId, option?.failChain, eventByAlias, out reason)) return false;
                    foreach (EventOptionCondition condition in option?.conditions ?? new List<EventOptionCondition>())
                    {
                        if ((condition.conditionKind == EventOptionConditionKind.MinimumResource || condition.conditionKind == EventOptionConditionKind.HasEquippedItem) && !itemAliases.Contains(condition.key?.Trim() ?? string.Empty))
                        {
                            reason = $"事件 {gameEvent.ContentId} 的条件引用了未知物品：{condition.key}";
                            return false;
                        }
                        if (condition.conditionKind == EventOptionConditionKind.HasTrait && !traits.ContainsCanonicalId(condition.key))
                        {
                            reason = $"事件 {gameEvent.ContentId} 的条件引用了未知或非稳定特性 ID：{condition.key}";
                            return false;
                        }
                    }
                }
            }
            reason = string.Empty;
            return true;
        }

        private static bool ValidateHunterTraits(IReadOnlyList<HunterData> hunters, PlayableTraitCatalog traits, out string reason)
        {
            foreach (HunterData hunter in hunters ?? Array.Empty<HunterData>())
                foreach (string traitId in hunter?.startingTraits ?? new List<string>())
                    if (!traits.ContainsCanonicalId(traitId))
                    {
                        reason = $"猎人模板 {hunter?.ContentId} 引用了未知或非稳定特性 ID：{traitId}";
                        return false;
                    }
            reason = string.Empty;
            return true;
        }

        private static bool ValidateEventChain(string sourceId, IReadOnlyList<EventData> chain, IReadOnlyDictionary<string, EventData> eventByAlias, out string reason)
        {
            foreach (EventData chainedEvent in chain ?? Array.Empty<EventData>())
                if (chainedEvent == null || !eventByAlias.TryGetValue(chainedEvent.ContentId, out EventData registered) || !ReferenceEquals(registered, chainedEvent) || chainedEvent.category != EventCategory.Triggered)
                {
                    reason = $"事件 {sourceId} 引用了计划外或非 Triggered 子事件：{chainedEvent?.ContentId}";
                    return false;
                }
            reason = string.Empty;
            return true;
        }

        private static bool ValidateInventionAcyclic(InventionData invention, HashSet<InventionData> knownInventions, IDictionary<InventionData, int> states, out string reason)
        {
            if (states.TryGetValue(invention, out int state))
            {
                if (state == 2)
                {
                    reason = string.Empty;
                    return true;
                }
                reason = $"发明前置关系存在循环：{invention.ContentId}";
                return false;
            }
            states[invention] = 1;
            foreach (InventionData prerequisite in invention.prerequisites ?? new List<InventionData>())
            {
                if (!knownInventions.Contains(prerequisite))
                {
                    reason = $"发明 {invention.ContentId} 引用了计划外前置发明。";
                    return false;
                }
                if (!ValidateInventionAcyclic(prerequisite, knownInventions, states, out reason)) return false;
            }
            states[invention] = 2;
            reason = string.Empty;
            return true;
        }

        private static HashSet<string> BuildAliases<T>(IReadOnlyList<T> assets, params Func<T, string>[] selectors) where T : UnityEngine.Object
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (T asset in assets ?? Array.Empty<T>())
                foreach (Func<T, string> selector in selectors)
                {
                    string alias = asset != null ? selector(asset)?.Trim() ?? string.Empty : string.Empty;
                    if (alias.Length > 0) result.Add(alias);
                }
            return result;
        }

        private static IEnumerable<EventEffect> EnumerateEffects(EventData gameEvent)
        {
            if (gameEvent == null) yield break;
            foreach (EventEffect effect in gameEvent.immediateEffects ?? new List<EventEffect>()) yield return effect;
            foreach (EventOption option in gameEvent.options ?? new List<EventOption>())
            {
                if (option == null) continue;
                foreach (EventEffect effect in option.successEffects ?? new List<EventEffect>()) yield return effect;
                foreach (EventEffect effect in option.failEffects ?? new List<EventEffect>()) yield return effect;
            }
        }

        private static bool ValidateAssets<T>(IReadOnlyList<T> assets, Func<T, string> getId, Func<T, string> getName, out string reason) where T : UnityEngine.Object => ValidateAssets(assets, getId, getName, null, out reason);

        private static bool ValidateAssets<T>(IReadOnlyList<T> assets, Func<T, string> getId, Func<T, string> getName, Func<T, string> getAlias, out string reason) where T : UnityEngine.Object
        {
            var owners = new Dictionary<string, T>(StringComparer.Ordinal);
            var references = new HashSet<T>();
            foreach (T asset in assets ?? Array.Empty<T>())
            {
                string id = asset != null ? getId(asset)?.Trim() ?? string.Empty : string.Empty;
                string displayName = asset != null ? getName(asset)?.Trim() ?? string.Empty : string.Empty;
                string alias = asset != null && getAlias != null ? getAlias(asset)?.Trim() ?? string.Empty : string.Empty;
                if (asset == null || id.Length == 0 || displayName.Length == 0)
                {
                    reason = "营地内容包含空对象、空稳定 ID 或空名称。";
                    return false;
                }
                if (!references.Add(asset))
                {
                    reason = $"营地内容重复引用同一对象：{id}";
                    return false;
                }
                if (owners.TryGetValue(id, out T idOwner) && !ReferenceEquals(idOwner, asset) || owners.TryGetValue(displayName, out T nameOwner) && !ReferenceEquals(nameOwner, asset))
                {
                    reason = $"营地内容身份冲突：{id}/{displayName}";
                    return false;
                }
                owners[id] = asset;
                owners[displayName] = asset;
                if (alias.Length > 0)
                {
                    if (owners.TryGetValue(alias, out T aliasOwner) && !ReferenceEquals(aliasOwner, asset))
                    {
                        reason = $"营地内容别名冲突：{alias}";
                        return false;
                    }
                    owners[alias] = asset;
                }
            }
            reason = string.Empty;
            return true;
        }

        private static void SynchronizeExistingRoster(SettlementManager manager)
        {
            PlayableSettlementItemRegistry.RestoreEquipment(manager.Data);
            PlayableBloodlineRuntime.Synchronize(manager.Data, manager.RandomSource);
            PlayableSymptomRuntime.Synchronize(manager.Data);
            PlayableGrowthMilestoneRuntime.Synchronize(manager.Data);
        }

        private static bool MigrateCampaignPacing(SettlementManager manager, CampaignCalendarDefinition calendar, out string reason)
        {
            SettlementInstance data = manager.Data;
            reason = string.Empty;
            if (!CampaignCalendarRules.TryValidateDefinition(calendar, out reason)) return false;
            if (data.CampaignPacingSchemaVersion > SettlementInstance.CurrentCampaignPacingSchemaVersion)
            {
                reason = "营地 pacing schema 高于当前内容版本。";
                return false;
            }
            if (data.CurrentYear < 1)
            {
                reason = "营地当前年份无效。";
                return false;
            }
            if (data.CampaignPacingSchemaVersion == SettlementInstance.CurrentCampaignPacingSchemaVersion)
            {
                if (!string.Equals(data.CampaignCalendarId, calendar.CalendarId, StringComparison.Ordinal))
                {
                    reason = "营地存档缺少或引用未知的战役日历。";
                    return false;
                }
                if (data.CurrentSeasonIndex < 0 || data.CurrentSeasonIndex >= calendar.Seasons.Count)
                {
                    reason = "营地当前季节超出战役日历定义。";
                    return false;
                }
                data.NormalizeLegacyHuntProgress();
                return true;
            }

            int completed = 0;
            if (data.CampaignPacingSchemaVersion == 0)
            {
                int legacyQuota = data.HuntsPerYear;
                if (legacyQuota != calendar.Seasons.Count || legacyQuota < 1 || data.HuntsCompletedThisYear < 0 || data.HuntsCompletedThisYear >= legacyQuota)
                {
                    data.CampaignPacingMigrationDiagnostic = $"旧年度狩猎进度与日历不匹配：{data.HuntsCompletedThisYear}/{legacyQuota}，已保守归一化到第一季。";
                }
                else
                {
                    completed = data.HuntsCompletedThisYear;
                    data.CampaignPacingMigrationDiagnostic = string.Empty;
                }
            }
            data.CampaignCalendarId = calendar.CalendarId;
            data.CurrentSeasonIndex = completed;
            if (data.CampaignPacingSchemaVersion == 1)
                data.CampaignPacingMigrationDiagnostic = string.Empty;
            data.NormalizeLegacyHuntProgress();
            data.CampaignPacingSchemaVersion = SettlementInstance.CurrentCampaignPacingSchemaVersion;
            return true;
        }

        private static void DestroyOwnedObject(UnityEngine.Object ownedObject)
        {
            if (ownedObject == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(ownedObject);
            else
                UnityEngine.Object.DestroyImmediate(ownedObject);
        }

        private readonly struct StartingResourceSnapshot
        {
            public StartingResourceSnapshot(ItemData item, int amount)
            {
                Item = item;
                Amount = amount;
            }

            public ItemData Item { get; }
            public int Amount { get; }
        }
    }
}
