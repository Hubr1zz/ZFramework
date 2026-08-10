using System.Collections.Generic;
using GameplayBase;

namespace Core
{
    /// <summary>Boss 运行时状态数据。</summary>
    public class BossRuntimeData : IBossState
    {
        public int    Id   { get; set; }
        public string Name { get; set; }
        public int    CurrentTimePoints { get; set; }

        private readonly List<int> _pendingActionCardIds = new();
        public IReadOnlyList<int> PendingActionCardIds => _pendingActionCardIds;

        private readonly List<int> _revealedNextCardIds = new();
        public IReadOnlyList<int> RevealedNextCardIds => _revealedNextCardIds;

        public void SetPendingActions(List<int> cardIds)
        {
            _pendingActionCardIds.Clear();
            _pendingActionCardIds.AddRange(cardIds);
        }

        public void SetRevealedNext(List<int> cardIds)
        {
            _revealedNextCardIds.Clear();
            _revealedNextCardIds.AddRange(cardIds);
        }

        public void PromoteRevealedToPending()
        {
            _pendingActionCardIds.Clear();
            _pendingActionCardIds.AddRange(_revealedNextCardIds);
            _revealedNextCardIds.Clear();
        }
    }
}
