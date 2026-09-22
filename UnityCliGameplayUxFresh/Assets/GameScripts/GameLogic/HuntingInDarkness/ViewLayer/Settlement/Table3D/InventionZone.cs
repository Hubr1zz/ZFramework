using System.Collections.Generic;
using Cards3D;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace UI
{
    /// <summary>营地发明区：资格牌进入牌堆，点击牌堆随机抽取至多两张供玩家选择。</summary>
    public class InventionZone : MonoBehaviour
    {
        [SerializeField] private SlotGrid _grid;

        public System.Action<InventionCard3D> OnInventionEffectRequested;
        public System.Action<InventionCard3D> OnInventionUnlockRequested;

        private readonly List<InventionCard3D> drawnCards = new();
        private readonly List<InventionCard3D> masteredCards = new();
        private readonly List<InventionData> eligibleInventions = new();
        private InventionSystem inventionSystem;
        private InventionDeckCard3D deckCard;

        public int EligibleCount => eligibleInventions.Count;
        public int DrawnCount => drawnCards.Count;

        public void SetRefs(SlotGrid grid) => _grid = grid;

        public void Fill(InventionSystem system)
        {
            Clear();
            inventionSystem = system;
            if (_grid == null || inventionSystem == null) return;
            deckCard = InventionDeckCard3D.Create(transform);
            deckCard.DrawRequested = DrawCandidates;
            _grid.TryPlaceCard(deckCard);
            RefreshCards();
        }

        public void RefreshCards()
        {
            if (inventionSystem == null) return;
            bool unlockedDrawnCard = drawnCards.Exists(card => card != null && inventionSystem.IsUnlocked(card.Data));
            if (unlockedDrawnCard) ClearDrawnCards();
            SynchronizeMasteredCards();

            foreach (InventionCard3D card in drawnCards)
            {
                if (card == null) continue;
                bool canUnlock = inventionSystem.CanUnlock(card.Data, out string reason);
                card.ConfigureState(false, canUnlock, reason);
            }
            RebuildEligiblePool();
            deckCard?.Present(eligibleInventions.Count, drawnCards.Count > 0);
        }

        public void DrawCandidates()
        {
            if (drawnCards.Count > 0 || eligibleInventions.Count == 0 || _grid == null) return;
            int drawCount = Mathf.Min(2, eligibleInventions.Count);
            var pool = new List<InventionData>(eligibleInventions);
            for (int index = 0; index < drawCount; index++)
            {
                int selectedIndex = Random.Range(0, pool.Count);
                InventionData invention = pool[selectedIndex];
                pool.RemoveAt(selectedIndex);
                InventionCard3D card = EntityCreator.CreateInventionCard(invention, transform);
                card.OnEffectMenuRequested = selected => OnInventionEffectRequested?.Invoke(selected);
                card.OnUnlockRequested = selected => OnInventionUnlockRequested?.Invoke(selected);
                if (!_grid.TryPlaceCard(card))
                {
                    Destroy(card.gameObject);
                    break;
                }
                card.ConfigureState(false, true, string.Empty);
                drawnCards.Add(card);
            }
            RebuildEligiblePool();
            deckCard.Present(eligibleInventions.Count, drawnCards.Count > 0);
        }

        public void Clear()
        {
            ClearDrawnCards();
            ClearMasteredCards();
            if (deckCard != null) DestroyObject(deckCard.gameObject);
            deckCard = null;
            eligibleInventions.Clear();
            if (_grid == null) return;
            foreach (CardSlot slot in _grid.Slots) slot.ClearCard();
        }

        private void RebuildEligiblePool()
        {
            eligibleInventions.Clear();
            if (inventionSystem == null) return;
            foreach (InventionData invention in inventionSystem.AllInventions)
            {
                if (invention == null || inventionSystem.IsUnlocked(invention) || drawnCards.Exists(card => card != null && card.Data == invention)) continue;
                if (inventionSystem.CanUnlock(invention, out _)) eligibleInventions.Add(invention);
            }
        }

        private void ClearDrawnCards()
        {
            foreach (InventionCard3D card in drawnCards)
            {
                if (card == null) continue;
                card.CurrentSlot?.ClearCard();
                DestroyObject(card.gameObject);
            }
            drawnCards.Clear();
        }

        private void SynchronizeMasteredCards()
        {
            var desired = new HashSet<InventionData>();
            foreach (InventionData invention in inventionSystem.AllInventions)
                if (invention != null && inventionSystem.IsUnlocked(invention) && invention.activeEffects?.Count > 0)
                    desired.Add(invention);

            for (int index = masteredCards.Count - 1; index >= 0; index--)
            {
                InventionCard3D card = masteredCards[index];
                if (card != null && desired.Remove(card.Data))
                {
                    card.ConfigureState(true, false, string.Empty);
                    continue;
                }
                if (card != null)
                {
                    card.CurrentSlot?.ClearCard();
                    DestroyObject(card.gameObject);
                }
                masteredCards.RemoveAt(index);
            }

            foreach (InventionData invention in inventionSystem.AllInventions)
            {
                if (!desired.Contains(invention)) continue;
                InventionCard3D card = EntityCreator.CreateInventionCard(invention, transform);
                card.OnEffectMenuRequested = selected => OnInventionEffectRequested?.Invoke(selected);
                card.ConfigureState(true, false, string.Empty);
                if (_grid.TryPlaceCard(card))
                    masteredCards.Add(card);
                else
                    DestroyObject(card.gameObject);
            }
        }

        private void ClearMasteredCards()
        {
            foreach (InventionCard3D card in masteredCards)
            {
                if (card == null) continue;
                card.CurrentSlot?.ClearCard();
                DestroyObject(card.gameObject);
            }
            masteredCards.Clear();
        }

        private static void DestroyObject(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
