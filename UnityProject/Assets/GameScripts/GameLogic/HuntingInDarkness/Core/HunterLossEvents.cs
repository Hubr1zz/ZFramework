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

        public HunterDiedEvent(int hunterId, string hunterName, int year, int growthPerHunter, int inspiredHunterCount)
        {
            HunterId = hunterId;
            HunterName = hunterName ?? string.Empty;
            Year = year;
            GrowthPerHunter = growthPerHunter;
            InspiredHunterCount = inspiredHunterCount;
        }
    }
}
