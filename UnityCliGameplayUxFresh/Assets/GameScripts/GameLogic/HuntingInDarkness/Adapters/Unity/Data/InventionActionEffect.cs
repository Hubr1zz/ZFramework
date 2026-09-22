using UnityEngine;

namespace HuntingInDarkness.Data
{
    public enum InventionActionEffectKind
    {
        None,
        ModifyHarvestHitChance
    }

    /// <summary>由发明向阶段 ActionQueue 注入的表驱动规则，不直接持有 Runner 或场景对象。</summary>
    [System.Serializable]
    public sealed class InventionActionEffect
    {
        [Tooltip("稳定效果 ID；用于去重和未来存档迁移。")]
        public string effectId;
        public InventionActionEffectKind kind;
        [Tooltip("目标内容关键词；ItemTag 会以同名小写关键词参与匹配。")]
        public string targetKeyword;
        [Tooltip("加法修正值。概率类效果使用 0-1 区间，例如 0.1 表示 +10%。")]
        public float value;
    }
}
