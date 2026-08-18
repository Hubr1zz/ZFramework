using System;
using System.Collections.Generic;
using Core;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    public static class PlayableHunterAdvancementAdapter
    {
        public static List<HunterAdvancementOutcome> ApplyAfterHunt(IEnumerable<HunterInstance> hunters, HunterManagementSystem management)
        {
            if (management == null)
                throw new ArgumentNullException(nameof(management));

            var outcomes = new List<HunterAdvancementOutcome>();
            if (hunters == null) return outcomes;

            var processedIds = new HashSet<int>();
            foreach (HunterInstance hunter in hunters)
            {
                if (hunter == null || !processedIds.Add(hunter.InstanceId)) continue;
                HunterAdvancementOutcome outcome = HunterAdvancementRules.AdvanceAfterHunt(hunter);
                if (outcome.Retired)
                {
                    outcomes.Add(outcome);
                    management.CompleteRetirement(hunter);
                    Debug.Log($"[HunterAdvancement] {hunter.Name} 在年龄 {hunter.Age} 退休");
                    EventBus.Publish(new HunterRetiredEvent { HunterId = hunter.InstanceId, Age = hunter.Age });
                    continue;
                }
                if (!outcome.Advanced) continue;

                outcomes.Add(outcome);
                Debug.Log($"[HunterAdvancement] {hunter.Name} 年龄 {outcome.PreviousAge} → {outcome.CurrentAge}，获得 1 点待分配成长");
                EventBus.Publish(new HunterAdvancedEvent { HunterId = hunter.InstanceId, Age = hunter.Age, ReachedMilestone = outcome.ReachedMilestone });
            }
            return outcomes;
        }

        public static bool TrySpendGrowth(HunterInstance hunter, HunterGrowthChoice choice)
        {
            if (!HunterAdvancementRules.TrySpendGrowth(hunter, choice)) return false;
            PlayableGrowthMilestoneRuntime.SynchronizeHunter(hunter);
            EventBus.Publish(new HunterGrowthSpentEvent { HunterId = hunter.InstanceId, Choice = choice });
            return true;
        }
    }

    public struct HunterAdvancedEvent
    {
        public int HunterId;
        public int Age;
        public bool ReachedMilestone;
    }

    public struct HunterGrowthSpentEvent
    {
        public int HunterId;
        public HunterGrowthChoice Choice;
    }

    public struct HunterRetiredEvent
    {
        public int HunterId;
        public int Age;
    }
}
