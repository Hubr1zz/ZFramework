using System.Collections.Generic;
using GameplayBase.Board;
using Sirenix.OdinInspector;
using UnityEngine;

namespace SO.Combat
{
    /// <summary>
    /// 场地规则：定义一场战斗的地图与组件布局。
    /// 随 Boss / 地区 / 特殊条件不同而不同，正式游戏中由狩猎阶段选定并注入。
    ///
    /// 出生位置（猎人 / Boss）从角色/Boss 配置迁移到这里，
    /// 因为同一个 Boss / 角色在不同战斗里可能出生在不同位置。
    /// </summary>
    [CreateAssetMenu(fileName = "NewCombatFieldRules", menuName = "CardTactics/Combat/FieldRules")]
    public class CombatFieldRulesSO : ScriptableObject
    {
        [Header("地图")]
        [Min(1)] public int mapRadius = 3;

        [Header("出生点")]
        [Tooltip("猎人小队出生槽，按顺序与小队配对")]
        public List<SpawnSlot> hunterSpawnSlots = new();
        public SpawnSlot bossSpawnSlot = new();

        [Header("固定组件池（生成在预设位置）")]
        public List<FixedComponentEntry> fixedComponents = new();

        [Header("动态组件池（按规则寻找位置，找不到则放弃）")]
        public List<DynamicComponentEntry> dynamicComponents = new();
    }

    /// <summary>出生槽：位置 + 朝向。</summary>
    [System.Serializable]
    public struct SpawnSlot
    {
        public Vector2Int tile;
        public HexDirection facing;
    }

    /// <summary>固定组件项：在预设格子放置一个组件。</summary>
    [System.Serializable]
    public class FixedComponentEntry
    {
        public CombatComponentSO component;
        public Vector2Int tile;
        public HexDirection facing;
    }

    /// <summary>动态组件项：按生成规则放置 count 个组件。</summary>
    [System.Serializable]
    public class DynamicComponentEntry
    {
        public CombatComponentSO component;
        [Min(1)] public int count = 1;

        [Tooltip("全部规则的交集即为合法生成格；为空表示任意空格")]
        [SerializeReference] public List<ComponentSpawnRuleData> rules = new();
    }
}
