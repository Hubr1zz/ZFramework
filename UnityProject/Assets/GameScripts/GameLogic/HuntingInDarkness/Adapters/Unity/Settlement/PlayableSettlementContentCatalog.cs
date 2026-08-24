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
        [NonSerialized] private List<HunterData> resolvedRecruitmentTemplates;

        [Header("营火休养")]
        [SerializeField] private ItemData recoveryCostItem;
        [SerializeField, Min(0)] private int recoveryCost = 1;
        [SerializeField, Min(1)] private int recoveryAmount = 1;

        [Header("死亡激励")]
        [SerializeField, Min(0)] private int deathInspirationGrowth = 1;
        [SerializeField, Min(1)] private int deathInspirationMinimumAge = 2;

        public bool IsConfigured => startingHunters != null && startingHunters.Exists(hunter => hunter != null) || hunterTable != null;
        public IReadOnlyList<HunterData> RecruitmentTemplates => resolvedRecruitmentTemplates ?? recruitmentTemplates;
        public ItemData RecruitmentCostItem => recruitmentCostItem;
        public int RecruitmentCost => Mathf.Max(0, recruitmentCost);
        public int MaximumLivingHunters => Mathf.Max(1, maximumLivingHunters);
        public ItemData RecoveryCostItem => recoveryCostItem;
        public int RecoveryCost => Mathf.Max(0, recoveryCost);
        public int RecoveryAmount => Mathf.Max(1, recoveryAmount);

        public bool ApplyTo(SettlementManager manager)
        {
            resolvedRecruitmentTemplates = null;
            if (manager == null || !IsConfigured) return false;

            PlayableSettlementContentExtensions.Extend(GetKnownItems(), recipes, inventions, inventionTable, out List<ItemData> allItems, out List<CraftRecipe> allRecipes, out List<InventionData> allInventions);
            if (!PlayableHunterTemplateTableRuntime.Extend(startingHunters, recruitmentTemplates, allItems, hunterTable, out List<HunterData> allStartingHunters, out List<HunterData> allRecruitmentTemplates, message => Debug.LogError($"[SettlementManager] {message}"))) return false;
            if (allStartingHunters.Count == 0)
            {
                Debug.LogError("[SettlementManager] 猎人内容未提供任何有效初始模板，已拒绝装配。");
                return false;
            }
            resolvedRecruitmentTemplates = allRecruitmentTemplates;
            PlayableSettlementInventionRegistry.Configure(allInventions);
            if (PlayableSettlementInventionRegistry.Inventions.Count != allInventions.Count)
            {
                Debug.LogError("[SettlementManager] 发明目录包含空白、重复或别名冲突的稳定身份，已拒绝装配。");
                return false;
            }

            manager.HunterMgmt.ConfigureDeathInspiration(deathInspirationGrowth, deathInspirationMinimumAge);
            PlayableSettlementItemRegistry.Configure(allItems);
            PlayableSettlementItemRegistry.MigratePersistentState(manager.Data);
            PlayableSettlementInventionRegistry.MigratePersistentState(manager.Data);
            PlayableEventTableRuntime.Extend(randomEvents, mainStoryEvents, out List<EventData> allRandomEvents, out List<EventData> allMainStoryEvents);
            var allEvents = new List<EventData>(allRandomEvents);
            allEvents.AddRange(allMainStoryEvents);
            PlayableSettlementEventRegistry.Configure(allEvents);
            if (!PlayableSettlementEventRegistry.IsValid)
            {
                Debug.LogError($"[SettlementManager] {PlayableSettlementEventRegistry.Diagnostic}");
                return false;
            }
            if (!PlayableSettlementEventRegistry.MigratePersistentState(manager.Data) && manager.Data.TimelineEventIdentitySchemaVersion > PlayableSettlementEventRegistry.CurrentIdentitySchemaVersion)
            {
                Debug.LogError($"[SettlementManager] {manager.Data.TimelineEventIdentityMigrationDiagnostic}");
                return false;
            }
            manager.Timeline.RandomEventPool = allRandomEvents;
            manager.Timeline.MainStoryEvents = allMainStoryEvents;
            MigrateCampaignPacing(manager);
            manager.Inventions.AllInventions = new List<InventionData>(PlayableSettlementInventionRegistry.Inventions);
            if (!PlayableSettlementModifierRuntime.Synchronize(manager.Data, manager.Inventions.AllInventions, message => Debug.LogError($"[SettlementManager] {message}"))) return false;
            manager.Workshop.AllRecipes = allRecipes;
            if (manager.Data.Hunters.Count > 0)
            {
                PlayableBloodlineRuntime.Synchronize(manager.Data);
                PlayableSettlementItemRegistry.RestoreEquipment(manager.Data);
                PlayableSymptomRuntime.Synchronize(manager.Data);
                PlayableGrowthMilestoneRuntime.Synchronize(manager.Data);
                return true;
            }

            foreach (var hunter in allStartingHunters)
                if (hunter != null)
                    manager.HunterMgmt.AddStartingHunter(hunter.hunterName, hunter);

            foreach (var resource in startingResources)
                if (resource?.Item != null && resource.Amount > 0)
                    manager.Data.AddResource(resource.Item, resource.Amount);

            PlayableSettlementItemRegistry.RestoreEquipment(manager.Data);
            PlayableBloodlineRuntime.Synchronize(manager.Data);
            PlayableSymptomRuntime.Synchronize(manager.Data);
            PlayableGrowthMilestoneRuntime.Synchronize(manager.Data);
            Debug.Log($"[SettlementManager] 已从内容目录创建 {manager.Data.Hunters.Count} 名初始猎人。");
            return manager.Data.Hunters.Count > 0;
        }

        private static void MigrateCampaignPacing(SettlementManager manager)
        {
            SettlementInstance data = manager.Data;
            if (data.CampaignPacingSchemaVersion >= SettlementInstance.CurrentCampaignPacingSchemaVersion)
            {
                data.NormalizeLegacyHuntProgress();
                return;
            }

            int legacyQuota = data.HuntsPerYear;
            int completed = data.HuntsCompletedThisYear;
            if (legacyQuota < 1 || legacyQuota > SettlementInstance.MaxLegacyHuntsPerYear || completed < 0 || completed >= legacyQuota)
            {
                data.CampaignPacingMigrationDiagnostic = $"旧年度狩猎进度无效：{completed}/{legacyQuota}，已安全归一化且未猜测年份。";
                data.NormalizeLegacyHuntProgress();
                data.CampaignPacingSchemaVersion = SettlementInstance.CurrentCampaignPacingSchemaVersion;
                return;
            }

            for (int index = 0; index < completed; index++)
            {
                data.CurrentYear = SettlementTimelineRules.AdvanceYear(data.CurrentYear);
                manager.Timeline.GetEventsForYear(data.CurrentYear);
            }
            data.CampaignPacingMigrationDiagnostic = string.Empty;
            data.NormalizeLegacyHuntProgress();
            data.CampaignPacingSchemaVersion = SettlementInstance.CurrentCampaignPacingSchemaVersion;
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
        public static PlayableSettlementContentCatalog Catalog => catalog;

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
