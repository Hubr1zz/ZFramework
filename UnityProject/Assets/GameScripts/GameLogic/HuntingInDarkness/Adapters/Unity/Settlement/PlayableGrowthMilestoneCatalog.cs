using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    [Serializable]
    public sealed class PlayableGrowthMilestoneDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private HunterGrowthChoice attribute;
        [SerializeField, Range(1, HunterAdvancementRules.MaximumGrowthAttribute)] private int threshold = 2;
        [SerializeField] private string grantedTrait;
        [SerializeField, Min(0)] private int willpowerCapacityBonus;
        [SerializeField] private int strengthBonus;
        [SerializeField] private int accuracyBonus;
        [SerializeField] private int evasionBonus;
        [SerializeField] private int movementBonus;

        public string Id => id;
        public string DisplayName => displayName;
        public HunterGrowthChoice Attribute => attribute;
        public int Threshold => threshold;
        public bool HasReward => !string.IsNullOrWhiteSpace(grantedTrait) || willpowerCapacityBonus > 0 || strengthBonus != 0 || accuracyBonus != 0 || evasionBonus != 0 || movementBonus != 0;

        public HunterGrowthMilestoneDefinition ToDomain()
        {
            return new HunterGrowthMilestoneDefinition(id, displayName, description, attribute, threshold, grantedTrait, willpowerCapacityBonus, new GrowthMilestoneStatModifiers(strengthBonus, accuracyBonus, evasionBonus, movementBonus));
        }
    }

    [CreateAssetMenu(fileName = "PlayableGrowthMilestoneCatalog", menuName = "Hunting in Darkness/Growth Milestone Catalog")]
    public sealed class PlayableGrowthMilestoneCatalog : ScriptableObject
    {
        [SerializeField] private List<PlayableGrowthMilestoneDefinition> milestones = new();

        public bool IsConfigured => Validate();

        public IReadOnlyList<HunterGrowthMilestoneDefinition> GetDefinitions()
        {
            var definitions = new List<HunterGrowthMilestoneDefinition>();
            foreach (PlayableGrowthMilestoneDefinition milestone in milestones)
                if (IsValid(milestone))
                    definitions.Add(milestone.ToDomain());
            definitions.Sort(CompareDefinitions);
            return definitions;
        }

        private bool Validate()
        {
            if (milestones.Count == 0) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var thresholds = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlayableGrowthMilestoneDefinition milestone in milestones)
            {
                if (!IsValid(milestone) || !ids.Add(milestone.Id)) return false;
                if (!thresholds.Add($"{milestone.Attribute}:{milestone.Threshold}")) return false;
            }
            return true;
        }

        private static bool IsValid(PlayableGrowthMilestoneDefinition milestone)
        {
            return milestone != null && !string.IsNullOrWhiteSpace(milestone.Id) && !string.IsNullOrWhiteSpace(milestone.DisplayName) && milestone.HasReward && (milestone.Attribute == HunterGrowthChoice.Courage || milestone.Attribute == HunterGrowthChoice.Understanding) && milestone.Threshold >= 1 && milestone.Threshold <= HunterAdvancementRules.MaximumGrowthAttribute;
        }

        private static int CompareDefinitions(HunterGrowthMilestoneDefinition left, HunterGrowthMilestoneDefinition right)
        {
            int attributeComparison = left.Attribute.CompareTo(right.Attribute);
            return attributeComparison != 0 ? attributeComparison : left.Threshold.CompareTo(right.Threshold);
        }
    }
}
