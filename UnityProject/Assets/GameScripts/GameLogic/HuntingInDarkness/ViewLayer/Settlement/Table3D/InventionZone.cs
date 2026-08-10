using System.Collections.Generic;
using Cards3D;
using HuntingInDarkness.Data;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 营地桌面「发明」区 presenter：用 InventionCard3D 填充 SlotGrid。
    /// 区域与卡牌工厂在场景中预放置并 Inspector 连线；未连线时由 SettlementTable3D 注入。
    /// </summary>
    public class InventionZone : MonoBehaviour
    {
        [SerializeField] private SlotGrid _grid;

        /// <summary>点击发明卡（有主动效果时）回调，由上层展示效果选择面板。</summary>
        public System.Action<InventionCard3D> OnInventionEffectRequested;

        private readonly List<InventionCard3D> _cards = new();

        public void SetRefs(SlotGrid grid) => _grid = grid;

        public void Fill(List<InventionData> inventions)
        {
            Clear();
            if (_grid == null) return;

            foreach (var inv in inventions)
            {
                var card = EntityCreator.CreateInventionCard(inv, transform);
                card.OnEffectMenuRequested = c => OnInventionEffectRequested?.Invoke(c);
                _grid.TryPlaceCard(card);
                _cards.Add(card);
            }
        }

        /// <summary>刷新发明卡视觉状态（解锁/可解锁/锁定）。</summary>
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
