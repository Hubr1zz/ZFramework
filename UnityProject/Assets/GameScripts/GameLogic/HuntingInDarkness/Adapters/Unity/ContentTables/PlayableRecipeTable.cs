using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Content;
using UnityEngine;

namespace HuntingInDarkness.ContentTables
{
    [Serializable]
    public sealed class RecipeIngredientTableRecord
    {
        public string itemId;
        public int count = 1;
    }

    [Serializable]
    public sealed class CraftRecipeTableRecord : IStableContentRecord
    {
        public string id;
        public string recipeName;
        public List<RecipeIngredientTableRecord> ingredients = new();
        public string outputItemId;
        public int outputCount = 1;
        public string requiredInventionId;
        public bool unlockedByMaterial;
        public string requiredWorkshopId;

        public string Id => id;
    }

    [Serializable]
    public sealed class CraftRecipeTableDocument
    {
        public int version = 1;
        public List<CraftRecipeTableRecord> recipes = new();
    }

    public sealed class JsonCraftRecipeTableSource : IContentTableSource<CraftRecipeTableRecord>
    {
        private readonly string resourcePath;

        public JsonCraftRecipeTableSource(string resourcePath)
        {
            this.resourcePath = resourcePath;
        }

        public IReadOnlyList<CraftRecipeTableRecord> Load()
        {
            TextAsset tableAsset = Resources.Load<TextAsset>(resourcePath);
            if (tableAsset == null)
            {
                Debug.LogWarning($"[ContentTable] 未找到配方表 Resources/{resourcePath}.json");
                return Array.Empty<CraftRecipeTableRecord>();
            }

            CraftRecipeTableDocument document = JsonUtility.FromJson<CraftRecipeTableDocument>(tableAsset.text);
            if (document?.recipes == null)
            {
                Debug.LogError($"[ContentTable] 配方表格式无效：{resourcePath}");
                return Array.Empty<CraftRecipeTableRecord>();
            }
            if (document.version != 1)
                Debug.LogWarning($"[ContentTable] 配方表版本 {document.version} 尚未显式支持，将按版本 1 读取。");
            return document.recipes;
        }
    }

    /// <summary>通过稳定物品资产 ID 解析配方，产出仍复用现有 CraftRecipe 与 Settlement ActionQueue。</summary>
    public static class PlayableCraftRecipeTableRuntime
    {
        private const string TablePath = "HuntingInDarkness/Tables/recipes";

        public static IReadOnlyList<CraftRecipe> GetRecipes(IReadOnlyList<ItemData> items, IReadOnlyList<InventionData> inventions)
        {
            return Build(new JsonCraftRecipeTableSource(TablePath).Load(), items, inventions, message => Debug.LogError($"[ContentTable] {message}"));
        }

        public static List<CraftRecipe> Build(IReadOnlyList<CraftRecipeTableRecord> records, IReadOnlyList<ItemData> items, IReadOnlyList<InventionData> inventions = null, Action<string> reportError = null)
        {
            var result = new List<CraftRecipe>();
            if (records == null) return result;

            Dictionary<string, ItemData> itemById = BuildAssetIndex(items, item => item.ContentId, "物品", reportError);
            Dictionary<string, InventionData> inventionById = BuildAssetIndex(inventions, invention => invention.name, "发明", reportError);
            Dictionary<string, int> idCounts = Count(records, record => record?.id);
            Dictionary<string, int> nameCounts = Count(records, record => record?.recipeName);
            var reportedDuplicates = new HashSet<string>(StringComparer.Ordinal);
            foreach (CraftRecipeTableRecord record in records)
            {
                string id = record?.id?.Trim() ?? string.Empty;
                string recipeName = record?.recipeName?.Trim() ?? string.Empty;
                bool duplicateId = idCounts.TryGetValue(id, out int idCount) && idCount > 1;
                bool duplicateName = nameCounts.TryGetValue(recipeName, out int nameCount) && nameCount > 1;
                if (duplicateId || duplicateName)
                {
                    if (duplicateId && reportedDuplicates.Add($"id:{id}")) reportError?.Invoke($"配方表存在重复记录：id:{id}");
                    if (duplicateName && reportedDuplicates.Add($"name:{recipeName}")) reportError?.Invoke($"配方表存在重复记录：name:{recipeName}");
                    continue;
                }
                if (!TryCreate(record, itemById, inventionById, out CraftRecipe recipe, out string error))
                {
                    reportError?.Invoke(error);
                    continue;
                }
                result.Add(recipe);
            }
            return result;
        }

        private static bool TryCreate(CraftRecipeTableRecord record, IReadOnlyDictionary<string, ItemData> itemById, IReadOnlyDictionary<string, InventionData> inventionById, out CraftRecipe recipe, out string error)
        {
            recipe = null;
            if (record == null || string.IsNullOrWhiteSpace(record.id) || string.IsNullOrWhiteSpace(record.recipeName))
            {
                error = "配方记录缺少稳定 id 或名称。";
                return false;
            }
            if (record.outputCount <= 0 || !TryResolve(itemById, record.outputItemId, out ItemData outputItem))
            {
                error = $"配方 {record.id} 的产物无效：{record.outputItemId}";
                return false;
            }
            if (!TryBuildIngredients(record, itemById, out List<RecipeIngredient> ingredients, out error)) return false;

            InventionData requiredInvention = null;
            string inventionId = record.requiredInventionId?.Trim() ?? string.Empty;
            if (inventionId.Length > 0 && !inventionById.TryGetValue(inventionId, out requiredInvention))
            {
                error = $"配方 {record.id} 引用了未知发明：{inventionId}";
                return false;
            }

            recipe = new CraftRecipe
            {
                recipeName = record.recipeName.Trim(),
                ingredients = ingredients,
                outputItem = outputItem,
                outputCount = record.outputCount,
                requiredInvention = requiredInvention,
                unlockedByMaterial = record.unlockedByMaterial,
                requiredWorkshopId = record.requiredWorkshopId?.Trim() ?? string.Empty
            };
            error = string.Empty;
            return true;
        }

        private static bool TryBuildIngredients(CraftRecipeTableRecord record, IReadOnlyDictionary<string, ItemData> itemById, out List<RecipeIngredient> ingredients, out string error)
        {
            ingredients = new List<RecipeIngredient>();
            error = string.Empty;
            var amounts = new Dictionary<ItemData, int>();
            var itemOrder = new List<ItemData>();
            if (record.ingredients != null)
            {
                foreach (RecipeIngredientTableRecord ingredient in record.ingredients)
                {
                    if (ingredient == null || ingredient.count <= 0 || !TryResolve(itemById, ingredient.itemId, out ItemData item))
                    {
                        error = $"配方 {record.id} 含无效原料：{ingredient?.itemId}";
                        return false;
                    }
                    long combined = (amounts.TryGetValue(item, out int count) ? count : 0L) + ingredient.count;
                    if (combined > int.MaxValue)
                    {
                        error = $"配方 {record.id} 的原料数量溢出：{ingredient.itemId}";
                        return false;
                    }
                    if (!amounts.ContainsKey(item)) itemOrder.Add(item);
                    amounts[item] = (int)combined;
                }
            }
            if (amounts.Count == 0)
            {
                error = $"配方 {record.id} 至少需要一种原料。";
                return false;
            }
            foreach (ItemData item in itemOrder)
                ingredients.Add(new RecipeIngredient { item = item, count = amounts[item] });
            return true;
        }

        private static bool TryResolve(IReadOnlyDictionary<string, ItemData> itemById, string id, out ItemData item)
        {
            item = null;
            string key = id?.Trim() ?? string.Empty;
            return key.Length > 0 && itemById.TryGetValue(key, out item) && item != null;
        }

        private static Dictionary<string, T> BuildAssetIndex<T>(IReadOnlyList<T> assets, Func<T, string> selectId, string label, Action<string> reportError) where T : UnityEngine.Object
        {
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            if (assets == null) return result;
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (T asset in assets)
            {
                string id = asset != null ? selectId(asset)?.Trim() ?? string.Empty : string.Empty;
                if (id.Length == 0) continue;
                counts[id] = counts.TryGetValue(id, out int count) ? count + 1 : 1;
            }
            var reported = new HashSet<string>(StringComparer.Ordinal);
            foreach (T asset in assets)
            {
                string id = asset != null ? selectId(asset)?.Trim() ?? string.Empty : string.Empty;
                if (id.Length == 0) continue;
                if (counts[id] > 1)
                {
                    if (reported.Add(id)) reportError?.Invoke($"{label}目录存在重复稳定 id：{id}");
                    continue;
                }
                result.Add(id, asset);
            }
            return result;
        }

        private static Dictionary<string, int> Count(IReadOnlyList<CraftRecipeTableRecord> records, Func<CraftRecipeTableRecord, string> selector)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (CraftRecipeTableRecord record in records)
            {
                string key = selector(record)?.Trim() ?? string.Empty;
                if (key.Length == 0) continue;
                counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
            }
            return counts;
        }
    }
}
