using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;

namespace HuntingInDarkness.Settlement
{
    /// <summary>锁定一次事件选择与骰值，直到玩家接受结果时才提交效果。</summary>
    public sealed class PlayableEventChoiceTransaction
    {
        private readonly EventSystem eventSystem;
        private readonly EventData gameEvent;
        private readonly EventOption option;
        private readonly HunterInstance actor;
        private readonly int optionIndex;
        private EventResolutionResult committedResult;
        private bool continued;

        public EventData GameEvent => gameEvent;
        public EventOption Option => option;
        public HunterInstance Actor => actor;
        public bool RequiresCheck => option.checkType != CheckType.None;
        public int RollValue { get; private set; }
        public int Bonus { get; }
        public int Total => RollValue + Bonus;
        public int Target => option.checkTarget;
        public bool Success => !RequiresCheck || EventRules.CheckSucceeded(RollValue, Bonus, Target);
        public bool HasRerolled { get; private set; }
        public bool IsCommitted { get; private set; }
        public bool CanReroll => RequiresCheck && !HasRerolled && !IsCommitted && actor != null && actor.Willpower > 0;

        internal PlayableEventChoiceTransaction(EventSystem eventSystem, EventData gameEvent, int optionIndex, HunterInstance actor, int rollValue, int bonus)
        {
            this.eventSystem = eventSystem;
            this.gameEvent = gameEvent;
            this.optionIndex = optionIndex;
            this.actor = actor;
            option = gameEvent.options[optionIndex];
            RollValue = rollValue;
            Bonus = bonus;
        }

        public bool TryReroll()
        {
            if (!CanReroll) return false;

            RerollResult result = eventSystem.TryReroll(actor, RollValue, 1, 10);
            if (!result.Success) return false;

            RollValue = result.FinalRoll;
            HasRerolled = true;
            return true;
        }

        public EventResolutionResult Commit()
        {
            if (IsCommitted) return committedResult;

            IsCommitted = true;
            committedResult = eventSystem.CommitPreparedChoice(gameEvent, optionIndex, actor, Success, RollValue);
            return committedResult;
        }

        public void Continue()
        {
            if (!IsCommitted || continued) return;
            continued = true;
            eventSystem.ContinuePreparedChoice();
        }
    }

    public partial class EventSystem
    {
        public PlayableEventChoiceTransaction PrepareChoice(EventData gameEvent, int optionIndex, HunterInstance actor = null)
        {
            if (gameEvent?.options == null || optionIndex < 0 || optionIndex >= gameEvent.options.Count) return null;

            EventOption option = gameEvent.options[optionIndex];
            actor ??= _selectedHunter;
            if (option.checkType != CheckType.None && actor == null) return null;
            if (!PlayableEventOptionAvailability.CanUse(option, actor, _settlement, out _)) return null;
            int rollValue = option.checkType == CheckType.None ? 0 : RollDice(1, 10);
            int bonus = GetCheckBonus(actor, option.checkType);
            return new PlayableEventChoiceTransaction(this, gameEvent, optionIndex, actor, rollValue, bonus);
        }

        internal EventResolutionResult CommitPreparedChoice(EventData gameEvent, int optionIndex, HunterInstance actor, bool success, int rollValue)
        {
            EventOption option = gameEvent.options[optionIndex];
            List<EventEffect> effects = success ? option.successEffects : option.failEffects;
            if (effects != null)
                foreach (EventEffect effect in effects)
                    ApplyEffect(effect, actor);

            MarkEventCompleted(gameEvent);
            EnqueueChain(success ? option.successChain : option.failChain);
            return new EventResolutionResult
            {
                Success = success,
                RollValue = rollValue,
                ResultText = success ? option.successText : option.failText
            };
        }

        internal void ContinuePreparedChoice() => ProcessNextInChain();
    }
}
