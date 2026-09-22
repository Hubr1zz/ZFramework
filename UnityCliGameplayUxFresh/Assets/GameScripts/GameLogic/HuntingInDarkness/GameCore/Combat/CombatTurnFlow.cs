using System;

namespace HuntingInDarkness.GameCore.Combat
{
    public enum CombatTurnPhase
    {
        PlayerTurn,
        BossTurn,
        Transition
    }

    public readonly struct TurnPhaseChange
    {
        public CombatTurnPhase Previous { get; }
        public CombatTurnPhase Current { get; }

        public TurnPhaseChange(CombatTurnPhase previous, CombatTurnPhase current)
        {
            Previous = previous;
            Current = current;
        }
    }

    /// <summary>
    /// Authoritative combat turn state. Engine adapters execute presentation and side effects.
    /// </summary>
    public sealed class CombatTurnFlow
    {
        public CombatTurnPhase CurrentPhase { get; private set; } = CombatTurnPhase.Transition;
        public bool HasStarted { get; private set; }
        public bool IsBossActionCompletionPending { get; private set; }

        public TurnPhaseChange Start()
        {
            if (HasStarted)
                throw new InvalidOperationException("Combat turn flow has already started.");

            HasStarted = true;
            return TransitionTo(CombatTurnPhase.PlayerTurn);
        }

        public TurnPhaseChange TransitionTo(CombatTurnPhase next)
        {
            if (CurrentPhase == CombatTurnPhase.BossTurn &&
                next != CombatTurnPhase.BossTurn &&
                IsBossActionCompletionPending)
            {
                throw new InvalidOperationException(
                    "Boss actions must signal completion before leaving the boss turn.");
            }

            CombatTurnPhase previous = CurrentPhase;
            CurrentPhase = next;
            if (next == CombatTurnPhase.BossTurn)
                IsBossActionCompletionPending = true;
            return new TurnPhaseChange(previous, next);
        }

        public void SignalBossActionsCompleted()
        {
            if (CurrentPhase != CombatTurnPhase.BossTurn)
                throw new InvalidOperationException("Boss action completion is only valid during the boss turn.");

            IsBossActionCompletionPending = false;
        }
    }
}
