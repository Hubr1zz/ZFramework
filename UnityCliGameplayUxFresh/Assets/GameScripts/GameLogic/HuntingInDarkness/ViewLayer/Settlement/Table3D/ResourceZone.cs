using System;
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
        private List<ResourceEntry> pendingResources;
        private int activeDragCount;

        public void SetRefs(SlotGrid grid) => _grid = grid;

        public void Synchronize(IReadOnlyList<ResourceEntry> resources)
        {
            if (_grid == null) return;
            List<ResourceEntry> snapshot = CopyPositiveResources(resources);
            if (activeDragCount > 0)
            {
                pendingResources = snapshot;
                return;
            }
            ApplySnapshot(snapshot);
        }

        private void ApplySnapshot(IReadOnlyList<ResourceEntry> resources)
        {
            var desiredAmounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (ResourceEntry entry in resources)
                desiredAmounts[entry.Key] = entry.Value;

            var retainedIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = _cards.Count - 1; index >= 0; index--)
            {
                ResourceCard3D card = _cards[index];
                if (card == null)
                {
                    _cards.RemoveAt(index);
                    continue;
                }
                if (!desiredAmounts.TryGetValue(card.ResourceId, out int amount) || !retainedIds.Add(card.ResourceId))
                {
                    RemoveCardAt(index);
                    continue;
                }
                card.UpdateCount(amount);
            }

            foreach (ResourceEntry entry in resources)
            {
                if (retainedIds.Contains(entry.Key)) continue;
                string displayName = PlayableSettlementItemRegistry.GetDisplayName(entry.Key);
                ResourceCard3D card = EntityCreator.CreateResourceCard(entry.Key, displayName, entry.Value, transform);
                Subscribe(card);
                if (!_grid.TryPlaceCard(card))
                {
                    Unsubscribe(card);
                    Destroy(card.gameObject);
                    Debug.LogWarning($"资源区没有可用卡槽：{entry.Key}");
                    continue;
                }
                _cards.Add(card);
                retainedIds.Add(entry.Key);
            }
        }

        private static List<ResourceEntry> CopyPositiveResources(IReadOnlyList<ResourceEntry> resources)
        {
            var snapshot = new List<ResourceEntry>();
            foreach (ResourceEntry entry in resources ?? Array.Empty<ResourceEntry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || entry.Value <= 0) continue;
                snapshot.Add(new ResourceEntry { Key = entry.Key, Value = entry.Value });
            }
            return snapshot;
        }

        private void Subscribe(ResourceCard3D card)
        {
            card.DragStarted += OnCardDragStarted;
            card.DragEnded += OnCardDragEnded;
        }

        private void Unsubscribe(ResourceCard3D card)
        {
            if (card == null) return;
            card.DragStarted -= OnCardDragStarted;
            card.DragEnded -= OnCardDragEnded;
        }

        private void OnCardDragStarted(CardView3D _) => activeDragCount++;

        private void OnCardDragEnded(CardView3D _)
        {
            activeDragCount = Mathf.Max(0, activeDragCount - 1);
            if (activeDragCount > 0 || pendingResources == null) return;
            List<ResourceEntry> snapshot = pendingResources;
            pendingResources = null;
            ApplySnapshot(snapshot);
        }

        private void RemoveCardAt(int index)
        {
            ResourceCard3D card = _cards[index];
            Unsubscribe(card);
            card.CurrentSlot?.ClearCard();
            Destroy(card.gameObject);
            _cards.RemoveAt(index);
        }

        public void Clear()
        {
            foreach (ResourceCard3D card in _cards)
            {
                Unsubscribe(card);
                if (card != null) Destroy(card.gameObject);
            }
            _cards.Clear();
            pendingResources = null;
            activeDragCount = 0;
            if (_grid != null)
                foreach (var slot in _grid.Slots) slot.ClearCard();
        }
    }
}
