using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    [Serializable]
    public sealed class StartingResourceDefinition
    {
        [SerializeField] private ItemData item;
        [SerializeField, Min(0)] private int amount;

        public ItemData Item => item;
        public int Amount => Mathf.Max(0, amount);
    }

    /// <summary>营地开局内容目录，把可编辑资产映射到现有 Settlement Adapter。</summary>
    [CreateAssetMenu(fileName = "PlayableSettlementContentCatalog", menuName = "Hunting in Darkness/Settlement Content Catalog")]
    public sealed class PlayableSettlementContentCatalog : ScriptableObject
    {
        [SerializeField] private List<HunterData> startingHunters = new();
        [SerializeField] private TextAsset hunterTable;
        [SerializeField] private TextAsset traitTable;
        [SerializeField] private List<StartingResourceDefinition> startingResources = new();
        [SerializeField] private List<EventData> randomEvents = new();
        [SerializeField] private List<EventData> mainStoryEvents = new();
        [SerializeField] private List<InventionData> inventions = new();
        [SerializeField] private TextAsset inventionTable;
        [SerializeField] private List<CraftRecipe> recipes = new();

        [Header("招募")]
        [SerializeField] private List<HunterData> recruitmentTemplates = new();
        [SerializeField] private ItemData recruitmentCostItem;
        [SerializeField, Min(0)] private int recruitmentCost = 1;
        [SerializeField, Min(1)] private int maximumLivingHunters = 6;

        [Header("营火休养")]
        [SerializeField] private ItemData recoveryCostItem;
        [SerializeField, Min(0)] private int recoveryCost = 1;
        [SerializeField, Min(1)] private int recoveryAmount = 1;

        [Header("死亡激励")]
        [SerializeField, Min(0)] private int deathInspirationGrowth = 1;
        [SerializeField, Min(1)] private int deathInspirationMinimumAge = 2;

        public bool IsConfigured => startingHunters != null && startingHunters.Exists(hunter => hunter != null) || hunterTable != null;
        public IReadOnlyList<HunterData> RecruitmentTemplates => PlayableSettlementContentRuntime.TryGetPlan(this, out PlayableSettlementContentPlan plan) ? plan.RecruitmentTemplates : recruitmentTemplates;
        public ItemData RecruitmentCostItem => recruitmentCostItem;
        public int RecruitmentCost => Mathf.Max(0, recruitmentCost);
        public int MaximumLivingHunters => Mathf.Max(1, maximumLivingHunters);
        public ItemData RecoveryCostItem => recoveryCostItem;
        public int RecoveryCost => Mathf.Max(0, recoveryCost);
        public int RecoveryAmount => Mathf.Max(1, recoveryAmount);

        public bool ApplyTo(SettlementManager manager)
        {
            if (manager == null || !IsConfigured) return false;
            if (PlayableSettlementContentRuntime.TryGetPlan(this, out PlayableSettlementContentPlan activePlan)) return activePlan.TryApplyTo(manager, out _);
            if (PlayableSettlementContentRuntime.CurrentPlan != null)
            {
                Debug.LogError("[SettlementManager] 已发布的营地内容属于另一目录，兼容 ApplyTo 不允许替换活动战役世代。");
                return false;
            }
            PlayableEventTableRuntime.GetEvents();
            if (!TryPreparePlan(PlayableEventTableRuntime.CurrentGeneration, out PlayableSettlementContentPlan replacement, out string reason))
            {
                Debug.LogError($"[SettlementManager] {reason}");
                return false;
            }
            PlayableSettlementContentPlan previous = PlayableSettlementContentRuntime.SwapPlan(replacement);
            if (replacement.TryApplyTo(manager, out reason))
            {
                PlayableSettlementContentRuntime.RetirePlan(previous);
                return true;
            }
            PlayableSettlementContentPlan rejected = PlayableSettlementContentRuntime.SwapPlan(previous);
            PlayableSettlementContentRuntime.RetirePlan(rejected);
            Debug.LogError($"[SettlementManager] {reason}");
            return false;
        }

        internal bool TryPreparePlan(PlayableEventTableGeneration eventGeneration, out PlayableSettlementContentPlan plan, out string reason)
        {
            plan = null;
            reason = string.Empty;
            if (!IsConfigured)
            {
                reason = "营地内容目录未配置。";
                return false;
            }
            var errors = new List<string>();
            IReadOnlyList<EventData> tableEvents = eventGeneration != null ? eventGeneration.Events : Array.Empty<EventData>();
            using var ownership = new PlayableSettlementContentOwnership();
            try
            {
                if (!PlayableTraitCatalog.TryLoad(traitTable, out PlayableTraitCatalog traitCatalog, out string traitReason)) errors.Add(traitReason);
                PlayableSettlementContentExtensions.Prepare(GetKnownItems(), recipes, inventions, inventionTable, tableEvents, ownership, out List<ItemData> allItems, out List<CraftRecipe> allRecipes, out List<InventionData> allInventions, errors.Add);
                bool huntersValid = PlayableHunterTemplateTableRuntime.Extend(startingHunters, recruitmentTemplates, allItems, hunterTable, out List<HunterData> allStartingHunters, out List<HunterData> allRecruitmentTemplates, out List<HunterData> generatedHunters, errors.Add);
                ownership.OwnRange(generatedHunters);
                PlayableEventTableRuntime.Extend(randomEvents, mainStoryEvents, tableEvents, out List<EventData> allRandomEvents, out List<EventData> allMainStoryEvents);
                var allEvents = new List<EventData>(allRandomEvents);
                allEvents.AddRange(allMainStoryEvents);
                if (traitCatalog != null && !PlayableSettlementContentPlan.ValidateContent(allItems, allInventions, allRecipes, allEvents, allStartingHunters, allRecruitmentTemplates, traitCatalog, out string validationReason)) errors.Add(validationReason);
                if (!PlayableSettlementRegistryBundle.TryCreate(allItems, allInventions, allEvents, out PlayableSettlementRegistryBundle registryBundle, out string registryReason)) errors.Add(registryReason);
                if (!huntersValid || allStartingHunters.Count == 0) errors.Add("猎人内容未提供任何有效初始模板。");
                if (errors.Count > 0)
                {
                    reason = string.Join("；", errors);
                    return false;
                }
                plan = new PlayableSettlementContentPlan(this, registryBundle, traitCatalog, eventGeneration, allRecipes, allRandomEvents, allMainStoryEvents, allStartingHunters, allRecruitmentTemplates, startingResources, ownership.Objects, deathInspirationGrowth, deathInspirationMinimumAge);
                ownership.Transfer();
                return true;
            }
            catch (Exception exception)
            {
                reason = $"营地内容计划构建异常：{exception.Message}";
                return false;
            }
        }

        private List<ItemData> GetKnownItems()
        {
            var items = new List<ItemData>();
            if (recruitmentCostItem != null)
                items.Add(recruitmentCostItem);
            if (recoveryCostItem != null)
                items.Add(recoveryCostItem);
            foreach (var resource in startingResources)
                if (resource?.Item != null)
                    items.Add(resource.Item);
            foreach (var recipe in recipes)
            {
                if (recipe?.outputItem != null)
                    items.Add(recipe.outputItem);
                if (recipe?.ingredients == null) continue;
                foreach (var ingredient in recipe.ingredients)
                    if (ingredient?.item != null)
                        items.Add(ingredient.item);
            }
            foreach (var hunter in startingHunters)
                if (hunter != null)
                    items.AddRange(hunter.startingEquipment.FindAll(item => item != null));
            foreach (var hunter in recruitmentTemplates)
                if (hunter != null)
                    items.AddRange(hunter.startingEquipment.FindAll(item => item != null));
            return items;
        }
    }

    /// <summary>由组合根配置、旧 Settlement 初始化入口消费的短生命期桥接。</summary>
    public static class PlayableSettlementContentRuntime
    {
        private static PlayableSettlementContentCatalog catalog;
        private static PlayableSettlementContentPlan currentPlan;
        private static PlayableSettlementRegistryBundle legacyRegistryBundle = PlayableSettlementRegistryBundle.CreateLegacy(null, null, null, false);
        public static PlayableSettlementContentCatalog Catalog => catalog;
        public static IReadOnlyList<ItemData> Items => RegistryBundle.Items;
        public static IReadOnlyList<InventionData> Inventions => RegistryBundle.Inventions;
        public static IReadOnlyList<EventData> Events => RegistryBundle.Events;
        internal static PlayableSettlementContentPlan CurrentPlan => currentPlan;
        internal static PlayableTraitCatalog TraitCatalog => currentPlan != null && !currentPlan.IsRetired ? currentPlan.TraitCatalog : null;
        internal static PlayableSettlementRegistryBundle RegistryBundle => currentPlan != null && !currentPlan.IsRetired ? currentPlan.RegistryBundle : legacyRegistryBundle;
        internal static bool IsEventGenerationLeased(PlayableEventTableGeneration generation) => generation != null && currentPlan != null && !currentPlan.IsRetired && ReferenceEquals(currentPlan.EventGeneration, generation);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            catalog = null;
            PlayableSettlementContentPlan retired = currentPlan;
            currentPlan = null;
            legacyRegistryBundle = PlayableSettlementRegistryBundle.CreateLegacy(null, null, null, false);
            RetirePlan(retired);
        }

        public static void Configure(PlayableSettlementContentCatalog contentCatalog)
        {
            if (currentPlan != null && !currentPlan.IsRetired)
                throw new InvalidOperationException("已发布营地内容计划期间不得重新配置内容目录。");
            legacyRegistryBundle = PlayableSettlementRegistryBundle.CreateLegacy(null, null, null, false);
            catalog = contentCatalog;
        }

        internal static void ConfigureForInstallation(PlayableSettlementContentCatalog contentCatalog) => catalog = contentCatalog;

        public static bool TryApplyTo(SettlementManager manager) => TryApplyTo(manager, out _);

        internal static bool TryApplyTo(SettlementManager manager, out string reason)
        {
            if (currentPlan != null && !currentPlan.IsRetired) return currentPlan.TryApplyTo(manager, out reason);
            if (catalog != null && catalog.ApplyTo(manager))
            {
                reason = string.Empty;
                return true;
            }
            reason = catalog == null ? "营地内容目录未配置。" : "营地内容目录无法投影到候选状态。";
            return false;
        }

        internal static bool TryGetPlan(PlayableSettlementContentCatalog sourceCatalog, out PlayableSettlementContentPlan plan)
        {
            plan = currentPlan;
            return plan != null && !plan.IsRetired && ReferenceEquals(plan.SourceCatalog, sourceCatalog);
        }

        internal static bool IsCurrentPlan(PlayableSettlementContentPlan plan) => plan != null && !plan.IsRetired && ReferenceEquals(currentPlan, plan);

        internal static PlayableSettlementContentPlan SwapPlan(PlayableSettlementContentPlan replacement)
        {
            PlayableSettlementContentPlan previous = currentPlan;
            currentPlan = replacement;
            return previous;
        }

        internal static void ConfigureLegacyItems(IEnumerable<ItemData> items)
        {
            EnsureLegacyConfigurationAllowed("Item");
            legacyRegistryBundle = PlayableSettlementRegistryBundle.CreateLegacy(items, legacyRegistryBundle.Inventions, legacyRegistryBundle.Events, legacyRegistryBundle.EventsConfigured);
        }

        internal static void ConfigureLegacyInventions(IEnumerable<InventionData> inventions)
        {
            EnsureLegacyConfigurationAllowed("Invention");
            legacyRegistryBundle = PlayableSettlementRegistryBundle.CreateLegacy(legacyRegistryBundle.Items, inventions, legacyRegistryBundle.Events, legacyRegistryBundle.EventsConfigured);
        }

        internal static void ConfigureLegacyEvents(IEnumerable<EventData> events, bool configured = true)
        {
            EnsureLegacyConfigurationAllowed("Event");
            legacyRegistryBundle = PlayableSettlementRegistryBundle.CreateLegacy(legacyRegistryBundle.Items, legacyRegistryBundle.Inventions, events, configured);
        }

        internal static PlayableSettlementRegistryBundle CaptureLegacyRegistryBundle() => legacyRegistryBundle;

        internal static void RestoreLegacyRegistryBundle(PlayableSettlementRegistryBundle bundle)
        {
            legacyRegistryBundle = bundle ?? PlayableSettlementRegistryBundle.CreateLegacy(null, null, null, false);
        }

        internal static void RetirePlan(PlayableSettlementContentPlan plan)
        {
            if (plan == null || ReferenceEquals(plan, currentPlan)) return;
            plan.Dispose();
        }

        private static void EnsureLegacyConfigurationAllowed(string registryName)
        {
            if (currentPlan == null || currentPlan.IsRetired) return;
            throw new InvalidOperationException($"已发布营地内容计划期间不得独立改写 {registryName} Registry。");
        }
    }
}
