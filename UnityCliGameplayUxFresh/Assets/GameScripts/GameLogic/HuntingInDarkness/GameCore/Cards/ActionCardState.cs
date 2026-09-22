using System;
using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Cards
{
    public enum ActionCardFace
    {
        FaceUp,
        FaceDown
    }

    public sealed class ActionCardState
    {
        public int InstanceId { get; }
        public int OwnerId { get; set; }
        public bool IsDiscardable { get; }
        public bool ResetsEachTurn { get; }
        public ActionCardFace Face { get; private set; } = ActionCardFace.FaceUp;
        public bool IsAvailableThisTurn { get; private set; } = true;

        public ActionCardState(
            int instanceId,
            int ownerId,
            bool isDiscardable,
            bool resetsEachTurn = false)
        {
            if (instanceId <= 0) throw new ArgumentOutOfRangeException(nameof(instanceId));
            InstanceId = instanceId;
            OwnerId = ownerId;
            IsDiscardable = isDiscardable;
            ResetsEachTurn = resetsEachTurn;
        }

        public bool CanDiscard =>
            Face == ActionCardFace.FaceUp && IsDiscardable && IsAvailableThisTurn;
        public bool CanPlay => Face == ActionCardFace.FaceUp && IsAvailableThisTurn;
        public void Flip() => Face = ActionCardFace.FaceDown;
        public void Restore() => Face = ActionCardFace.FaceUp;
        public void SetFace(ActionCardFace face) => Face = face;
        public void MarkUsed()
        {
            if (ResetsEachTurn)
                IsAvailableThisTurn = false;
        }

        public void ResetForNewTurn() => IsAvailableThisTurn = true;
    }

    public static class CardConditionRules
    {
        public static bool AllMatchingConditionsPass<TCondition, TTiming>(
            IReadOnlyList<TCondition> conditions,
            TTiming timing,
            Func<TCondition, TTiming> getTiming,
            Func<TCondition, bool> evaluate)
        {
            bool found = false;
            var comparer = EqualityComparer<TTiming>.Default;
            foreach (TCondition condition in conditions)
            {
                if (!comparer.Equals(getTiming(condition), timing))
                    continue;
                found = true;
                if (!evaluate(condition))
                    return false;
            }
            return found;
        }
    }
}
