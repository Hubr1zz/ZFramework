using UnityEngine;

namespace SO.Combat
{
    /// <summary>
    /// 战场组件模板：地图中的障碍物 / 可互动物体（石头、机关、火堆等）。
    /// 由场地规则的固定池 / 动态池引用，BattleGenerator 据此在棋盘上生成组件实例。
    ///
    /// 注意：具体的交互行为、可视化暂留占位，本期仅打通生成管线。
    /// </summary>
    [CreateAssetMenu(fileName = "NewCombatComponent", menuName = "CardTactics/Combat/CombatComponent")]
    public class CombatComponentSO : ScriptableObject
    {
        [Header("基础信息")]
        public string componentName;
        [Tooltip("用于动态生成规则的引用键（如『石头』），留空则用 componentName")]
        public string componentKey;

        [Header("占格行为")]
        [Tooltip("勾选后该组件所在格视为不可通行/不可生成")]
        public bool blocksMovement = true;

        [Header("视觉（占位）")]
        public Sprite icon;
        public GameObject prefab;

        // TODO: 交互效果（可互动物体被触发时的效果列表），后续接入。

        /// <summary>动态规则匹配用的标识键。</summary>
        public string Key => string.IsNullOrEmpty(componentKey) ? componentName : componentKey;
    }
}
