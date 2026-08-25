using System;
using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Hunt
{
    public readonly struct HarvestCardResult
    {
        public int CardIndex { get; }
        public bool IsHit { get; }
        public string MaterialId { get; }
        public string MaterialName { get; }

        public HarvestCardResult(int cardIndex, bool isHit, string materialId = "", string materialName = "")
        {
            CardIndex = cardIndex;
            IsHit = isHit;
            MaterialId = materialId ?? string.Empty;
            MaterialName = materialName ?? string.Empty;
        }
    }

    public readonly struct HarvestMaterialDefinition
    {
        public HarvestMaterialDefinition(string materialId, string displayName, double hitChance)
        {
            MaterialId = materialId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            HitChance = Math.Max(0d, Math.Min(hitChance, 1d));
        }

        public string MaterialId { get; }
        public string DisplayName { get; }
        public double HitChance { get; }
    }

    /// <summary>一次采集的不可变逐卡结果；表现层只负责按顺序揭示。</summary>
    public sealed class HarvestDrawPlan
    {
        public const int MaximumCardCount = 32;

        private readonly IReadOnlyList<HarvestCardResult> cards;

        public IReadOnlyList<HarvestCardResult> Cards => cards;
        public int CardCount => cards.Count;
        public int RevealLimit { get; }
        public int HitCount { get; }
        public double HitChance { get; }

        public HarvestDrawPlan(IReadOnlyList<HarvestCardResult> cards, double hitChance = 0d, int? revealLimit = null)
        {
            this.cards = cards ?? new HarvestCardResult[0];
            HitChance = Math.Max(0d, Math.Min(hitChance, 1d));
            RevealLimit = Math.Max(0, Math.Min(revealLimit ?? this.cards.Count, this.cards.Count));
            int hitCount = 0;
            foreach (HarvestCardResult card in this.cards)
                if (card.IsHit)
                    hitCount++;
            HitCount = hitCount;
        }
    }
}
