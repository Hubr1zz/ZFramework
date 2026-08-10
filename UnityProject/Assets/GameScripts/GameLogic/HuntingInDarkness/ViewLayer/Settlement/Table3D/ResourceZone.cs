using System.Collections.Generic;
using Cards3D;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 营地桌面「资源」区 presenter：用 ResourceCard3D 填充 SlotGrid，并响应资源数量变化。
    /// 区域与卡牌工厂在场景中预放置并 Inspector 连线；未连线时由 SettlementTable3D 注入。
    /// </summary>
    public class ResourceZone : MonoBehaviour
    {
        [SerializeField] private SlotGrid _grid;

        private readonly List<ResourceCard3D> _cards = new();

        public void SetRefs(SlotGrid grid) => _grid = grid;

        public void Fill(List<ResourceEntry> resources)
        {
            Clear();
            if (_grid == null) return;

            foreach (var entry in resources)
            {
                var card = EntityCreator.CreateResourceCard(entry.Key, entry.Value, transform);
                _grid.TryPlaceCard(card);
                _cards.Add(card);
            }
        }

        /// <summary>资源数量变化：命中已有卡则就地更新并返回 true，否则返回 false（需整区重填）。</summary>
        public bool TryUpdateCount(string resourceName, int newAmount)
        {
            foreach (var c in _cards)
            {
                if (c.ResourceName == resourceName)
                {
                    c.UpdateCount(newAmount);
                    return true;
                }
            }
            return false;
        }

        /// <summary>按当前数据刷新所有资源卡数量。</summary>
        public void RefreshCounts(SettlementManager mgr)
        {
            foreach (var card in _cards)
                card.UpdateCount(mgr.Data.GetResource(card.ResourceName));
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
