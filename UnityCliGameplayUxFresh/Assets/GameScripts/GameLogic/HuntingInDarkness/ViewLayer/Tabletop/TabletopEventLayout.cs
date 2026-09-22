using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    /// <summary>跨阶段事件卡的确定性桌面布局。</summary>
    public static class TabletopEventLayout
    {
        public const int CardsPerRow = 4;
        public const float ChoiceSpacingX = 1.62f;
        public const float ChoiceSpacingZ = 1.98f;
        public const float FirstChoiceRowZ = -2.45f;

        public static Vector3 GetChoiceLocalPosition(int index, int count)
        {
            if (count <= 0) throw new System.ArgumentOutOfRangeException(nameof(count));
            if (index < 0 || index >= count) throw new System.ArgumentOutOfRangeException(nameof(index));
            int row = index / CardsPerRow;
            int column = index % CardsPerRow;
            int rowCount = Mathf.Min(CardsPerRow, count - row * CardsPerRow);
            float x = (column - (rowCount - 1) * 0.5f) * ChoiceSpacingX;
            return new Vector3(x, 0f, FirstChoiceRowZ - row * ChoiceSpacingZ);
        }
    }
}
