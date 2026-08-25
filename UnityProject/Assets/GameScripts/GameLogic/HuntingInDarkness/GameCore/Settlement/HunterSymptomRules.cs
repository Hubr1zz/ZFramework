using System;
using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Settlement
{
    public enum SymptomResolutionChoice
    {
        Internalize,
        Overcome
    }

    [Serializable]
    public sealed class HunterSymptomState
    {
        public string SymptomId;
        public int InternalizationProgress;
        public int LastReflectionYear;
        public bool IsInternalized;
        public bool IsOvercome;
        public int AppliedStrengthDelta;
        public int AppliedAccuracyDelta;
        public int AppliedEvasionDelta;
        public int AppliedMovementDelta;
    }

    public readonly struct SymptomStatModifiers
    {
        public int Strength { get; }
        public int Accuracy { get; }
        public int Evasion { get; }
        public int Movement { get; }

        public SymptomStatModifiers(int strength, int accuracy, int evasion, int movement)
        {
            Strength = strength;
            Accuracy = accuracy;
            Evasion = evasion;
            Movement = movement;
        }
    }

    public sealed class SymptomDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public SymptomStatModifiers NegativeModifiers { get; }
        public SymptomStatModifiers InternalizedBonus { get; }
        public int InternalizationThreshold { get; }
        public int ReflectionWillpowerCost { get; }
        public int OvercomeCourageRequirement { get; }
        public int OvercomeGrowthCost { get; }

        public SymptomDefinition(string id, string displayName, string description, SymptomStatModifiers negativeModifiers, SymptomStatModifiers internalizedBonus, int internalizationThreshold, int reflectionWillpowerCost, int overcomeCourageRequirement, int overcomeGrowthCost)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            NegativeModifiers = negativeModifiers;
            InternalizedBonus = internalizedBonus;
            InternalizationThreshold = Math.Max(1, internalizationThreshold);
            ReflectionWillpowerCost = Math.Max(0, reflectionWillpowerCost);
            OvercomeCourageRequirement = Math.Max(0, overcomeCourageRequirement);
            OvercomeGrowthCost = Math.Max(0, overcomeGrowthCost);
        }
    }

    public static class HunterSymptomRules
    {
        public static HunterSymptomState Register(HunterState hunter, SymptomDefinition definition)
        {
            if (!CanUse(hunter, definition)) return null;
            EnsureCollections(hunter);
            HunterSymptomState existing = Find(hunter, definition.Id);
            if (existing != null) return existing;

            var state = new HunterSymptomState { SymptomId = definition.Id, LastReflectionYear = -1 };
            state.AppliedStrengthDelta = ApplyStat(ref hunter.Stats.strength, definition.NegativeModifiers.Strength);
            state.AppliedAccuracyDelta = ApplyStat(ref hunter.Stats.accuracy, definition.NegativeModifiers.Accuracy);
            state.AppliedEvasionDelta = ApplyStat(ref hunter.Stats.evasion, definition.NegativeModifiers.Evasion);
            state.AppliedMovementDelta = ApplyStat(ref hunter.Stats.movement, definition.NegativeModifiers.Movement);
            hunter.SymptomStates.Add(state);
            AddUnique(hunter.Ailments, definition.DisplayName);
            return state;
        }

        public static bool TryInternalize(HunterState hunter, SymptomDefinition definition, int currentYear, out string reason)
        {
            HunterSymptomState state = Find(hunter, definition?.Id);
            if (!CanInternalize(hunter, definition, currentYear, out reason)) return false;

            if (!hunter.TrySpendWillpower(definition.ReflectionWillpowerCost))
            {
                reason = "意志不足。";
                return false;
            }
            state.LastReflectionYear = currentYear;
            state.InternalizationProgress++;
            if (state.InternalizationProgress >= definition.InternalizationThreshold)
            {
                state.InternalizationProgress = definition.InternalizationThreshold;
                state.IsInternalized = true;
                ApplyModifiers(hunter, definition.InternalizedBonus);
                AddUnique(hunter.Traits, GetInternalizedTraitName(definition));
            }
            reason = string.Empty;
            return true;
        }

        public static bool TryOvercome(HunterState hunter, SymptomDefinition definition, out string reason)
        {
            HunterSymptomState state = Find(hunter, definition?.Id);
            if (!CanOvercome(hunter, definition, out reason)) return false;

            hunter.UnspentGrowth -= definition.OvercomeGrowthCost;
            ReverseAppliedModifiers(hunter, state);
            state.IsOvercome = true;
            hunter.Ailments.RemoveAll(value => value == definition.DisplayName);
            AddUnique(hunter.Traits, GetOvercomeTraitName(definition));
            reason = string.Empty;
            return true;
        }

        public static bool CanInternalize(HunterState hunter, SymptomDefinition definition, int currentYear, out string reason)
        {
            HunterSymptomState state = Find(hunter, definition?.Id);
            if (!CanProgress(hunter, definition, state, out reason)) return false;
            if (state.IsInternalized)
            {
                reason = "这一症状已经被内化。";
                return false;
            }
            if (state.LastReflectionYear == currentYear)
            {
                reason = "本年已经面对过这一症状。";
                return false;
            }
            if (hunter.Willpower < definition.ReflectionWillpowerCost)
            {
                reason = "意志不足。";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public static bool CanOvercome(HunterState hunter, SymptomDefinition definition, out string reason)
        {
            HunterSymptomState state = Find(hunter, definition?.Id);
            if (!CanProgress(hunter, definition, state, out reason)) return false;
            if (hunter.Courage < definition.OvercomeCourageRequirement)
            {
                reason = $"需要 {definition.OvercomeCourageRequirement} 点勇气。";
                return false;
            }
            if (hunter.UnspentGrowth < definition.OvercomeGrowthCost)
            {
                reason = $"需要 {definition.OvercomeGrowthCost} 点成长。";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public static HunterSymptomState Find(HunterState hunter, string symptomId)
        {
            if (hunter?.SymptomStates == null || string.IsNullOrWhiteSpace(symptomId)) return null;
            return hunter.SymptomStates.Find(state => state != null && string.Equals(state.SymptomId, symptomId, StringComparison.Ordinal));
        }

        public static string GetInternalizedTraitName(SymptomDefinition definition) => $"内化·{definition.DisplayName}";
        public static string GetOvercomeTraitName(SymptomDefinition definition) => $"已克服·{definition.DisplayName}";

        private static bool CanUse(HunterState hunter, SymptomDefinition definition)
        {
            return hunter != null && hunter.Stats != null && definition != null && !string.IsNullOrWhiteSpace(definition.Id) && !string.IsNullOrWhiteSpace(definition.DisplayName);
        }

        private static bool CanProgress(HunterState hunter, SymptomDefinition definition, HunterSymptomState state, out string reason)
        {
            if (!CanUse(hunter, definition) || state == null)
            {
                reason = "猎人没有这一症状。";
                return false;
            }
            if (!hunter.IsAvailable)
            {
                reason = "只有营地中的存活猎人可以面对症状。";
                return false;
            }
            if (state.IsOvercome)
            {
                reason = "这一症状已经被克服。";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private static void EnsureCollections(HunterState hunter)
        {
            hunter.SymptomStates ??= new List<HunterSymptomState>();
            hunter.Ailments ??= new List<string>();
            hunter.Traits ??= new List<string>();
        }

        private static int ApplyStat(ref int value, int delta)
        {
            int previous = value;
            long changed = (long)value + delta;
            value = (int)Math.Max(0L, Math.Min(int.MaxValue, changed));
            return value - previous;
        }

        private static void ApplyModifiers(HunterState hunter, SymptomStatModifiers modifiers)
        {
            ApplyStat(ref hunter.Stats.strength, modifiers.Strength);
            ApplyStat(ref hunter.Stats.accuracy, modifiers.Accuracy);
            ApplyStat(ref hunter.Stats.evasion, modifiers.Evasion);
            ApplyStat(ref hunter.Stats.movement, modifiers.Movement);
        }

        private static void ReverseAppliedModifiers(HunterState hunter, HunterSymptomState state)
        {
            ApplyStat(ref hunter.Stats.strength, -state.AppliedStrengthDelta);
            ApplyStat(ref hunter.Stats.accuracy, -state.AppliedAccuracyDelta);
            ApplyStat(ref hunter.Stats.evasion, -state.AppliedEvasionDelta);
            ApplyStat(ref hunter.Stats.movement, -state.AppliedMovementDelta);
            state.AppliedStrengthDelta = 0;
            state.AppliedAccuracyDelta = 0;
            state.AppliedEvasionDelta = 0;
            state.AppliedMovementDelta = 0;
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!values.Contains(value)) values.Add(value);
        }
    }
}
