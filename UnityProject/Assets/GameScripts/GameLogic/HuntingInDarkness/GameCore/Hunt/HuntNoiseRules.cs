using System;
using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Hunt
{
    /// <summary>狩猎噪音牌堆的最小配置。</summary>
    public readonly struct HuntNoiseDefinition
    {
        public HuntNoiseDefinition(int deckSize, int baseNoisePerHunter, int maxDangerCards)
        {
            DeckSize = Math.Max(0, deckSize);
            BaseNoisePerHunter = Math.Max(0, baseNoisePerHunter);
            MaxDangerCards = Math.Max(0, maxDangerCards);
        }

        public int DeckSize { get; }
        public int BaseNoisePerHunter { get; }
        public int MaxDangerCards { get; }
        public bool IsEnabled => DeckSize > 0 && MaxDangerCards > 0;
    }

    /// <summary>一次狩猎噪音判定所需的不可变数据。</summary>
    public readonly struct NoiseCheckPlan
    {
        internal NoiseCheckPlan(int noiseScore, int dangerCardCount, int deckSize, bool isEnabled)
        {
            NoiseScore = Math.Max(0, noiseScore);
            DeckSize = Math.Max(0, deckSize);
            DangerCardCount = Math.Max(0, Math.Min(dangerCardCount, DeckSize));
            IsEnabled = isEnabled;
        }

        public int NoiseScore { get; }
        public int DangerCardCount { get; }
        public int DeckSize { get; }
        public bool IsEnabled { get; }

        public bool IsDangerCard(int cardValue) => IsEnabled && cardValue > 0 && cardValue <= DangerCardCount;
    }

    /// <summary>只计算狩猎噪音牌堆，不负责抽牌或触发事件。</summary>
    public static class HuntNoiseRules
    {
        public static NoiseCheckPlan CreatePlan(int livingHunterCount, IEnumerable<int> equipmentNoiseValues, HuntNoiseDefinition definition)
        {
            int safeHunterCount = Math.Max(0, livingHunterCount);
            long equipmentNoise = 0;
            if (equipmentNoiseValues != null)
                foreach (int value in equipmentNoiseValues)
                    equipmentNoise = Math.Min(int.MaxValue, equipmentNoise + Math.Max(0, value));

            int noiseScore = ClampToInt((long)safeHunterCount * definition.BaseNoisePerHunter + equipmentNoise);
            int dangerCardCount = Math.Min(noiseScore, Math.Min(definition.MaxDangerCards, definition.DeckSize));
            return new NoiseCheckPlan(noiseScore, dangerCardCount, definition.DeckSize, definition.IsEnabled);
        }

        public static NoiseCheckPlan ApplyNoiseModifier(NoiseCheckPlan plan, int modifier, int maxDangerCards)
        {
            if (!plan.IsEnabled) return plan;
            int noiseScore = ClampToInt((long)plan.NoiseScore + modifier);
            int dangerCardCount = Math.Min(noiseScore, Math.Min(Math.Max(0, maxDangerCards), plan.DeckSize));
            return new NoiseCheckPlan(noiseScore, dangerCardCount, plan.DeckSize, true);
        }

        private static int ClampToInt(long value) => (int)Math.Max(0L, Math.Min(int.MaxValue, value));
    }
}
