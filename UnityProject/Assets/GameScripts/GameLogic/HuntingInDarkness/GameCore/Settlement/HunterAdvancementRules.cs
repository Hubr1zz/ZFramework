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
            if (hunter == null || !hunter.IsAvailable || hunter.UnspentGrowth <= 0) return false;
            if (choice != HunterGrowthChoice.Courage && choice != HunterGrowthChoice.Understanding) return false;
            if (choice == HunterGrowthChoice.Courage && hunter.Courage >= MaximumGrowthAttribute) return false;
            if (choice == HunterGrowthChoice.Understanding && hunter.Understanding >= MaximumGrowthAttribute) return false;

            if (choice == HunterGrowthChoice.Courage)
                hunter.Courage++;
            else
                hunter.Understanding++;
            hunter.UnspentGrowth--;
            return true;
        }
    }
}
