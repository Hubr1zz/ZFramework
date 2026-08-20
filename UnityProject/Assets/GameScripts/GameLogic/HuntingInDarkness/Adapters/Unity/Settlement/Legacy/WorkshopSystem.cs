using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    /// <summary>
    /// 工坊系统（纯 C#）。
    /// 职责：管理可用配方、制造逻辑（消耗资源 → 产出装备）、配方解锁。
    /// </summary>
    public class WorkshopSystem
    {
        private readonly SettlementInstance _settlement;
        private readonly InventionSystem    _inventionSystem;

        /// <summary>所有配方模板（运行时从 SO 加载）</summary>
        public List<CraftRecipe> AllRecipes { get; set; } = new();

        public WorkshopSystem(SettlementInstance settlement, InventionSystem inventionSystem)
        {
            _settlement      = settlement;
            _inventionSystem = inventionSystem;
        }

        // ─── 查询 ─────────────────────────────────────────────────

        /// <summary>当前已解锁的可制造配方</summary>
        public List<CraftRecipe> GetAvailableRecipes()
        {
            var result = new List<CraftRecipe>();
            foreach (var recipe in AllRecipes)
            {
                if (IsRecipeUnlocked(recipe))
                    result.Add(recipe);
            }
            return result;
        }

        public bool IsRecipeUnlocked(CraftRecipe recipe)
        {
            if (recipe == null || !string.IsNullOrEmpty(recipe.requiredWorkshopId) && !_settlement.IsWorkshopBuilt(recipe.requiredWorkshopId)) return false;
            return WorkshopRules.IsUnlocked(
                ToDefinition(recipe),
                _settlement.IsInventionUnlocked,
                _settlement.GetResource);
        }

        public bool CanCraft(CraftRecipe recipe, out string reason)
        {
            if (recipe != null && !string.IsNullOrEmpty(recipe.requiredWorkshopId) && !_settlement.IsWorkshopBuilt(recipe.requiredWorkshopId))
            {
                reason = $"需要先建造工坊：{recipe.requiredWorkshopId}";
                return false;
            }
            return WorkshopRules.CanCraft(
                ToDefinition(recipe),
                _settlement.IsInventionUnlocked,
                _settlement.GetResource,
                out reason);
        }

        // ─── 制造 ─────────────────────────────────────────────────

        /// <summary>执行制造。返回产出的物品实例列表（失败返回空）</summary>
        public List<ItemInstance> TryCraft(CraftRecipe recipe)
        {
            if (!CanCraft(recipe, out var reason))
            {
                Debug.LogWarning($"[Workshop] 无法制造 {recipe?.recipeName}: {reason}");
                return new List<ItemInstance>();
            }

            // 消耗原料
            foreach (var ing in recipe.ingredients)
                if (ing.item != null) _settlement.SpendResource(ing.item, ing.count);

            // 产出物品（加入资源存储）
            var output = new List<ItemInstance>();
            for (int i = 0; i < recipe.outputCount; i++)
                output.Add(new ItemInstance(recipe.outputItem));

            // 资源型产出直接存入仓库
            if (recipe.outputItem.itemType == ItemType.Resource)
                _settlement.AddResource(recipe.outputItem, recipe.outputCount);
            else
                _settlement.AddStoredEquipment(recipe.outputItem, recipe.outputCount);

            Debug.Log($"[Workshop] 制造完成：{recipe.outputItem.itemName} ×{recipe.outputCount}");
            return output;
        }

        private static CraftRecipeDefinition ToDefinition(CraftRecipe recipe)
        {
            if (recipe == null) return null;
            var ingredients = new List<ResourceCost>();
            foreach (RecipeIngredient ingredient in recipe.ingredients)
                if (ingredient?.item != null)
                    ingredients.Add(new ResourceCost(ingredient.item.ContentId, ingredient.count));
            return new CraftRecipeDefinition(
                recipe.recipeName,
                recipe.requiredInvention != null ? recipe.requiredInvention.ContentId : "",
                recipe.unlockedByMaterial,
                ingredients,
                recipe.outputItem != null ? recipe.outputItem.ContentId : "",
                recipe.outputCount);
        }
    }
}
