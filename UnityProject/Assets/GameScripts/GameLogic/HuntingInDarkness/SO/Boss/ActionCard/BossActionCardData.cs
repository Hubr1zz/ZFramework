using System.Collections.Generic;
using Core;
using UnityEngine;

namespace SO.Boss.ActionCard
{
    /// <summary>Boss行动卡模板</summary>
    [CreateAssetMenu(fileName = "", menuName = "CardTactics/Boss/ActionCard")]
    public class BossActionCardData : ScriptableObject
    {
        public string actionName;
        [TextArea] public string description;
        public int timePointCost;

        [Header("效果（BossActionCardEffect 子类）")]
        [SerializeReference]
        public List<BossActionCardEffectData> effects = new();

        [Header("抽取权重（用于加权随机）")]
        public int drawWeight = 1;

        [Header("条件：仅当满足时才可被抽到")]
        // TODO: 后续扩展为 List<BossCardConditionSO>，支持
        //   "Boss HP < 50%"、"回合数 >= N"、"特定部位已摧毁" 等条件组合。
        public bool alwaysAvailable = true;
    }
}
