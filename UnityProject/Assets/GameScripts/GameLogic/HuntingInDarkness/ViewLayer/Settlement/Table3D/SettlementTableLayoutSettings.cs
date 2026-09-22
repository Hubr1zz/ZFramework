using System;
using UnityEngine;

namespace UI
{
    /// <summary>程序化营地桌面的可序列化布局预设；场景 prefab 可在 Inspector 中覆盖默认值。</summary>
    [Serializable]
    public sealed class SettlementTableLayoutSettings
    {
        [Header("桌面尺度")]
        [SerializeField, Min(1f)] private float cardScale = 1.35f;
        [SerializeField, Min(0f)] private float slotGap = 0.22f;
        [SerializeField, Min(0f)] private float zoneGap = 1.20f;
        [SerializeField, Min(0f)] private float outerPadding = 1.20f;
        [SerializeField] private Vector2 minimumTableSize = new(12f, 8f);

        [Header("各区最大列数")]
        [SerializeField, Min(1)] private int hunterMaxColumns = 3;
        [SerializeField, Min(1)] private int resourceMaxColumns = 4;
        [SerializeField, Min(1)] private int workshopMaxColumns = 3;
        [SerializeField, Min(1)] private int inventionMaxColumns = 5;

        public float CardScale => Mathf.Max(1f, cardScale);
        public float SlotGap => Mathf.Max(0f, slotGap);
        public float ZoneGap => Mathf.Max(0f, zoneGap);
        public float OuterPadding => Mathf.Max(0f, outerPadding);
        public Vector2 MinimumTableSize => new(Mathf.Max(1f, minimumTableSize.x), Mathf.Max(1f, minimumTableSize.y));
        public int HunterMaxColumns => Mathf.Max(1, hunterMaxColumns);
        public int ResourceMaxColumns => Mathf.Max(1, resourceMaxColumns);
        public int WorkshopMaxColumns => Mathf.Max(1, workshopMaxColumns);
        public int InventionMaxColumns => Mathf.Max(1, inventionMaxColumns);
    }
}
