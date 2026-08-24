using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;

namespace HuntingInDarkness.ActionFlow.Events
{
    public enum PlayableEventEffectStatus
    {
        Applied,
        Failed
    }

    public readonly struct PlayableEventEffectResult
    {
        public PlayableEventEffectResult(int effectIndex, EventEffect effect, PlayableEventEffectStatus status, string reason, string eventId = "")
        {
            EventId = eventId ?? string.Empty;
            EffectIndex = effectIndex;
            EffectType = effect?.effectType;
            TargetName = effect?.targetName ?? string.Empty;
            Status = status;
            Reason = reason ?? string.Empty;
        }

        public string EventId { get; }
        public int EffectIndex { get; }
        public EventEffectType? EffectType { get; }
        public string TargetName { get; }
        public PlayableEventEffectStatus Status { get; }
        public string Reason { get; }
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
}
