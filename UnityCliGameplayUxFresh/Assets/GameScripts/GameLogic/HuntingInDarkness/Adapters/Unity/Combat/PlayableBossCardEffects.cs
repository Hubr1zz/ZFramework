using Core;
using GameplayBase.Card.BossActionCard;
using GameplayBase.Card.Effect;
using HuntingInDarkness.GameCore.Combat;
using UnityEngine;

namespace HuntingInDarkness.Combat
{
    /// <summary>Playable Boss 行动卡使用的可序列化攻击配置。</summary>
    [System.Serializable]
    public sealed class PlayableBossAttackEffectData : BossActionCardEffectData
    {
        [SerializeField] private string actionName = "怪物攻击";
        [SerializeField, Min(1)] private int woundCount = 1;
        [SerializeField, Min(1)] private int accuracy = 2;
        [SerializeField, Min(1)] private int attackCount = 1;
        [SerializeField] private BossTargetPolicy targetPolicy = BossTargetPolicy.PlayerChoice;
        [SerializeField, Min(0)] private int movementDistance = 1;
        [SerializeField, Min(0)] private int attackRange = 1;
        [SerializeField, TextArea] private string noTargetOrRangeFailure = "怪物没有可攻击的目标，本次攻击落空。";
        [SerializeField] private string mindBurdenName;

        public string ActionName => string.IsNullOrWhiteSpace(actionName) ? "怪物攻击" : actionName;
        public int WoundCount => Mathf.Max(1, woundCount);
        public int Accuracy => Mathf.Max(1, accuracy);
        public int AttackCount => Mathf.Max(1, attackCount);
        public BossTargetPolicy TargetPolicy => targetPolicy;
        public int MovementDistance => Mathf.Max(0, movementDistance);
        public int AttackRange => Mathf.Max(0, attackRange);
        public string NoTargetOrRangeFailure => string.IsNullOrWhiteSpace(noTargetOrRangeFailure) ? "怪物没有可攻击的目标，本次攻击落空。" : noTargetOrRangeFailure;
        public string MindBurdenName => mindBurdenName?.Trim() ?? string.Empty;
        public override BossActionCardEffect CreateRuntime() => new PlayableDirectedBossAttackEffect(ActionName, WoundCount, Accuracy, AttackCount, TargetPolicy, MovementDistance, AttackRange, NoTargetOrRangeFailure, MindBurdenName);
    }
}
