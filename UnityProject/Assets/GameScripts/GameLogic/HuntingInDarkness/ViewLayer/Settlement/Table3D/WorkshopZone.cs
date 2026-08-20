using System.Collections.Generic;
using Cards3D;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 营地桌面「工坊 / 建筑」区 presenter：用 WorkshopCard3D 填充 SlotGrid。
    /// 按已解锁配方的 requiredWorkshopId 生成工坊卡；卡牌只发起 Adapter 命令，不拥有制作状态。
    /// </summary>
    public class WorkshopZone : MonoBehaviour
    {
        [SerializeField] private SlotGrid _grid;

        /// <summary>点击工坊卡回调（由上层注入，弹出可制造物品面板）。</summary>
        public System.Action<WorkshopCard3D> OnWorkshopClicked;
        public System.Func<CraftRecipe, UniTask<SettlementCraftCommandResult>> OnCraftRequested;

        private readonly List<WorkshopCard3D> _cards = new();

        public void SetRefs(SlotGrid grid) => _grid = grid;

        public void Fill(WorkshopSystem workshop)
        {
            Clear();
            if (_grid == null || workshop == null) return;

            var recipesByWorkshop = new Dictionary<string, List<CraftRecipe>>();
            foreach (CraftRecipe recipe in workshop.GetAvailableRecipes())
            {
                if (recipe == null) continue;
                string workshopId = string.IsNullOrWhiteSpace(recipe.requiredWorkshopId) ? "共享工坊" : recipe.requiredWorkshopId;
                if (!recipesByWorkshop.TryGetValue(workshopId, out List<CraftRecipe> recipes))
                {
                    recipes = new List<CraftRecipe>();
                    recipesByWorkshop.Add(workshopId, recipes);
                }
                recipes.Add(recipe);
            }

            foreach (KeyValuePair<string, List<CraftRecipe>> pair in recipesByWorkshop)
            {
                WorkshopCard3D card = EntityCreator.CreateWorkshopCard(pair.Key, $"可制作 {pair.Value.Count} 种物品", transform);
                card.Configure(pair.Value, craftCommand: OnCraftRequested);
                card.OnCraftMenuRequested = clicked => OnWorkshopClicked?.Invoke(clicked);
                _grid.TryPlaceCard(card);
                _cards.Add(card);
            }
        }

        /// <summary>刷新工坊卡视觉状态。</summary>
        public void RefreshCards()
        {
            foreach (var c in _cards) c.Refresh();
        }

        public void Clear()
        {
            foreach (var c in _cards)
                if (c != null) Destroy(c.gameObject);
            _cards.Clear();
            if (_grid != null)
                foreach (var slot in _grid.Slots) slot.ClearCard();
        }
    }
}
