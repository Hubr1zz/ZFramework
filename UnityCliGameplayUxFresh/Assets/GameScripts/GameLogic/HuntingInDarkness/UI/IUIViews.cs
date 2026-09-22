// ─── UI 视图接口 ──────────────────────────────────────────────────────────────

using GameplayBase;

namespace UI
{
    public interface ICardView
    {
        void OnCardUpdated(ICharacterActionCardInstanceState state);
        void OnCardFlipped(int cardInstanceId, CardFace newFace);
        void OnCardPlayed(int cardInstanceId);
    }

    public interface ITimelineView
    {
        void OnTimePointChanged(int entityId, bool isBoss, int newValue);
        void OnEntityExhausted(int entityId);
    }

    public interface ITurnView
    {
        void OnPhaseChanged(TurnPhase newPhase, int turnNumber);
        void OnCharacterSelectable(int characterId, bool selectable);
    }
}
