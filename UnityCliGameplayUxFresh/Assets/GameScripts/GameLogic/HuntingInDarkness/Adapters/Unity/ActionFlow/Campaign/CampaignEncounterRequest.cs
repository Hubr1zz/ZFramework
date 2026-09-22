using System;
using GameplayBase;
using UnityEngine;

namespace HuntingInDarkness.ActionFlow.Campaign
{
    public enum CampaignEncounterSourceKind
    {
        HuntBossTile,
        HuntEvent,
        SettlementEvent
    }

    /// <summary>由阶段环境提交给 Campaign Runner 的不可变遭遇请求。</summary>
    public readonly struct CampaignEncounterRequest
    {
        public CampaignEncounterRequest(Guid sourceSessionId, string encounterId, CampaignEncounterSourceKind sourceKind, GamePhase sourcePhase, Vector2Int sourceCoordinate, string sourceEventId, string sourceContextId)
        {
            SourceSessionId = sourceSessionId;
            EncounterId = encounterId ?? string.Empty;
            SourceKind = sourceKind;
            SourcePhase = sourcePhase;
            SourceCoordinate = sourceCoordinate;
            SourceEventId = sourceEventId ?? string.Empty;
            SourceContextId = sourceContextId ?? string.Empty;
        }

        public Guid SourceSessionId { get; }
        public string EncounterId { get; }
        public CampaignEncounterSourceKind SourceKind { get; }
        public GamePhase SourcePhase { get; }
        public Vector2Int SourceCoordinate { get; }
        public string SourceEventId { get; }
        public string SourceContextId { get; }
        public bool IsValid => SourceSessionId != Guid.Empty && !string.IsNullOrWhiteSpace(EncounterId);
    }

    public struct CampaignEncounterRequestedEvent
    {
        public CampaignEncounterRequest Request;
    }
}
