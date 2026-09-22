using Core;
using GameplayBase.Card.CharacterActionCard;
using GameplayBase.Card.Effect;

namespace HuntingInDarkness.Combat
{
    /// <summary>可序列化的基础攻击效果配置，补齐旧项目只有运行时效果、无法在行动卡资产中配置的缺口。</summary>
    [System.Serializable]
    public sealed class PlayableAttackEffectData : CharacterActionCardEffectData
    {
        public override CharacterActionCardEffect CreateRuntime() => new PlayablePreparedAttackEffect();
    }
}
