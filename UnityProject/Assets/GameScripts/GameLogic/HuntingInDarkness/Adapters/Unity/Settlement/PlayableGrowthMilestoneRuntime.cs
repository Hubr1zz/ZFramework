using System.Collections.Generic;
using Core;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    public static class PlayableGrowthMilestoneRuntime
    {
        private static PlayableGrowthMilestoneCatalog catalog;

        public static PlayableGrowthMilestoneCatalog Catalog => catalog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            catalog = null;
        }

        public static void Configure(PlayableGrowthMilestoneCatalog milestoneCatalog)
        {
            catalog = milestoneCatalog != null && milestoneCatalog.IsConfigured ? milestoneCatalog : null;
        }

        public static List<HunterGrowthMilestoneOutcome> Synchronize(SettlementInstance settlement)
        {
            var outcomes = new List<HunterGrowthMilestoneOutcome>();
            if (settlement?.Hunters == null) return outcomes;
            foreach (HunterInstance hunter in settlement.Hunters)
                outcomes.AddRange(SynchronizeHunter(hunter));
            return outcomes;
        }

        public static List<HunterGrowthMilestoneOutcome> SynchronizeHunter(HunterInstance hunter)
        {
            var outcomes = new List<HunterGrowthMilestoneOutcome>();
            if (hunter == null || catalog == null) return outcomes;
            foreach (HunterGrowthMilestoneDefinition definition in catalog.GetDefinitions())
            {
                if (!HunterGrowthMilestoneRules.TryClaim(hunter, definition, out HunterGrowthMilestoneOutcome outcome)) continue;
                outcomes.Add(outcome);
                EventBus.Publish(new HunterGrowthMilestoneReachedEvent(hunter.InstanceId, hunter.Name, outcome));
            }
            return outcomes;
        }
    }

    public readonly struct HunterGrowthMilestoneReachedEvent
    {
        public int HunterId { get; }
        public string HunterName { get; }
        public string MilestoneId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public HunterGrowthChoice Attribute { get; }
        public int Threshold { get; }
        public string GrantedTrait { get; }

        public HunterGrowthMilestoneReachedEvent(int hunterId, string hunterName, HunterGrowthMilestoneOutcome outcome)
        {
            HunterId = hunterId;
            HunterName = hunterName ?? string.Empty;
            MilestoneId = outcome.Id;
            DisplayName = outcome.DisplayName;
            Description = outcome.Description;
            Attribute = outcome.Attribute;
            Threshold = outcome.Threshold;
            GrantedTrait = outcome.GrantedTrait;
        }
    }
}
