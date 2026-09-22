using System;
using GameplayBase;
using HuntingInDarkness.ActionFlow.Campaign;
using UnityEngine;

namespace HuntingInDarkness.ActionFlow.Hunt
{
    internal sealed class HuntEncounterAccumulator
    {
        private readonly Guid huntSessionId;
        private readonly string defaultEncounterId;
        private readonly string destinationId;

        internal HuntEncounterAccumulator(Guid huntSessionId, string defaultEncounterId, string destinationId)
        {
            this.huntSessionId = huntSessionId;
            this.defaultEncounterId = string.IsNullOrWhiteSpace(defaultEncounterId) ? "default" : defaultEncounterId.Trim();
            this.destinationId = destinationId ?? string.Empty;
        }

        internal bool HasRequest { get; private set; }
        internal CampaignEncounterRequest Request { get; private set; }

        internal bool TryAdd(string encounterId, CampaignEncounterSourceKind sourceKind, Vector2Int sourceCoordinate, string sourceEventId)
        {
            if (HasRequest) return false;
            string resolvedId = string.IsNullOrWhiteSpace(encounterId) ? defaultEncounterId : encounterId.Trim();
            Request = new CampaignEncounterRequest(huntSessionId, resolvedId, sourceKind, GamePhase.Hunt, sourceCoordinate, sourceEventId, destinationId);
            HasRequest = true;
            return true;
        }
    }
}
