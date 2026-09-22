using System.Collections.Generic;
using HuntingInDarkness.Data;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    /// <summary>无需改写基础内容目录即可追加物品与配方的可发现内容包。</summary>
    [CreateAssetMenu(fileName = "PlayableSettlementContentExtension", menuName = "Hunting in Darkness/Settlement Content Extension")]
    public sealed class PlayableSettlementContentExtension : ScriptableObject
    {
        [SerializeField] private List<ItemData> items = new();
        [SerializeField] private List<CraftRecipe> recipes = new();

        public IReadOnlyList<ItemData> Items => items;
        public IReadOnlyList<CraftRecipe> Recipes => recipes;
    }
}
