using System;
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
        private List<HunterInstance> pendingHunters;
        private int activeDragCount;

        /// <summary>程序化回退时由 SettlementTable3D 注入区域。</summary>
        public void SetRefs(SlotGrid grid) => _grid = grid;

        public void Fill(List<HunterInstance> hunters)
        {
            if (_grid == null) return;
            List<HunterInstance> snapshot = CopyHunters(hunters);
            if (activeDragCount > 0)
            {
                pendingHunters = snapshot;
                return;
            }
            ApplySnapshot(snapshot);
        }

        private void ApplySnapshot(IReadOnlyList<HunterInstance> hunters)
        {
            var desiredHunters = new Dictionary<int, HunterInstance>();
            foreach (HunterInstance hunter in hunters)
                desiredHunters[hunter.InstanceId] = hunter;

            var retainedIds = new HashSet<int>();
            for (int index = _cards.Count - 1; index >= 0; index--)
            {
                HunterCard3D card = _cards[index];
                if (card == null)
                {
                    _cards.RemoveAt(index);
                    continue;
                }
                int hunterId = card.Hunter?.InstanceId ?? 0;
                if (!desiredHunters.TryGetValue(hunterId, out HunterInstance hunter) || !retainedIds.Add(hunterId))
                {
                    RemoveCardAt(index);
                    continue;
                }
                card.Refresh(hunter);
            }

            foreach (HunterInstance hunter in hunters)
            {
                if (retainedIds.Contains(hunter.InstanceId)) continue;
                HunterCard3D card = EntityCreator.CreateHunterCard(hunter, transform);
                card.OnHunterClicked = c => OnHunterClicked?.Invoke(c.Hunter);
                Subscribe(card);
                if (!_grid.TryPlaceCard(card))
                {
                    Unsubscribe(card);
                    Destroy(card.gameObject);
                    Debug.LogWarning($"猎人区没有可用卡槽：{hunter.InstanceId}");
                    continue;
                }
                _cards.Add(card);
                retainedIds.Add(hunter.InstanceId);
            }
        }

        private static List<HunterInstance> CopyHunters(IReadOnlyList<HunterInstance> hunters)
        {
            var snapshot = new List<HunterInstance>();
            var hunterIds = new HashSet<int>();
            foreach (HunterInstance hunter in hunters ?? Array.Empty<HunterInstance>())
                if (hunter != null && hunterIds.Add(hunter.InstanceId))
                    snapshot.Add(hunter);
            return snapshot;
        }

        private void Subscribe(HunterCard3D card)
        {
            card.DragStarted += OnCardDragStarted;
            card.DragEnded += OnCardDragEnded;
        }

        private void Unsubscribe(HunterCard3D card)
        {
            if (card == null) return;
            card.DragStarted -= OnCardDragStarted;
            card.DragEnded -= OnCardDragEnded;
        }

        private void OnCardDragStarted(CardView3D _) => activeDragCount++;

        private void OnCardDragEnded(CardView3D _)
        {
            activeDragCount = Mathf.Max(0, activeDragCount - 1);
            if (activeDragCount > 0 || pendingHunters == null) return;
            List<HunterInstance> snapshot = pendingHunters;
            pendingHunters = null;
            ApplySnapshot(snapshot);
        }

        private void RemoveCardAt(int index)
        {
            HunterCard3D card = _cards[index];
            Unsubscribe(card);
            card.CurrentSlot?.ClearCard();
            Destroy(card.gameObject);
            _cards.RemoveAt(index);
        }

        public void Clear()
        {
            foreach (HunterCard3D card in _cards)
            {
                Unsubscribe(card);
                if (card == null) continue;
                card.CurrentSlot?.ClearCard();
                Destroy(card.gameObject);
            }
            _cards.Clear();
            pendingHunters = null;
            activeDragCount = 0;
            if (_grid != null)
                foreach (CardSlot slot in _grid.Slots) slot.ClearCard();
        }
    }
}
