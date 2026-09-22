using System.Collections.Generic;
using System.Linq;
using GameplayBase;
using HuntingInDarkness.GameCore.Combat;

namespace Core
{
    /// <summary>
    /// Compatibility read model over the engine-independent timeline state.
    /// Mutation is coordinated by <see cref="TimelineManager"/> so Unity events stay in the adapter.
    /// </summary>
    public sealed class TimelineEntry
    {
        private readonly TimelineState _state;

        internal TimelineEntry(TimelineState state) => _state = state;

        public int EntityId => _state.EntityId;
        public bool IsBoss => _state.IsBoss;
        public int CurrentTimePoints => _state.CurrentTimePoints;
        public int Limit => _state.Limit;
        public bool IsDoneThisTurn => _state.IsDoneThisTurn;
        public int Willpower => _state.Willpower;
        public TimelineActionStatus Status => _state.Status;
    }

    /// <summary>
    /// Unity adapter for the pure <see cref="TimelineService"/> rules.
    /// It translates domain changes into the legacy EventBus contract consumed by views.
    /// </summary>
    public class TimelineManager
    {
        private readonly TimelineService _timeline = new();

        public int RoundLimit => _timeline.RoundLimit;

        public void RegisterCharacter(int characterId, int initialWillpower) =>
            _timeline.Register(characterId, isBoss: false, initialWillpower);

        public void RegisterBoss(int bossId) =>
            _timeline.Register(bossId, isBoss: true);

        public int GetTimePoints(int entityId) =>
            _timeline.Get(entityId)?.CurrentTimePoints ?? 0;

        public int GetLimit(int characterId) =>
            _timeline.Get(characterId)?.Limit ?? RoundLimit;

        public bool HasExceededLimit(int characterId)
        {
            TimelineState entry = _timeline.Get(characterId);
            return entry != null && entry.CurrentTimePoints > entry.Limit;
        }

        public bool IsCharacterDone(int characterId) =>
            _timeline.Get(characterId)?.IsDoneThisTurn == true;

        public TimelineActionStatus GetStatus(int characterId) =>
            _timeline.Get(characterId)?.Status ?? TimelineActionStatus.Done;

        public int GetWillpower(int characterId) =>
            _timeline.Get(characterId)?.Willpower ?? 0;

        public bool Contains(int characterId) => _timeline.Get(characterId) != null;

        public bool CanSpendWillpower(int characterId, int amount) =>
            _timeline.CanSpendWillpower(characterId, amount);

        public bool TrySpendWillpower(int characterId, int amount) =>
            _timeline.TrySpendWillpower(characterId, amount);

        public bool CanCharacterAct(int characterId, IGameContext context) =>
            _timeline.CanCharacterAct(characterId, context.PlayerCharacters.Select(c => c.Id));

        public bool ShouldTransitionToBoss(IGameContext context) =>
            _timeline.ShouldTransitionToBoss(context.PlayerCharacters.Select(c => c.Id));

        public void SetRoundLimit(int limit) => _timeline.SetRoundLimit(limit);

        public void AccumulateTimePoints(int entityId, int amount)
        {
            TimePointChange? change = _timeline.AddTimePoints(entityId, amount);
            if (change.HasValue)
                Publish(change.Value);
        }

        public void MarkCharacterDone(int characterId) =>
            _timeline.MarkCharacterDone(characterId);

        public AssistanceResult TryAssistOvertimeCharacter(int helperId, int targetId)
        {
            AssistanceResult result = _timeline.TryAssistOvertimeCharacter(helperId, targetId);
            if (result.TimePointChange.HasValue)
                Publish(result.TimePointChange.Value);
            return result;
        }

        public bool TryRelieveOvertimeCharacter(int targetId)
        {
            if (!_timeline.TryRelieveOvertimeCharacter(targetId, out TimePointChange change))
                return false;

            Publish(change);
            return true;
        }

        public void ProcessOverflowForNewPlayerTurn()
        {
            foreach (TimePointChange change in _timeline.ProcessOverflowForNewPlayerTurn())
                Publish(change);
        }

        public List<TimelineEntry> GetSortedTimeline() =>
            _timeline.Entries
                .OrderBy(entry => entry.CurrentTimePoints)
                .Select(entry => new TimelineEntry(entry))
                .ToList();

        public void Reset() => _timeline.Reset();

        private static void Publish(TimePointChange change)
        {
            EventBus.Publish(new TimePointChangedEvent
            {
                EntityId = change.EntityId,
                IsBoss = change.IsBoss,
                OldValue = change.OldValue,
                NewValue = change.NewValue
            });
        }
    }
}
