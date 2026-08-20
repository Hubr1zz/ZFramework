using System;
using UnityEngine;

namespace UI.Hunt
{
    /// <summary>狩猎状态桌的确定性卡位，供运行时与无场景验证共同使用。</summary>
    public static class HuntStatusBoardLayout
    {
        public const int MaximumHunterCards = 4;
        public const float MapEdgeOffset = 2.35f;
        private const float HunterColumnSpacing = 1.56f;
        private const float HunterRowSpacing = 1.92f;

        public static Vector3 SummaryCardLocalPosition => new(0f, 0f, 2.42f);

        public static Vector3 GetHunterCardLocalPosition(int index)
        {
            if (index < 0 || index >= MaximumHunterCards)
                throw new ArgumentOutOfRangeException(nameof(index));
            int column = index % 2;
            int row = index / 2;
            return new Vector3((column - 0.5f) * HunterColumnSpacing, 0f, -0.2f - row * HunterRowSpacing);
        }
    }
}
