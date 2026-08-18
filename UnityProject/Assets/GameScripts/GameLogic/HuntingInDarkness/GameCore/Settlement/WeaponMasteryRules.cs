using System;
using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Settlement
{
    public sealed class WeaponMasteryMilestoneDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public int Threshold { get; }
        public string GrantedTrait { get; }

        public WeaponMasteryMilestoneDefinition(string id, string displayName, int threshold, string grantedTrait)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Threshold = threshold;
            GrantedTrait = grantedTrait ?? string.Empty;
        }
    }

    public sealed class WeaponMasteryFamilyDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<WeaponMasteryMilestoneDefinition> Milestones { get; }

        public WeaponMasteryFamilyDefinition(string id, string displayName, IReadOnlyList<WeaponMasteryMilestoneDefinition> milestones)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Milestones = milestones ?? Array.Empty<WeaponMasteryMilestoneDefinition>();
        }
    }

    public readonly struct WeaponMasteryGainOutcome
    {
        public string MasteryId { get; }
        public string MasteryName { get; }
        public int OldValue { get; }
        public int NewValue { get; }
        public IReadOnlyList<string> ReachedMilestoneNames { get; }

        public WeaponMasteryGainOutcome(string masteryId, string masteryName, int oldValue, int newValue, IReadOnlyList<string> reachedMilestoneNames)
        {
            MasteryId = masteryId ?? string.Empty;
            MasteryName = masteryName ?? string.Empty;
            OldValue = oldValue;
            NewValue = newValue;
            ReachedMilestoneNames = reachedMilestoneNames ?? Array.Empty<string>();
        }
    }

    /// <summary>武器熟练度的纯规则。具体装备与战斗事件由 Unity Adapter 转换。</summary>
    public static class WeaponMasteryRules
    {
        public const int ExperiencePerBattle = 1;

        public static bool CanGain(bool hunterIsAlive, string primaryWeaponName, IReadOnlyCollection<string> effectiveWeapons)
        {
            if (!hunterIsAlive || string.IsNullOrWhiteSpace(primaryWeaponName) || effectiveWeapons == null)
                return false;

            foreach (string weaponName in effectiveWeapons)
                if (string.Equals(primaryWeaponName, weaponName, StringComparison.Ordinal))
                    return true;

            return false;
        }

        public static bool TryGain(HunterState hunter, WeaponMasteryFamilyDefinition family, int amount, out WeaponMasteryGainOutcome outcome)
        {
            outcome = default;
            if (hunter == null || !hunter.IsAlive || hunter.HP == null || hunter.HP.head <= 0 || hunter.HP.body <= 0 || family == null || string.IsNullOrWhiteSpace(family.Id) || amount <= 0) return false;
            if (!CanIncrease(hunter, family.Id)) return false;

            hunter.WeaponMasteries ??= new List<WeaponMasteryState>();
            WeaponMasteryState mastery = FindMastery(hunter.WeaponMasteries, family.Id);
            if (mastery == null)
            {
                int migratedExperience = hunter.WeaponMasteries.Count == 0 ? Math.Max(0, hunter.WeaponProficiency) : 0;
                mastery = new WeaponMasteryState
                {
                    MasteryId = family.Id,
                    DisplayName = family.DisplayName,
                    Experience = migratedExperience
                };
                hunter.WeaponMasteries.Add(mastery);
            }

            mastery.ClaimedMilestoneIds ??= new List<string>();
            mastery.DisplayName = family.DisplayName;
            int oldValue = Math.Max(0, mastery.Experience);
            mastery.Experience = (int)Math.Min(int.MaxValue, (long)oldValue + amount);
            var reachedMilestones = new List<string>();
            ClaimReachedMilestones(hunter, mastery, family.Milestones, reachedMilestones);
            hunter.WeaponProficiency = GetMaximumExperience(hunter.WeaponMasteries);
            outcome = new WeaponMasteryGainOutcome(family.Id, family.DisplayName, oldValue, mastery.Experience, reachedMilestones);
            return mastery.Experience > oldValue;
        }

        public static bool CanIncrease(HunterState hunter, string masteryId)
        {
            if (hunter == null || string.IsNullOrWhiteSpace(masteryId)) return false;
            if (hunter.WeaponMasteries == null || hunter.WeaponMasteries.Count == 0) return hunter.WeaponProficiency < int.MaxValue;
            WeaponMasteryState mastery = FindMastery(hunter.WeaponMasteries, masteryId);
            if (mastery != null) return mastery.Experience < int.MaxValue;
            return true;
        }

        private static WeaponMasteryState FindMastery(IReadOnlyList<WeaponMasteryState> masteries, string masteryId)
        {
            foreach (WeaponMasteryState mastery in masteries)
                if (mastery != null && string.Equals(mastery.MasteryId, masteryId, StringComparison.Ordinal))
                    return mastery;
            return null;
        }

        private static void ClaimReachedMilestones(HunterState hunter, WeaponMasteryState mastery, IReadOnlyList<WeaponMasteryMilestoneDefinition> milestones, ICollection<string> reachedNames)
        {
            if (milestones == null) return;
            foreach (WeaponMasteryMilestoneDefinition milestone in milestones)
            {
                if (milestone == null || string.IsNullOrWhiteSpace(milestone.Id) || milestone.Threshold <= 0 || mastery.Experience < milestone.Threshold || mastery.ClaimedMilestoneIds.Contains(milestone.Id)) continue;
                mastery.ClaimedMilestoneIds.Add(milestone.Id);
                if (!string.IsNullOrWhiteSpace(milestone.DisplayName))
                    reachedNames.Add(milestone.DisplayName);
                hunter.Traits ??= new List<string>();
                if (!string.IsNullOrWhiteSpace(milestone.GrantedTrait) && !hunter.Traits.Contains(milestone.GrantedTrait))
                    hunter.Traits.Add(milestone.GrantedTrait);
            }
        }

        private static int GetMaximumExperience(IReadOnlyList<WeaponMasteryState> masteries)
        {
            int maximum = 0;
            foreach (WeaponMasteryState mastery in masteries)
                if (mastery != null)
                    maximum = Math.Max(maximum, mastery.Experience);
            return maximum;
        }
    }
}
