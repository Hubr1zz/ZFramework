using System.Collections.Generic;
using GameplayBase;
using GameplayBase.Card.BossActionCard;
using GameplayBase.Card.CharacterActionCard;
using GameplayBase.CombatSystem;
using GameplayBase.CombatSystem.Cards.FlipConditions;
using SO.Boss.ActionCard;
using UI;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 效果数据泛型基类 — 可序列化，Inspector 中配置具体子类参数。
    /// </summary>
    [System.Serializable]
    public abstract class EffectData<T>
    {
        public abstract T CreateRuntime();
    }

    /// <summary>角色行动卡效果数据基类</summary>
    [System.Serializable]
    public abstract class CharacterActionCardEffectData : EffectData<CharacterActionCardEffect>
    {
        [Header("目标 / 范围（可空；范围是目标选择的一部分）")]
        [SerializeReference] public TargetingRuleData targeting;
    }

    /// <summary>Boss行动卡效果数据基类</summary>
    [System.Serializable]
    public abstract class BossActionCardEffectData : EffectData<BossActionCardEffect> { }

    /// <summary>翻面条件数据基类</summary>
    [System.Serializable]
    public abstract class FlipConditionData
    {
        public abstract IFlipCondition CreateRuntime();
    }
}
