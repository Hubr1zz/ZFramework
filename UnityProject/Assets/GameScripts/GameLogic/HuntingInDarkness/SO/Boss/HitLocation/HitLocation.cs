using System.Collections.Generic;
using Core;
using GameplayBase.CombatSystem;
using UnityEngine;

namespace SO.Boss.HitLocation
{
    /// <summary>Boss受击部位卡模板（ScriptableObject）</summary>
    [CreateAssetMenu(menuName = "CardTactics/Hit Location")]
    public class HitLocationCardData : ScriptableObject
    {
        [Header("基础信息")]
        public string locationName;
        [TextArea] public string description;
        public int toughness;

        [Header("抽取权重")]
        public int drawWeight = 1;

        [Header("部位血量")]
        public int maxHp = 1;

        [Header("效果列表（triggerCondition + 效果数据）")]
        [SerializeReference]
        public List<HitLocationEffectEntry> effects = new();

        [Header("命中掉落（翻开时触发）")]
        public List<LootEntry> onHitLoot = new();

        [Header("摧毁掉落（HP归零时触发）")]
        public List<LootEntry> onDestroyLoot = new();
    }
}
