using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.ContentTables;
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
        [SerializeField, Min(1)] private int huntsPerYear = 2;
        [SerializeField] private List<HunterData> startingHunters = new();
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

        public bool IsConfigured => startingHunters.Exists(hunter => hunter != null);
        public IReadOnlyList<HunterData> RecruitmentTemplates => recruitmentTemplates;
        public ItemData RecruitmentCostItem => recruitmentCostItem;
        public int RecruitmentCost => Mathf.Max(0, recruitmentCost);
        public int MaximumLivingHunters => Mathf.Max(1, maximumLivingHunters);
        public ItemData RecoveryCostItem => recoveryCostItem;
        public int RecoveryCost => Mathf.Max(0, recoveryCost);
        public int RecoveryAmount => Mathf.Max(1, recoveryAmount);

        public bool ApplyTo(SettlementManager manager)
        {
            if (manager == null || !IsConfigured) return false;

            PlayableSettlementContentExtensions.Extend(GetKnownItems(), recipes, inventions, inventionTable, out List<ItemData> allItems, out List<CraftRecipe> allRecipes, out List<InventionData> allInventions);
            PlayableSettlementInventionRegistry.Configure(allInventions);
            if (PlayableSettlementInventionRegistry.Inventions.Count != allInventions.Count)
            {
                Debug.LogError("[SettlementManager] 发明目录包含空白、重复或别名冲突的稳定身份，已拒绝装配。");
                return false;
            }

            manager.HunterMgmt.ConfigureDeathInspiration(deathInspirationGrowth, deathInspirationMinimumAge);
            manager.Data.HuntsPerYear = Mathf.Max(1, huntsPerYear);
            manager.Data.HuntsCompletedThisYear = Mathf.Clamp(manager.Data.HuntsCompletedThisYear, 0, manager.Data.HuntsPerYear - 1);
            PlayableSettlementItemRegistry.Configure(allItems);
            PlayableSettlementItemRegistry.MigratePersistentState(manager.Data);
            PlayableSettlementInventionRegistry.MigratePersistentState(manager.Data);
            PlayableEventTableRuntime.Extend(randomEvents, mainStoryEvents, out List<EventData> allRandomEvents, out List<EventData> allMainStoryEvents);
            manager.Timeline.RandomEventPool = allRandomEvents;
            manager.Timeline.MainStoryEvents = allMainStoryEvents;
            manager.Inventions.AllInventions = new List<InventionData>(PlayableSettlementInventionRegistry.Inventions);
            if (!PlayableSettlementModifierRuntime.Synchronize(manager.Data, manager.Inventions.AllInventions, message => Debug.LogError($"[SettlementManager] {message}"))) return false;
            manager.Workshop.AllRecipes = allRecipes;
            if (manager.Data.Hunters.Count > 0)
            {
                PlayableSettlementItemRegistry.RestoreEquipment(manager.Data);
                PlayableSymptomRuntime.Synchronize(manager.Data);
                PlayableGrowthMilestoneRuntime.Synchronize(manager.Data);
                return true;
            }

            foreach (var hunter in startingHunters)
                if (hunter != null)
                    manager.HunterMgmt.AddStartingHunter(hunter.hunterName, hunter);

            foreach (var resource in startingResources)
                if (resource?.Item != null && resource.Amount > 0)
                    manager.Data.AddResource(resource.Item, resource.Amount);

            PlayableSettlementItemRegistry.RestoreEquipment(manager.Data);
            PlayableSymptomRuntime.Synchronize(manager.Data);
            PlayableGrowthMilestoneRuntime.Synchronize(manager.Data);
            Debug.Log($"[SettlementManager] 已从内容目录创建 {manager.Data.Hunters.Count} 名初始猎人。");
            return manager.Data.Hunters.Count > 0;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            catalog = null;
        }

        public static void Configure(PlayableSettlementContentCatalog contentCatalog)
        {
            catalog = contentCatalog;
        }

        public static bool TryApplyTo(SettlementManager manager)
        {
            return catalog != null && catalog.ApplyTo(manager);
        }
    }
}
