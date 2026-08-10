using System.Collections.Generic;
using Cards3D;
using HuntingInDarkness.Data;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 营地桌面「猎人」区 presenter：用 HunterCard3D 填充自己的 SlotGrid。
    /// 区域（SlotGrid）与卡牌工厂在场景中预放置并 Inspector 连线；
    /// 未连线时由 SettlementTable3D 程序化回退注入（<see cref="SetRefs"/>）。
    /// </summary>
    public class HunterZone : MonoBehaviour
    {
        [SerializeField] private SlotGrid _grid;

        /// <summary>点击猎人卡回调（由上层注入）。</summary>
        public System.Action<HunterInstance> OnHunterClicked;

        private readonly List<HunterCard3D> _cards = new();

        /// <summary>程序化回退时由 SettlementTable3D 注入区域。</summary>
        public void SetRefs(SlotGrid grid) => _grid = grid;

        public void Fill(List<HunterInstance> hunters)
        {
            Clear();
            if (_grid == null) return;

            foreach (var h in hunters)
            {
                var card = EntityCreator.CreateHunterCard(h, transform);
                card.OnHunterClicked = c => OnHunterClicked?.Invoke(c.Hunter);
                _grid.TryPlaceCard(card);
                _cards.Add(card);
            }
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
