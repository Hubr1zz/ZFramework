using System;
using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Settlement
{
    public readonly struct GrowthMilestoneStatModifiers
    {
        public int Strength { get; }
        public int Accuracy { get; }
        public int Evasion { get; }
        public int Movement { get; }

        public GrowthMilestoneStatModifiers(int strength, int accuracy, int evasion, int movement)
        {
            Strength = strength;
            Accuracy = accuracy;
            Evasion = evasion;
            Movement = movement;
        }
    }

    public sealed class HunterGrowthMilestoneDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public HunterGrowthChoice Attribute { get; }
        public int Threshold { get; }
        public string GrantedTrait { get; }
        public int WillpowerCapacityBonus { get; }
        public GrowthMilestoneStatModifiers StatModifiers { get; }

        public HunterGrowthMilestoneDefinition(string id, string displayName, string description, HunterGrowthChoice attribute, int threshold, string grantedTrait, int willpowerCapacityBonus, GrowthMilestoneStatModifiers statModifiers)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            Attribute = attribute;
            Threshold = Math.Max(1, threshold);
            GrantedTrait = grantedTrait ?? string.Empty;
            WillpowerCapacityBonus = Math.Max(0, willpowerCapacityBonus);
            StatModifiers = statModifiers;
        }
    }

    public readonly struct HunterGrowthMilestoneOutcome
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public HunterGrowthChoice Attribute { get; }
        public int Threshold { get; }
        public string GrantedTrait { get; }

        public HunterGrowthMilestoneOutcome(HunterGrowthMilestoneDefinition definition)
        {
            Id = definition.Id;
            DisplayName = definition.DisplayName;
            Description = definition.Description;
            Attribute = definition.Attribute;
            Threshold = definition.Threshold;
            GrantedTrait = definition.GrantedTrait;
        }
    }

    public static class HunterGrowthMilestoneRules
    {
        public static bool TryClaim(HunterState hunter, HunterGrowthMilestoneDefinition definition, out HunterGrowthMilestoneOutcome outcome)
        {
            outcome = default;
            if (!CanClaim(hunter, definition)) return false;

            hunter.ClaimedGrowthMilestoneIds ??= new List<string>();
            hunter.Traits ??= new List<string>();
            hunter.Stats ??= new HunterStats();
            hunter.ClaimedGrowthMilestoneIds.Add(definition.Id);
            if (!string.IsNullOrWhiteSpace(definition.GrantedTrait) && !hunter.Traits.Contains(definition.GrantedTrait))
                hunter.Traits.Add(definition.GrantedTrait);
            IncreaseWillpowerCapacity(hunter, definition.WillpowerCapacityBonus);
            ApplyStat(ref hunter.Stats.strength, definition.StatModifiers.Strength);
            ApplyStat(ref hunter.Stats.accuracy, definition.StatModifiers.Accuracy);
            ApplyStat(ref hunter.Stats.evasion, definition.StatModifiers.Evasion);
            ApplyStat(ref hunter.Stats.movement, definition.StatModifiers.Movement);
            outcome = new HunterGrowthMilestoneOutcome(definition);
            return true;
        }

        private static bool CanClaim(HunterState hunter, HunterGrowthMilestoneDefinition definition)
        {
            if (hunter == null || hunter.IsDead || definition == null || string.IsNullOrWhiteSpace(definition.Id)) return false;
            if (definition.Attribute != HunterGrowthChoice.Courage && definition.Attribute != HunterGrowthChoice.Understanding) return false;
            if (hunter.ClaimedGrowthMilestoneIds != null && hunter.ClaimedGrowthMilestoneIds.Contains(definition.Id)) return false;
            int value = definition.Attribute == HunterGrowthChoice.Courage ? hunter.Courage : hunter.Understanding;
            return value >= definition.Threshold;
        }

        private static void IncreaseWillpowerCapacity(HunterState hunter, int bonus)
        {
            if (bonus <= 0) return;
            int previousMaximum = Math.Max(0, hunter.WillpowerMax);
            int current = Math.Max(0, hunter.Willpower);
            hunter.WillpowerMax = ClampToInt((long)previousMaximum + bonus);
            hunter.Willpower = Math.Min(hunter.WillpowerMax, ClampToInt((long)current + bonus));
        }

        private static void ApplyStat(ref int value, int delta)
        {
            value = ClampToInt(Math.Max(0L, (long)value + delta));
        }

        private static int ClampToInt(long value) => (int)Math.Max(0L, Math.Min(int.MaxValue, value));
    }
}
