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
                _settlement.HasDiscoveredMaterial);
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
                _settlement.HasDiscoveredMaterial,
                GetIngredientAmount,
                out reason);
        }

        public int GetIngredientAmount(CraftIngredientSource source, string itemId)
        {
            return source == CraftIngredientSource.ResourcePool ? _settlement.GetResource(itemId) : _settlement.GetStoredItem(itemId);
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

            var output = new List<ItemInstance>();
            for (int index = 0; index < recipe.outputCount; index++)
                output.Add(new ItemInstance(recipe.outputItem));

            var spentIngredients = new List<RecipeIngredient>();
            foreach (RecipeIngredient ingredient in recipe.ingredients)
            {
                if (ingredient?.item == null) continue;
                bool spent = ingredient.item.itemType == ItemType.Resource ? _settlement.SpendResource(ingredient.item, ingredient.count) : _settlement.SpendStoredItem(ingredient.item, ingredient.count);
                if (spent)
                {
                    spentIngredients.Add(ingredient);
                    continue;
                }
                RollbackIngredients(spentIngredients);
                throw new System.InvalidOperationException($"配方 {recipe.ContentId} 在完整校验后扣除原料失败：{ingredient.item.ContentId}");
            }

            // 资源型产出直接存入仓库
            if (recipe.outputItem.itemType == ItemType.Resource)
                _settlement.AddResource(recipe.outputItem, recipe.outputCount);
            else
                _settlement.AddStoredEquipment(recipe.outputItem, recipe.outputCount);

            Debug.Log($"[Workshop] 制造完成：{recipe.outputItem.itemName} ×{recipe.outputCount}");
            return output;
        }

        private void RollbackIngredients(List<RecipeIngredient> spentIngredients)
        {
            for (int index = spentIngredients.Count - 1; index >= 0; index--)
            {
                RecipeIngredient ingredient = spentIngredients[index];
                if (ingredient.item.itemType == ItemType.Resource)
                    _settlement.AddResource(ingredient.item, ingredient.count);
                else
                    _settlement.AddStoredItem(ingredient.item, ingredient.count);
            }
        }

        private static CraftRecipeDefinition ToDefinition(CraftRecipe recipe)
        {
            if (recipe == null) return null;
            var ingredients = new List<CraftIngredientCost>();
            foreach (RecipeIngredient ingredient in recipe.ingredients)
                if (ingredient?.item != null)
                    ingredients.Add(new CraftIngredientCost(ingredient.item.ContentId, ingredient.count, ingredient.item.itemType == ItemType.Resource ? CraftIngredientSource.ResourcePool : CraftIngredientSource.StoredItemPool));
            return new CraftRecipeDefinition(
                recipe.ContentId,
                recipe.requiredInvention != null ? recipe.requiredInvention.ContentId : "",
                recipe.unlockedByMaterial,
                ingredients,
                recipe.outputItem != null ? recipe.outputItem.ContentId : "",
                recipe.outputCount);
        }
    }
}
