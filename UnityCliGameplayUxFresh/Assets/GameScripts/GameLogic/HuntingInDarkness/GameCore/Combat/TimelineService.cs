using System;
using System.Collections.Generic;
using System.Linq;

namespace HuntingInDarkness.GameCore.Combat
{
    public enum TimelineActionStatus
    {
        Ready,
        Exhausted,
        Overtime,
        Done
    }

    public enum AssistanceFailure
    {
        None,
        SameCharacter,
        HelperUnavailable,
        TargetUnavailable,
        TargetNotOvertime,
        InsufficientWillpower
    }

    public readonly struct AssistanceResult
    {
        public bool Success => Failure == AssistanceFailure.None;
        public AssistanceFailure Failure { get; }
        public TimePointChange? TimePointChange { get; }

        private AssistanceResult(AssistanceFailure failure, TimePointChange? timePointChange)
        {
            Failure = failure;
            TimePointChange = timePointChange;
        }

        public static AssistanceResult Rejected(AssistanceFailure failure) =>
            new AssistanceResult(failure, null);

        public static AssistanceResult Accepted(TimePointChange change) =>
            new AssistanceResult(AssistanceFailure.None, change);
    }

    public readonly struct TimePointChange
    {
        public int EntityId { get; }
        public bool IsBoss { get; }
        public int OldValue { get; }
        public int NewValue { get; }

        public TimePointChange(int entityId, bool isBoss, int oldValue, int newValue)
        {
            EntityId = entityId;
            IsBoss = isBoss;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    public sealed class TimelineState
    {
        public int EntityId { get; }
        public bool IsBoss { get; }
        public int CurrentTimePoints { get; internal set; }
        public int Limit { get; internal set; }
        public bool IsDoneThisTurn { get; internal set; }
        public int Willpower { get; internal set; }
        public TimelineActionStatus Status { get; internal set; } = TimelineActionStatus.Ready;

        internal TimelineState(int entityId, bool isBoss, int initialWillpower)
        {
            EntityId = entityId;
            IsBoss = isBoss;
            Willpower = Math.Max(0, initialWillpower);
        }
    }

    /// <summary>Engine-independent time-point and action-order rules.</summary>
    public sealed class TimelineService
    {
        public const int DefaultRoundLimit = 3;

        private readonly Dictionary<int, TimelineState> _entries =
            new Dictionary<int, TimelineState>();

        public int RoundLimit { get; private set; } = DefaultRoundLimit;
        public IEnumerable<TimelineState> Entries => _entries.Values;

        public TimelineState Register(int entityId, bool isBoss, int initialWillpower = 0)
        {
            var entry = new TimelineState(entityId, isBoss, initialWillpower);
            _entries[entityId] = entry;
            return entry;
        }

        public TimelineState Get(int entityId) =>
            _entries.TryGetValue(entityId, out TimelineState entry) ? entry : null;

        public void SetRoundLimit(int limit) => RoundLimit = Math.Max(1, limit);

        public TimePointChange? AddTimePoints(int entityId, int amount)
        {
            TimelineState entry = Get(entityId);
            if (entry == null)
                return null;

            TimePointChange change = SetTimePoints(entry, entry.CurrentTimePoints + amount);
            RefreshStatus(entry);
            return change;
        }

        public bool CanCharacterAct(int characterId, IEnumerable<int> activeCharacterIds)
        {
            TimelineState self = Get(characterId);
            if (self == null || self.IsBoss || self.IsDoneThisTurn)
                return false;

            var others = activeCharacterIds
                .Where(id => id != characterId)
                .Select(Get)
                .Where(entry => entry != null && !entry.IsBoss && !entry.IsDoneThisTurn)
                .ToList();

            return others.Count == 0 ||
                   !others.All(entry => self.CurrentTimePoints > entry.CurrentTimePoints);
        }

        public bool ShouldTransitionToBoss(IEnumerable<int> characterIds) =>
            characterIds.All(id => Get(id)?.IsDoneThisTurn == true);

        public void MarkCharacterDone(int characterId)
        {
            TimelineState entry = Get(characterId);
            if (entry != null && !entry.IsBoss)
            {
                entry.IsDoneThisTurn = true;
                entry.Status = TimelineActionStatus.Done;
            }
        }

        public AssistanceResult TryAssistOvertimeCharacter(int helperId, int targetId)
        {
            if (helperId == targetId)
                return AssistanceResult.Rejected(AssistanceFailure.SameCharacter);

            TimelineState helper = Get(helperId);
            if (helper == null || helper.IsBoss)
                return AssistanceResult.Rejected(AssistanceFailure.HelperUnavailable);

            TimelineState target = Get(targetId);
            if (target == null || target.IsBoss)
                return AssistanceResult.Rejected(AssistanceFailure.TargetUnavailable);
            if (target.Status != TimelineActionStatus.Overtime)
                return AssistanceResult.Rejected(AssistanceFailure.TargetNotOvertime);
            if (helper.Willpower < 1)
                return AssistanceResult.Rejected(AssistanceFailure.InsufficientWillpower);

            if (!TryRelieveOvertimeCharacter(targetId, out TimePointChange change))
                return AssistanceResult.Rejected(AssistanceFailure.TargetNotOvertime);

            helper.Willpower--;
            return AssistanceResult.Accepted(change);
        }

        public bool TryRelieveOvertimeCharacter(int targetId, out TimePointChange change)
        {
            change = default;
            TimelineState target = Get(targetId);
            if (target == null || target.IsBoss || target.Status != TimelineActionStatus.Overtime)
                return false;

            change = SetTimePoints(target, target.CurrentTimePoints + 1);
            RefreshStatus(target);
            return true;
        }

        public bool CanSpendWillpower(int characterId, int amount)
        {
            TimelineState character = Get(characterId);
            return amount >= 0 &&
                   character != null &&
                   !character.IsBoss &&
                   character.Willpower >= amount;
        }

        public bool TrySpendWillpower(int characterId, int amount)
        {
            if (!CanSpendWillpower(characterId, amount))
                return false;
            Get(characterId).Willpower -= amount;
            return true;
        }

        public List<TimePointChange> ProcessOverflowForNewPlayerTurn()
        {
            var changes = new List<TimePointChange>();
            foreach (TimelineState entry in _entries.Values)
            {
                if (entry.IsBoss)
                    continue;

                int overflow = entry.CurrentTimePoints > entry.Limit
                    ? entry.CurrentTimePoints - entry.Limit
                    : 0;
                changes.Add(SetTimePoints(entry, overflow > 0 ? -overflow : 0));
                entry.Limit = RoundLimit;
                entry.IsDoneThisTurn = false;
                RefreshStatus(entry);
            }
            return changes;
        }

        public void Reset()
        {
            foreach (TimelineState entry in _entries.Values)
            {
                entry.CurrentTimePoints = 0;
                entry.Limit = 0;
                entry.IsDoneThisTurn = false;
                entry.Status = TimelineActionStatus.Ready;
            }
            RoundLimit = DefaultRoundLimit;
        }

        private static TimePointChange SetTimePoints(TimelineState entry, int value)
        {
            int oldValue = entry.CurrentTimePoints;
            entry.CurrentTimePoints = value;
            return new TimePointChange(entry.EntityId, entry.IsBoss, oldValue, value);
        }

        private static void RefreshStatus(TimelineState entry)
        {
            if (entry.IsBoss)
                return;

            if (entry.CurrentTimePoints > entry.Limit)
            {
                entry.Status = TimelineActionStatus.Exhausted;
                entry.IsDoneThisTurn = true;
                return;
            }

            if (entry.CurrentTimePoints < -entry.Limit)
            {
                entry.Status = TimelineActionStatus.Overtime;
                entry.IsDoneThisTurn = true;
                return;
            }

            entry.Status = TimelineActionStatus.Ready;
            entry.IsDoneThisTurn = false;
        }
    }
}
