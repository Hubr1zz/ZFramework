using System;
using System.Collections.Generic;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.Data;

namespace HuntingInDarkness.Settlement
{
    /// <summary>把共享 occurrence 算法映射到现有 Settlement 存档 DTO。</summary>
    public sealed class SettlementEventChainCheckpointAdapter
    {
        private readonly SettlementInstance settlement;

        public SettlementEventChainCheckpointAdapter(SettlementInstance settlement)
        {
            this.settlement = settlement;
        }

        public IReadOnlyList<PlayableEventChainOccurrence> GetPending(string chainId)
        {
            SettlementEventChainCheckpoint checkpoint = FindCheckpoint(chainId);
            if (checkpoint?.PendingOccurrences == null) return Array.Empty<PlayableEventChainOccurrence>();
            var occurrences = new List<PlayableEventChainOccurrence>(checkpoint.PendingOccurrences.Count);
            foreach (SettlementEventChainOccurrence occurrence in checkpoint.PendingOccurrences)
                if (occurrence != null)
                    occurrences.Add(ToSharedOccurrence(occurrence));
            return occurrences;
        }

        public string GetDiagnostic(string chainId) => FindCheckpoint(chainId)?.Diagnostic ?? string.Empty;

        public IReadOnlyList<PlayableEventChainOccurrence> Commit(string chainId, int completedSequence, IReadOnlyList<string> childEventIds, int year, int actorId, IReadOnlyCollection<string> ancestorEventIds = null)
        {
            if (settlement == null) return Array.Empty<PlayableEventChainOccurrence>();
            IReadOnlyList<SettlementEventChainOccurrence> appended = settlement.CommitEventChainOccurrence(chainId, completedSequence, childEventIds, year, actorId, ancestorEventIds);
            if (appended == null || appended.Count == 0) return Array.Empty<PlayableEventChainOccurrence>();
            var result = new List<PlayableEventChainOccurrence>(appended.Count);
            foreach (SettlementEventChainOccurrence occurrence in appended)
                if (occurrence != null)
                    result.Add(ToSharedOccurrence(occurrence));
            return result;
        }

        public static PlayableEventChainOccurrence ToSharedOccurrence(SettlementEventChainOccurrence occurrence)
        {
            if (occurrence == null) return default;
            return new PlayableEventChainOccurrence(occurrence.Sequence, occurrence.EventId, occurrence.EventName, occurrence.Year, occurrence.ActorId, occurrence.AncestorEventIds);
        }

        private SettlementEventChainCheckpoint FindCheckpoint(string chainId)
        {
            string normalizedChainId = chainId?.Trim() ?? string.Empty;
            return settlement?.PendingEventChains?.Find(candidate => candidate != null && candidate.ChainId == normalizedChainId);
        }
    }
}
