using UnityEngine;

namespace Cards3D
{
    /// <summary>卡面文字在世界空间中的可读性基线。</summary>
    public static class CardPresentationConsts
    {
        public const float DynamicTitleFontSize = 1.60f;
        public const float CompactTitleFontSize = 1.40f;
        public const float DynamicBodyFontSize = 1.10f;
        public const float LegacyFontSizeThreshold = 0.25f;
        public const float DynamicTitleRectHeight = 0.34f;
        public const float DynamicBodyRectHeight = 0.22f;

        public static bool IsTitle(string objectName) => objectName == "Title" || objectName == "Name" || objectName == "GridLabel";

        public static float ResolveDynamicFontSize(string objectName, float requested)
        {
            if (requested >= LegacyFontSizeThreshold) return requested;
            return IsTitle(objectName) ? DynamicTitleFontSize : DynamicBodyFontSize;
        }

        public static Vector2 ResolveDynamicRectSize(string objectName, Vector2 requested)
        {
            float minimumHeight = IsTitle(objectName) ? DynamicTitleRectHeight : DynamicBodyRectHeight;
            return new Vector2(requested.x, Mathf.Max(requested.y, minimumHeight));
        }
    }
}
