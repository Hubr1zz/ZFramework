using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Data;
using UnityEngine;

namespace HuntingInDarkness.Combat
{
    [Serializable]
    public sealed class PlayableWeaponMasteryMilestone
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, Min(1)] private int threshold = 2;
        [SerializeField] private string grantedTrait;

        public string Id => id;
        public string DisplayName => displayName;
        public int Threshold => threshold;
        public string GrantedTrait => grantedTrait;

        public WeaponMasteryMilestoneDefinition ToDomain()
        {
            return new WeaponMasteryMilestoneDefinition(id, displayName, threshold, grantedTrait);
        }
    }

    [Serializable]
    public sealed class PlayableWeaponMasteryFamily
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private List<string> weaponNames = new();
        [SerializeField] private List<PlayableWeaponMasteryMilestone> milestones = new();

        public string Id => id;
        public string DisplayName => displayName;
        public IReadOnlyList<string> WeaponNames => weaponNames;
        public IReadOnlyList<PlayableWeaponMasteryMilestone> Milestones => milestones;

        public WeaponMasteryFamilyDefinition ToDomain()
        {
            var definitions = new List<WeaponMasteryMilestoneDefinition>();
            foreach (PlayableWeaponMasteryMilestone milestone in milestones)
                definitions.Add(milestone.ToDomain());
            definitions.Sort((left, right) => left.Threshold.CompareTo(right.Threshold));
            return new WeaponMasteryFamilyDefinition(id, displayName, definitions);
        }
    }

    [CreateAssetMenu(fileName = "PlayableWeaponMasteryCatalog", menuName = "Hunting in Darkness/Weapon Mastery Catalog")]
    public sealed class PlayableWeaponMasteryCatalog : ScriptableObject
    {
        [SerializeField] private List<PlayableWeaponMasteryFamily> families = new();
        [Header("营地训练")]
        [SerializeField] private string trainingInventionName = "武器训练";
        [SerializeField] private ItemData trainingCostItem;
        [SerializeField, Min(0)] private int trainingCost = 1;
        [SerializeField, Min(1)] private int trainingExperience = 1;

        public bool IsConfigured => Validate();
        public string TrainingInventionName => trainingInventionName;
        public ItemData TrainingCostItem => trainingCostItem;
        public int TrainingCost => Mathf.Max(0, trainingCost);
        public int TrainingExperience => Mathf.Max(1, trainingExperience);

        public IReadOnlyList<WeaponMasteryFamilyDefinition> GetFamilies()
        {
            var definitions = new List<WeaponMasteryFamilyDefinition>();
            foreach (PlayableWeaponMasteryFamily family in families)
                if (family != null)
                    definitions.Add(family.ToDomain());
            return definitions;
        }

        public bool TryGetFamily(string masteryId, out WeaponMasteryFamilyDefinition family)
        {
            family = null;
            foreach (PlayableWeaponMasteryFamily candidate in families)
            {
                if (candidate == null || !string.Equals(candidate.Id, masteryId, StringComparison.Ordinal)) continue;
                family = candidate.ToDomain();
                return true;
            }
            return false;
        }

        public bool TryResolve(string weaponName, out WeaponMasteryFamilyDefinition family)
        {
            family = null;
            if (string.IsNullOrWhiteSpace(weaponName)) return false;
            foreach (PlayableWeaponMasteryFamily candidate in families)
            {
                if (!ContainsWeapon(candidate.WeaponNames, weaponName)) continue;
                family = candidate.ToDomain();
                return true;
            }
            return false;
        }

        private bool Validate()
        {
            if (families.Count == 0 || string.IsNullOrWhiteSpace(trainingInventionName) || trainingCostItem == null) return false;
            var familyIds = new HashSet<string>(StringComparer.Ordinal);
            var weaponNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlayableWeaponMasteryFamily family in families)
            {
                if (family == null || string.IsNullOrWhiteSpace(family.Id) || string.IsNullOrWhiteSpace(family.DisplayName) || family.WeaponNames.Count == 0 || !familyIds.Add(family.Id)) return false;
                foreach (string weaponName in family.WeaponNames)
                    if (string.IsNullOrWhiteSpace(weaponName) || !weaponNames.Add(weaponName)) return false;
                if (!ValidateMilestones(family.Milestones)) return false;
            }
            return true;
        }

        private static bool ValidateMilestones(IReadOnlyList<PlayableWeaponMasteryMilestone> milestones)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var thresholds = new HashSet<int>();
            foreach (PlayableWeaponMasteryMilestone milestone in milestones)
                if (milestone == null || string.IsNullOrWhiteSpace(milestone.Id) || string.IsNullOrWhiteSpace(milestone.DisplayName) || milestone.Threshold <= 0 || string.IsNullOrWhiteSpace(milestone.GrantedTrait) || !ids.Add(milestone.Id) || !thresholds.Add(milestone.Threshold)) return false;
            return true;
        }

        private static bool ContainsWeapon(IReadOnlyList<string> names, string weaponName)
        {
            foreach (string name in names)
                if (string.Equals(name, weaponName, StringComparison.Ordinal))
                    return true;
            return false;
        }
    }
}
