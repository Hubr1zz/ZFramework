using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Hunt
{
    public readonly struct HarvestCardResult
    {
        public int CardIndex { get; }
        public bool IsHit { get; }

        public HarvestCardResult(int cardIndex, bool isHit)
        {
            CardIndex = cardIndex;
            IsHit = isHit;
        }
    }

    /// <summary>一次采集的不可变逐卡结果；表现层只负责按顺序揭示。</summary>
    public sealed class HarvestDrawPlan
    {
        public const int MaximumCardCount = 32;

        private readonly IReadOnlyList<HarvestCardResult> cards;

        public IReadOnlyList<HarvestCardResult> Cards => cards;
        public int CardCount => cards.Count;
        public int HitCount { get; }

        public HarvestDrawPlan(IReadOnlyList<HarvestCardResult> cards)
        {
            this.cards = cards ?? new HarvestCardResult[0];
            int hitCount = 0;
            foreach (HarvestCardResult card in this.cards)
                if (card.IsHit)
                    hitCount++;
            HitCount = hitCount;
        }
    }
}
