using System;
using System.Collections.Generic;
using HuntingInDarkness.Bootstrap;
using HuntingInDarkness.Data;
using HuntingInDarkness.ContentTables;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    public static class PlayableSettlementContentExtensions
    {
        public static void Extend(IReadOnlyList<ItemData> baseItems, IReadOnlyList<CraftRecipe> baseRecipes, out List<ItemData> allItems, out List<CraftRecipe> allRecipes)
        {
            Extend(baseItems, baseRecipes, null, out allItems, out allRecipes);
        }

        public static void Extend(IReadOnlyList<ItemData> baseItems, IReadOnlyList<CraftRecipe> baseRecipes, IReadOnlyList<InventionData> inventions, out List<ItemData> allItems, out List<CraftRecipe> allRecipes)
        {
            Extend(baseItems, baseRecipes, inventions, null, out allItems, out allRecipes, out _);
        }

        public static void Extend(IReadOnlyList<ItemData> baseItems, IReadOnlyList<CraftRecipe> baseRecipes, IReadOnlyList<InventionData> baseInventions, TextAsset inventionTable, out List<ItemData> allItems, out List<CraftRecipe> allRecipes, out List<InventionData> allInventions)
        {
            allItems = CopyItems(baseItems);
            allRecipes = CopyRecipes(baseRecipes);
            allInventions = CopyInventions(baseInventions);
            AppendExplicitExtensions(allItems, allRecipes, Array.Empty<PlayableSettlementContentExtension>());
            AppendItems(allItems, PlayableItemTableRuntime.GetItems());
            AppendInventions(allInventions, PlayableInventionTableRuntime.GetInventions(inventionTable, allItems, baseInventions));
            AppendRecipeItems(allItems, allRecipes);
        }

        public static void Extend(IReadOnlyList<ItemData> baseItems, IReadOnlyList<CraftRecipe> baseRecipes, IReadOnlyList<InventionData> baseInventions, TextAsset inventionTable, PlayableContentSourceBundle sourceBundle, IReadOnlyList<EventData> events, out List<ItemData> allItems, out List<CraftRecipe> allRecipes, out List<InventionData> allInventions)
        {
            allItems = CopyItems(baseItems);
            allRecipes = CopyRecipes(baseRecipes);
            allInventions = CopyInventions(baseInventions);
            if (sourceBundle == null) return;
            AppendExplicitExtensions(allItems, allRecipes, sourceBundle.SettlementExtensions);
            AppendItems(allItems, PlayableItemTableRuntime.BuildTable(sourceBundle.ItemsTable, null));
            AppendInventions(allInventions, PlayableInventionTableRuntime.BuildTable(inventionTable, allItems, baseInventions, events ?? Array.Empty<EventData>(), null));
            AppendRecipes(allRecipes, PlayableCraftRecipeTableRuntime.BuildTable(sourceBundle.RecipesTable, allItems, allInventions, null));
            AppendRecipeItems(allItems, allRecipes);
        }

        internal static void Prepare(IReadOnlyList<ItemData> baseItems, IReadOnlyList<CraftRecipe> baseRecipes, IReadOnlyList<InventionData> baseInventions, TextAsset inventionTable, IReadOnlyList<EventData> events, PlayableContentSourceBundle sourceBundle, PlayableSettlementContentOwnership ownership, out List<ItemData> allItems, out List<CraftRecipe> allRecipes, out List<InventionData> allInventions, System.Action<string> reportError)
        {
            allItems = CopyItems(baseItems);
            allRecipes = CopyRecipes(baseRecipes);
            allInventions = CopyInventions(baseInventions);
            if (sourceBundle == null)
            {
                reportError?.Invoke("缺少 Settlement 内容源 Bundle。");
                return;
            }
            AppendExplicitExtensions(allItems, allRecipes, sourceBundle.SettlementExtensions);

            List<ItemData> generatedItems = PlayableItemTableRuntime.BuildTable(sourceBundle.ItemsTable, reportError);
            ownership.OwnRange(generatedItems);
            AppendItems(allItems, generatedItems);
            List<InventionData> generatedInventions = PlayableInventionTableRuntime.BuildTable(inventionTable, allItems, baseInventions, events, reportError);
            ownership.OwnRange(generatedInventions);
            AppendInventions(allInventions, generatedInventions);
            AppendRecipes(allRecipes, PlayableCraftRecipeTableRuntime.BuildTable(sourceBundle.RecipesTable, allItems, allInventions, reportError));
            AppendRecipeItems(allItems, allRecipes);
        }

        private static void AppendExplicitExtensions(List<ItemData> allItems, List<CraftRecipe> allRecipes, IReadOnlyList<PlayableSettlementContentExtension> extensions)
        {
            if (extensions == null) return;
            foreach (PlayableSettlementContentExtension extension in extensions)
            {
                if (extension == null) continue;
                AppendItems(allItems, extension.Items);
                AppendRecipes(allRecipes, extension.Recipes);
            }
        }

        private static List<ItemData> CopyItems(IReadOnlyList<ItemData> source)
        {
            var result = new List<ItemData>();
            AppendItems(result, source);
            return result;
        }

        private static List<CraftRecipe> CopyRecipes(IReadOnlyList<CraftRecipe> source)
        {
            var result = new List<CraftRecipe>();
            AppendRecipes(result, source);
            return result;
        }

        private static List<InventionData> CopyInventions(IReadOnlyList<InventionData> source)
        {
            var result = new List<InventionData>();
            if (source == null) return result;
            foreach (InventionData invention in source)
                if (invention != null)
                    result.Add(invention);
            return result;
        }

        private static void AppendItems(List<ItemData> target, IReadOnlyList<ItemData> source)
        {
            if (source == null) return;
            foreach (ItemData item in source)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.itemName)) continue;
                if (target.Exists(existing => existing != null && (existing.ContentId == item.ContentId || existing.itemName == item.itemName))) continue;
                target.Add(item);
            }
        }

        private static void AppendRecipes(List<CraftRecipe> target, IReadOnlyList<CraftRecipe> source)
        {
            if (source == null) return;
            foreach (CraftRecipe recipe in source)
            {
                if (recipe == null || string.IsNullOrWhiteSpace(recipe.recipeName)) continue;
                if (target.Exists(existing => existing != null && existing.ContentId == recipe.ContentId)) continue;
                target.Add(recipe);
            }
        }

        private static void AppendInventions(List<InventionData> target, IReadOnlyList<InventionData> source)
        {
            if (source == null) return;
            foreach (InventionData invention in source)
            {
                if (invention != null)
                    target.Add(invention);
            }
        }

        private static void AppendRecipeItems(List<ItemData> target, IReadOnlyList<CraftRecipe> recipes)
        {
            if (recipes == null) return;
            foreach (CraftRecipe recipe in recipes)
            {
                if (recipe?.outputItem != null)
                    AppendItems(target, new[] { recipe.outputItem });
                if (recipe?.ingredients == null) continue;
                foreach (RecipeIngredient ingredient in recipe.ingredients)
                    if (ingredient?.item != null)
                        AppendItems(target, new[] { ingredient.item });
            }
        }
    }
}
