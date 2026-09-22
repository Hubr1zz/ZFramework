using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;

namespace HuntingInDarkness.ActionFlow.Events
{
    public enum PlayableEventEffectStatus
    {
        Applied,
        Failed
    }

    public readonly struct PlayableEventEffectResult
    {
        public PlayableEventEffectResult(int effectIndex, EventEffect effect, PlayableEventEffectStatus status, string reason, string eventId = "", string resolvedTargetId = "", int targetActorId = 0, bool stateChanged = false, int previousValue = 0, int currentValue = 0, DeathCardType? deathCard = null, string permanentInjuryId = "", bool hunterDied = false, string deathDeckId = "", int facedownPosition = -1)
        {
            EventId = eventId ?? string.Empty;
            EffectIndex = effectIndex;
            EffectType = effect?.effectType;
            TargetName = effect?.targetName ?? string.Empty;
            Status = status;
            Reason = reason ?? string.Empty;
            ResolvedTargetId = resolvedTargetId ?? string.Empty;
            TargetActorId = targetActorId;
            StateChanged = stateChanged;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
            DeathCard = deathCard;
            PermanentInjuryId = permanentInjuryId ?? string.Empty;
            HunterDied = hunterDied;
            DeathDeckId = deathDeckId ?? string.Empty;
            FacedownPosition = facedownPosition;
            SurvivalEvent = effect?.SurvivalEvent;
        }

        public string EventId { get; }
        public int EffectIndex { get; }
        public EventEffectType? EffectType { get; }
        public string TargetName { get; }
        public PlayableEventEffectStatus Status { get; }
        public string Reason { get; }
        public string ResolvedTargetId { get; }
        public int TargetActorId { get; }
        public bool StateChanged { get; }
        public int PreviousValue { get; }
        public int CurrentValue { get; }
        public DeathCardType? DeathCard { get; }
        public string PermanentInjuryId { get; }
        public bool HunterDied { get; }
        public string DeathDeckId { get; }
        public int FacedownPosition { get; }
        public EventData SurvivalEvent { get; }
        public bool Succeeded => Status == PlayableEventEffectStatus.Applied;
    }

    public readonly struct PlayableEventEffectBatchResult
    {
        private readonly IReadOnlyList<PlayableEventEffectResult> effects;

        public PlayableEventEffectBatchResult(IReadOnlyList<PlayableEventEffectResult> effects)
        {
            var snapshot = new PlayableEventEffectResult[effects?.Count ?? 0];
            for (int index = 0; index < snapshot.Length; index++)
                snapshot[index] = effects[index];
            this.effects = Array.AsReadOnly(snapshot);
            AppliedCount = 0;
            FailedCount = 0;
            foreach (PlayableEventEffectResult effect in this.effects)
            {
                if (effect.Succeeded) AppliedCount++;
                else FailedCount++;
            }
        }

        public static PlayableEventEffectBatchResult Empty => new(Array.Empty<PlayableEventEffectResult>());
        public IReadOnlyList<PlayableEventEffectResult> Effects => effects ?? Array.Empty<PlayableEventEffectResult>();
        public int Count => AppliedCount + FailedCount;
        public int AppliedCount { get; }
        public int FailedCount { get; }
        public bool HasFailures => FailedCount > 0;
        public bool Succeeded => FailedCount == 0;
    }

    public struct HunterSymptomAcquiredEvent
    {
        public string SourceEventId;
        public int EffectIndex;
        public int HunterId;
        public string SymptomId;
        public string SymptomName;
    }

    public struct HunterWoundedEvent
    {
        public string SourceEventId;
        public int EffectIndex;
        public int HunterId;
        public string BodyPartId;
        public int PreviousHealth;
        public int CurrentHealth;
    }
}
