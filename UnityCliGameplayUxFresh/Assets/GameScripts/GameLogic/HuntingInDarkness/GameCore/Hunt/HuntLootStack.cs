using System;

namespace HuntingInDarkness.GameCore.Hunt
{
    /// <summary>跨阶段保存的狩猎携带物聚合值；只包含稳定物品 ID 与数量。</summary>
    [Serializable]
    public sealed class HuntLootStack
    {
        public string ItemId;
        public int Count;

        public HuntLootStack()
        {
        }

        public HuntLootStack(string itemId, int count)
        {
            ItemId = itemId ?? string.Empty;
            Count = count;
        }
    }
}
