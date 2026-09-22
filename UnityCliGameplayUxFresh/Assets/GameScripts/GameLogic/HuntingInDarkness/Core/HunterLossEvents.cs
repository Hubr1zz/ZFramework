namespace Core
{
    /// <summary>永久死亡事务完成后发布，供非阻塞表现层消费。</summary>
    public readonly struct HunterDiedEvent
    {
        public int HunterId { get; }
        public string HunterName { get; }
        public int Year { get; }
        public int GrowthPerHunter { get; }
        public int InspiredHunterCount { get; }
        public string CauseId { get; }
        public string CauseText { get; }

        public HunterDiedEvent(int hunterId, string hunterName, int year, int growthPerHunter, int inspiredHunterCount)
            : this(hunterId, hunterName, year, growthPerHunter, inspiredHunterCount, string.Empty, string.Empty)
        {
        }

        public HunterDiedEvent(int hunterId, string hunterName, int year, int growthPerHunter, int inspiredHunterCount, string causeId, string causeText)
        {
            HunterId = hunterId;
            HunterName = hunterName ?? string.Empty;
            Year = year;
            GrowthPerHunter = growthPerHunter;
            InspiredHunterCount = inspiredHunterCount;
            CauseId = causeId ?? string.Empty;
            CauseText = causeText ?? string.Empty;
        }
    }
}
