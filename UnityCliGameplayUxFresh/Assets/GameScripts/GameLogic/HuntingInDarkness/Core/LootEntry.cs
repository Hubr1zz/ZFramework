using UnityEngine;

namespace Core
{
    /// <summary>
    /// 战利品掉落词条。挂在 BossConfigSO / HitLocationCardData 上配置。
    /// </summary>
    [System.Serializable]
    public class LootEntry
    {
        [Tooltip("资源名称（与 SettlementManager 资源字典的键匹配）")]
        public string resourceName;

        [Tooltip("掉落概率 0-1")]
        [Range(0f, 1f)]
        public float chance = 1f;

        [Tooltip("掉落数量")]
        [Min(1)]
        public int count = 1;
    }
}
