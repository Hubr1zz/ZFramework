namespace HuntingInDarkness.GameCore.Settlement
{
    public enum HunterGrowthChoice
    {
        Courage,
        Understanding
    }

    public readonly struct HunterAdvancementOutcome
    {
        public bool Advanced { get; }
        public int PreviousAge { get; }
        public int CurrentAge { get; }
        public bool ReachedMilestone { get; }
        public bool Retired { get; }

        public HunterAdvancementOutcome(bool advanced, int previousAge, int currentAge, bool reachedMilestone, bool retired = false)
        {
            Advanced = advanced;
            PreviousAge = previousAge;
            CurrentAge = currentAge;
            ReachedMilestone = reachedMilestone;
            Retired = retired;
        }
    }

    public static class HunterAdvancementRules
    {
        public const int MaximumAge = 12;
        public const int MaximumGrowthAttribute = 8;

        public static HunterAdvancementOutcome AdvanceAfterHunt(HunterState hunter)
        {
            if (hunter == null || !hunter.IsAvailable)
                return new HunterAdvancementOutcome(false, hunter?.Age ?? 0, hunter?.Age ?? 0, false);
            if (hunter.Age >= MaximumAge)
            {
                hunter.Availability = HunterAvailabilityState.Retired;
                return new HunterAdvancementOutcome(false, hunter.Age, hunter.Age, false, true);
            }

            int previousAge = hunter.Age;
            hunter.Age++;
            hunter.UnspentGrowth++;
            bool reachedMilestone = hunter.Age == 2 || hunter.Age == 5 || hunter.Age == 8;
            return new HunterAdvancementOutcome(true, previousAge, hunter.Age, reachedMilestone);
        }

        public static bool TrySpendGrowth(HunterState hunter, HunterGrowthChoice choice)
        {
            if (!CanSpendGrowth(hunter, choice, out _)) return false;

            if (choice == HunterGrowthChoice.Courage)
                hunter.Courage++;
            else
                hunter.Understanding++;
            hunter.UnspentGrowth--;
            return true;
        }

        public static bool CanSpendGrowth(HunterState hunter, HunterGrowthChoice choice, out string reason)
        {
            if (hunter == null || !hunter.IsAvailable)
            {
                reason = "猎人当前无法分配成长。";
                return false;
            }
            if (hunter.UnspentGrowth <= 0)
            {
                reason = "没有待分配的成长点。";
                return false;
            }
            if (choice != HunterGrowthChoice.Courage && choice != HunterGrowthChoice.Understanding)
            {
                reason = "成长方向无效。";
                return false;
            }
            if ((choice == HunterGrowthChoice.Courage && hunter.Courage >= MaximumGrowthAttribute) || (choice == HunterGrowthChoice.Understanding && hunter.Understanding >= MaximumGrowthAttribute))
            {
                reason = "该成长属性已达到上限。";
                return false;
            }
            reason = string.Empty;
            return true;
        }
    }
}
