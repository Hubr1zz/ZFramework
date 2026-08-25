using System.Collections.Generic;
using HuntingInDarkness.ActionFlow.Events;
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
        private readonly IPlayableEventResourceCommand resourceCommand;
        private EventResolutionResult committedResult;
        private IReadOnlyList<EventData> standaloneChain;
        private IReadOnlyList<string> standaloneEncounterIds;
        private PlayableEventEffectBatchResult standaloneEffectResults;
        private bool continued;

        public EventData GameEvent => gameEvent;
        public EventOption Option => option;
        public HunterInstance Actor => actor;
        public bool RequiresCheck => option.checkType != CheckType.None;
        public int RollValue { get; private set; }
        public int Bonus { get; }
        public int Total => PlayableEventCheckRules.ResolveTotal(option, RollValue, Bonus);
        public int Target => option.checkTarget;
        public bool Success => !RequiresCheck || PlayableEventCheckRules.IsSuccessful(option, RollValue, Bonus);
        public bool HasRerolled { get; private set; }
        public bool IsCommitted { get; private set; }
        public bool CanReroll => RequiresCheck && !HasRerolled && !IsCommitted && actor != null && actor.Willpower > 0;

        internal PlayableEventChoiceTransaction(EventSystem eventSystem, EventData gameEvent, int optionIndex, HunterInstance actor, int rollValue, int bonus, IPlayableEventResourceCommand resourceCommand)
        {
            this.eventSystem = eventSystem;
            this.gameEvent = gameEvent;
            this.optionIndex = optionIndex;
            this.actor = actor;
            this.resourceCommand = resourceCommand;
            option = gameEvent.options[optionIndex];
            RollValue = rollValue;
            Bonus = bonus;
        }

        public bool TryReroll(int? preparedRoll = null)
        {
            if (!CanReroll) return false;

            int count = PlayableEventCheckRules.ResolveCount(option);
            int sides = PlayableEventCheckRules.ResolveSides(option);
            RerollResult result = preparedRoll.HasValue ? eventSystem.TryReroll(actor, RollValue, preparedRoll.Value, count, count * sides) : eventSystem.TryReroll(actor, RollValue, count, sides);
            if (!result.Success) return false;

            RollValue = result.FinalRoll;
            HasRerolled = true;
            return true;
        }

        public EventResolutionResult Commit()
        {
            if (IsCommitted) return committedResult;

            IsCommitted = true;
            committedResult = eventSystem.CommitPreparedChoice(gameEvent, optionIndex, actor, Success, RollValue, resourceCommand);
            return committedResult;
        }

        /// <summary>提交单个节点但不推进共享事件队列，供 ActionQueue 自己维护事件子树。</summary>
        public PlayableEventCommitResult CommitStandalone(bool captureEncounterRequests = false)
        {
            if (!IsCommitted)
            {
                IsCommitted = true;
                PlayableEventCommitResult result = eventSystem.CommitPreparedChoiceStandalone(gameEvent, optionIndex, actor, Success, RollValue, resourceCommand, captureEncounterRequests);
                committedResult = result.Result;
                standaloneChain = result.ChainedEvents;
                standaloneEncounterIds = result.EncounterIds;
                standaloneEffectResults = result.EffectResults;
            }
            return new PlayableEventCommitResult(committedResult, standaloneChain, standaloneEncounterIds, standaloneEffectResults);
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
        public PlayableEventChoiceTransaction PrepareChoice(EventData gameEvent, int optionIndex, HunterInstance actor = null, int? preparedRoll = null, IPlayableEventResourceCommand resourceCommand = null)
        {
            if (gameEvent?.options == null || optionIndex < 0 || optionIndex >= gameEvent.options.Count) return null;
            EventOption option = gameEvent.options[optionIndex];
            if (preparedRoll.HasValue && !PlayableEventCheckRules.IsValidRoll(option, preparedRoll.Value)) return null;
            actor ??= _selectedHunter;
            bool requiresHunter = option.checkType != CheckType.None || PlayableEventOptionAvailability.RequiresHunter(option);
            if (requiresHunter && (actor == null || !ReferenceEquals(_settlement.GetHunter(actor.InstanceId), actor))) return null;
            if (PlayableEventOptionAvailability.HasHunterDeathEffect(option) && hunterDeathCommand == null) return null;
            if (!PlayableEventOptionAvailability.CanUse(option, actor, _settlement, out _)) return null;
            int rollValue = option.checkType == CheckType.None ? 0 : preparedRoll ?? RollDice(PlayableEventCheckRules.ResolveCount(option), PlayableEventCheckRules.ResolveSides(option));
            int bonus = GetCheckBonus(actor, option.checkType);
            return new PlayableEventChoiceTransaction(this, gameEvent, optionIndex, actor, rollValue, bonus, resourceCommand);
        }

        internal EventResolutionResult CommitPreparedChoice(EventData gameEvent, int optionIndex, HunterInstance actor, bool success, int rollValue, IPlayableEventResourceCommand resourceCommand)
        {
            PlayableEventCommitResult result = CommitPreparedChoiceStandalone(gameEvent, optionIndex, actor, success, rollValue, resourceCommand);
            MarkEventCompleted(gameEvent);
            if (result.EncounterIds.Count > 0)
                _pendingChain.Clear();
            else
                EnqueueChain(result.ChainedEvents);
            return result.Result;
        }

        internal PlayableEventCommitResult CommitPreparedChoiceStandalone(EventData gameEvent, int optionIndex, HunterInstance actor, bool success, int rollValue, IPlayableEventResourceCommand resourceCommand, bool captureEncounterRequests = false)
        {
            EventOption option = gameEvent.options[optionIndex];
            List<EventEffect> effects = success ? option.successEffects : option.failEffects;
            var encounterIds = new List<string>();
            var effectResults = new List<PlayableEventEffectResult>();
            if (gameEvent.eventType == GameEventType.Combat && !string.IsNullOrWhiteSpace(gameEvent.combatEncounterId))
                RecordEncounter(gameEvent.combatEncounterId, encounterIds);
            if (effects != null)
                for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                    effectResults.Add(ApplyEffect(effects[effectIndex], actor, actor, encounterIds, resourceCommand, effectIndex, gameEvent.ContentId));
            if (gameEvent.eventType == GameEventType.Combat && encounterIds.Count == 0)
                RecordEncounter(gameEvent.combatEncounterId, encounterIds);
            bool campaignEnded = _settlement.GetAliveHunters().Count == 0;
            if (campaignEnded)
                encounterIds.Clear();
            if (!captureEncounterRequests && !campaignEnded)
                PublishEncounters(encounterIds, gameEvent.name);
            var result = new EventResolutionResult
            {
                Success = success,
                RollValue = rollValue,
                ResultText = success ? option.successText : option.failText,
                EffectResults = new PlayableEventEffectBatchResult(effectResults)
            };
            IReadOnlyList<EventData> chain = System.Array.Empty<EventData>();
            if (!campaignEnded)
                chain = success ? option.successChain : option.failChain;
            return new PlayableEventCommitResult(result, chain, encounterIds, result.EffectResults);
        }

        internal void ContinuePreparedChoice() => ProcessNextInChain();
    }

    internal static class PlayableEventCheckRules
    {
        public static int ResolveCount(EventOption option) => option?.checkCount > 0 ? option.checkCount : 1;

        public static int ResolveSides(EventOption option) => option?.checkSides > 1 ? option.checkSides : 10;

        public static bool IsValidRoll(EventOption option, int roll)
        {
            int count = ResolveCount(option);
            int sides = ResolveSides(option);
            return roll >= count && roll <= count * sides;
        }

        public static int ResolveTotal(EventOption option, int roll, int bonus)
        {
            return option?.checkPresentation == EventCheckPresentationKind.OldMaid ? roll : roll + bonus;
        }

        public static bool IsSuccessful(EventOption option, int roll, int bonus)
        {
            if (option?.checkPresentation == EventCheckPresentationKind.OldMaid) return roll > 1;
            return EventRules.CheckSucceeded(roll, bonus, option?.checkTarget ?? 0);
        }
    }

    public readonly struct PlayableEventCommitResult
    {
        public PlayableEventCommitResult(EventResolutionResult result, IReadOnlyList<EventData> chainedEvents)
            : this(result, chainedEvents, System.Array.Empty<string>(), result.EffectResults)
        {
        }

        public PlayableEventCommitResult(EventResolutionResult result, IReadOnlyList<EventData> chainedEvents, IReadOnlyList<string> encounterIds)
            : this(result, chainedEvents, encounterIds, result.EffectResults)
        {
        }

        public PlayableEventCommitResult(EventResolutionResult result, IReadOnlyList<EventData> chainedEvents, IReadOnlyList<string> encounterIds, PlayableEventEffectBatchResult effectResults)
        {
            Result = result;
            ChainedEvents = chainedEvents ?? System.Array.Empty<EventData>();
            EncounterIds = encounterIds ?? System.Array.Empty<string>();
            EffectResults = effectResults;
        }

        public EventResolutionResult Result { get; }
        public IReadOnlyList<EventData> ChainedEvents { get; }
        public IReadOnlyList<string> EncounterIds { get; }
        public PlayableEventEffectBatchResult EffectResults { get; }
    }

    public readonly struct PlayableEventNodeCommitResult
    {
        public PlayableEventNodeCommitResult(IReadOnlyList<EventData> chainedEvents, IReadOnlyList<string> encounterIds)
            : this(chainedEvents, encounterIds, PlayableEventEffectBatchResult.Empty)
        {
        }

        public PlayableEventNodeCommitResult(IReadOnlyList<EventData> chainedEvents, IReadOnlyList<string> encounterIds, PlayableEventEffectBatchResult effectResults)
        {
            ChainedEvents = chainedEvents ?? System.Array.Empty<EventData>();
            EncounterIds = encounterIds ?? System.Array.Empty<string>();
            EffectResults = effectResults;
        }

        public IReadOnlyList<EventData> ChainedEvents { get; }
        public IReadOnlyList<string> EncounterIds { get; }
        public PlayableEventEffectBatchResult EffectResults { get; }
    }

    public struct PlayableEventEncounterRequestedEvent
    {
        public string EncounterId;
        public string SourceEventId;
    }
}
