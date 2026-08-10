using System.Collections.Generic;
using Cards3D;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 营地桌面「工坊 / 建筑」区 presenter：用 WorkshopCard3D 填充 SlotGrid。
    /// 暂无工坊建筑数据（待 WorkshopData SO 定义），<see cref="Fill"/> 预留空实现。
    /// </summary>
    public class WorkshopZone : MonoBehaviour
    {
        [SerializeField] private SlotGrid _grid;

        /// <summary>点击工坊卡回调（由上层注入，弹出可制造物品面板）。</summary>
        public System.Action<WorkshopCard3D> OnWorkshopClicked;

        private readonly List<WorkshopCard3D> _cards = new();

        public void SetRefs(SlotGrid grid) => _grid = grid;

        public void Fill()
        {
            Clear();
            if (_grid == null) return;

            // TODO: WorkshopData SO 定义后，从工坊建筑数据填充工坊卡：
            // foreach (var ws in mgr.Workshop.GetBuildings())
            // {
            //     var card = EntityCreator.CreateWorkshopCard(ws.name, ws.description, transform, ws.icon);
            //     card.OnCraftMenuRequested = c => OnWorkshopClicked?.Invoke(c);
            //     _grid.TryPlaceCard(card);
            //     _cards.Add(card);
            // }
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
