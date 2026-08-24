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
            if (!TryPreparePlan(PlayableEventTableRuntime.GetEvents(), out PlayableSettlementContentPlan replacement, out string reason))
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

        internal bool TryPreparePlan(IReadOnlyList<EventData> tableEvents, out PlayableSettlementContentPlan plan, out string reason)
        {
            plan = null;
            reason = string.Empty;
            if (!IsConfigured)
            {
                reason = "营地内容目录未配置。";
                return false;
            }
            var errors = new List<string>();
            using var ownership = new PlayableSettlementContentOwnership();
            try
            {
                PlayableSettlementContentExtensions.Prepare(GetKnownItems(), recipes, inventions, inventionTable, tableEvents, ownership, out List<ItemData> allItems, out List<CraftRecipe> allRecipes, out List<InventionData> allInventions, errors.Add);
                bool huntersValid = PlayableHunterTemplateTableRuntime.Extend(startingHunters, recruitmentTemplates, allItems, hunterTable, out List<HunterData> allStartingHunters, out List<HunterData> allRecruitmentTemplates, out List<HunterData> generatedHunters, errors.Add);
                ownership.OwnRange(generatedHunters);
                PlayableEventTableRuntime.Extend(randomEvents, mainStoryEvents, tableEvents, out List<EventData> allRandomEvents, out List<EventData> allMainStoryEvents);
                var allEvents = new List<EventData>(allRandomEvents);
                allEvents.AddRange(allMainStoryEvents);
                if (!PlayableSettlementContentPlan.ValidateContent(allItems, allInventions, allRecipes, allEvents, out string validationReason)) errors.Add(validationReason);
                if (!huntersValid || allStartingHunters.Count == 0) errors.Add("猎人内容未提供任何有效初始模板。");
                if (errors.Count > 0)
                {
                    reason = string.Join("；", errors);
                    return false;
                }
                plan = new PlayableSettlementContentPlan(this, allItems, allInventions, allRecipes, allRandomEvents, allMainStoryEvents, allStartingHunters, allRecruitmentTemplates, startingResources, ownership.Objects, deathInspirationGrowth, deathInspirationMinimumAge);
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
        public static PlayableSettlementContentCatalog Catalog => catalog;
        internal static PlayableSettlementContentPlan CurrentPlan => currentPlan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            catalog = null;
            PlayableSettlementContentPlan retired = SwapPlan(null);
            RetirePlan(retired);
        }

        public static void Configure(PlayableSettlementContentCatalog contentCatalog)
        {
            PlayableSettlementContentPlan retired = SwapPlan(null);
            catalog = contentCatalog;
            RetirePlan(retired);
        }

        internal static void ConfigureForInstallation(PlayableSettlementContentCatalog contentCatalog) => catalog = contentCatalog;

        public static bool TryApplyTo(SettlementManager manager)
        {
            if (currentPlan != null && !currentPlan.IsRetired) return currentPlan.TryApplyTo(manager, out _);
            return catalog != null && catalog.ApplyTo(manager);
        }

        internal static bool TryGetPlan(PlayableSettlementContentCatalog sourceCatalog, out PlayableSettlementContentPlan plan)
        {
            plan = currentPlan;
            return plan != null && !plan.IsRetired && ReferenceEquals(plan.SourceCatalog, sourceCatalog);
        }

        internal static PlayableSettlementContentPlan SwapPlan(PlayableSettlementContentPlan replacement)
        {
            PlayableSettlementContentPlan previous = currentPlan;
            currentPlan = replacement;
            PlayableSettlementItemRegistry.Configure(replacement?.Items);
            PlayableSettlementInventionRegistry.Configure(replacement?.Inventions);
            if (replacement == null)
            {
                PlayableSettlementEventRegistry.Configure(null);
                return previous;
            }
            var events = new List<EventData>(replacement.RandomEvents);
            events.AddRange(replacement.MainStoryEvents);
            PlayableSettlementEventRegistry.Configure(events);
            if (PlayableSettlementItemRegistry.Items.Count != replacement.Items.Count || PlayableSettlementInventionRegistry.Inventions.Count != replacement.Inventions.Count || !PlayableSettlementEventRegistry.IsValid)
                throw new InvalidOperationException("营地内容计划与兼容 Registry 的稳定身份投影不一致。");
            return previous;
        }

        internal static void RetirePlan(PlayableSettlementContentPlan plan)
        {
            if (plan == null || ReferenceEquals(plan, currentPlan)) return;
            plan.Dispose();
        }
    }
}
