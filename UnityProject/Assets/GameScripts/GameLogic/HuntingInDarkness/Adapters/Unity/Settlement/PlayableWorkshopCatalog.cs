using System;
using System.Collections.Generic;
using System.Linq;
using HuntingInDarkness.Data;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    [Serializable]
    public sealed class PlayableWorkshopCost
    {
        [SerializeField] private ItemData item;
        [SerializeField, Min(1)] private int amount = 1;

        public ItemData Item => item;
        public int Amount => amount;
    }

    [Serializable]
    public sealed class PlayableWorkshopDefinition
    {
        [SerializeField] private string workshopId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private InventionData requiredInvention;
        [SerializeField] private List<PlayableWorkshopCost> costs = new();

        public string WorkshopId => workshopId;
        public string DisplayName => displayName;
        public string Description => description;
        public InventionData RequiredInvention => requiredInvention;
        public IReadOnlyList<PlayableWorkshopCost> Costs
        {
            get
            {
                if (costs == null) return Array.Empty<PlayableWorkshopCost>();
                return costs;
            }
        }
    }

    [CreateAssetMenu(fileName = "PlayableWorkshopCatalog", menuName = "Hunting in Darkness/Workshop Catalog")]
    public sealed class PlayableWorkshopCatalog : ScriptableObject
    {
        [SerializeField] private List<PlayableWorkshopDefinition> workshops = new();

        public IReadOnlyList<PlayableWorkshopDefinition> Workshops
        {
            get
            {
                if (workshops == null) return Array.Empty<PlayableWorkshopDefinition>();
                return workshops;
            }
        }

        public bool TryValidateAgainst(IReadOnlyList<ItemData> items, IReadOnlyList<InventionData> inventions, IReadOnlyList<CraftRecipe> recipes, out string reason)
        {
            var itemSet = new HashSet<ItemData>(items ?? Array.Empty<ItemData>());
            var inventionSet = new HashSet<InventionData>(inventions ?? Array.Empty<InventionData>());
            var workshopIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlayableWorkshopDefinition workshop in Workshops)
            {
                string workshopId = workshop?.WorkshopId?.Trim() ?? string.Empty;
                if (workshop == null || workshopId.Length == 0)
                {
                    reason = "工坊目录包含空定义或空 ID。";
                    return false;
                }
                if (!string.Equals(workshop.WorkshopId, workshopId, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(workshop.DisplayName))
                {
                    reason = $"工坊 {workshopId} 的 ID 或显示名称未规范配置。";
                    return false;
                }
                if (!workshopIds.Add(workshopId))
                {
                    reason = $"工坊 ID 重复：{workshopId}";
                    return false;
                }
                if (workshop.RequiredInvention != null && !inventionSet.Contains(workshop.RequiredInvention))
                {
                    reason = $"工坊 {workshopId} 引用了当前营地内容世代之外的发明。";
                    return false;
                }
                if (workshop.Costs.Any(cost => cost?.Item == null || cost.Amount <= 0 || !itemSet.Contains(cost.Item)))
                {
                    reason = $"工坊 {workshopId} 引用了当前营地内容世代之外的成本物品。";
                    return false;
                }
            }
            foreach (CraftRecipe recipe in recipes ?? Array.Empty<CraftRecipe>())
            {
                if (recipe == null)
                {
                    reason = "工坊配方列表包含空定义。";
                    return false;
                }
                string requiredWorkshopId = recipe.requiredWorkshopId?.Trim() ?? string.Empty;
                if (!string.Equals(recipe.requiredWorkshopId ?? string.Empty, requiredWorkshopId, StringComparison.Ordinal))
                {
                    reason = $"配方 {recipe.recipeName} 的工坊 ID 未规范配置。";
                    return false;
                }
                if (requiredWorkshopId.Length > 0 && !workshopIds.Contains(requiredWorkshopId))
                {
                    reason = $"配方 {recipe.recipeName} 引用了不存在的工坊：{requiredWorkshopId}";
                    return false;
                }
            }
            reason = string.Empty;
            return true;
        }
    }
}
