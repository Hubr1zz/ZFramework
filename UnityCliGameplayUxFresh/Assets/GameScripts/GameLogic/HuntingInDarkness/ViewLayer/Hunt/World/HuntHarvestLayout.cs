using HuntingInDarkness.GameCore.Hunt;
using UnityEngine;

namespace UI.Hunt
{
    /// <summary>世界空间采集牌的确定性布局预算，供运行时表现和无场景数据验证共同使用。</summary>
    public static class HuntHarvestLayout
    {
        public const int CardsPerRow = 6;
        public const float CardSpacingX = 0.88f;
        public const float CardSpacingZ = 1.16f;

        public static int ClampCardCount(int configuredCount) => Mathf.Clamp(configuredCount, 0, HarvestDrawPlan.MaximumCardCount);

        public static Vector3 GetCardLocalPosition(int cardIndex, int cardCount)
        {
            int safeCount = ClampCardCount(cardCount);
            if (cardIndex < 0 || cardIndex >= safeCount)
                throw new System.ArgumentOutOfRangeException(nameof(cardIndex));
            int row = cardIndex / CardsPerRow;
            int column = cardIndex % CardsPerRow;
            int rowCardCount = Mathf.Min(CardsPerRow, safeCount - row * CardsPerRow);
            float x = (column - (rowCardCount - 1) * 0.5f) * CardSpacingX;
            return new Vector3(x, 0f, -row * CardSpacingZ);
        }

        public static Vector3 GetCloseCardLocalPosition(int cardCount)
        {
            int safeCount = ClampCardCount(cardCount);
            int rowCount = Mathf.Max(1, Mathf.CeilToInt(safeCount / (float)CardsPerRow));
            return new Vector3(0f, 0f, -(rowCount - 1) * CardSpacingZ - 1.12f);
        }
    }
}
