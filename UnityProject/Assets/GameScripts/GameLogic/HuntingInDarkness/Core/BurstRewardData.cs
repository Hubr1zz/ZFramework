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
    /// 角色行动卡：爆发奖励配置。策划在 CardData 上配置。
    /// 玩家右键正面卡牌时：不触发正面效果，获得此处配置的资源，然后翻面。
    /// </summary>
    [System.Serializable]
    public class BurstRewardData
    {
        public bool enabled = true;

        [Header("资源奖励（可同时给多种）")]
        public int currencyReward;       // 费用/货币
        public int timePointReward;      // 时点返还（负值表示消耗更少时点）

        [Header("描述（UI展示）")]
        public string rewardDescription; // 例如："获得 2 费用"

        [Header("附加效果（可选，弃置时额外触发）")]
        [SerializeReference]
        public List<CharacterActionCardEffectData> bonusEffects = new();
    }
}
