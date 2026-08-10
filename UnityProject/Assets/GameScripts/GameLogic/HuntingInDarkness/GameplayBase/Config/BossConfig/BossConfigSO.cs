using System.Collections.Generic;
using Core;
using GameplayBase.CombatSystem;
using SO.Boss.ActionCard;
using SO.Boss.HitLocation;
using UnityEngine;

namespace Config
{
    [CreateAssetMenu(fileName = "NewBossConfig", menuName = "CardTactics/Config/BossConfig")]
    public class BossConfigSO : ScriptableObject
    {
        public string bossName = "Boss";
        public List<BossActionCardData> bossCardPool = new();
        public List<HitLocationCardData> bossHitLocationPool = new();
        // 出生位置已迁移至 CombatFieldRulesSO.bossSpawnSlot（按战斗场地决定）

        [Header("Boss击败掉落（以抽卡形式结算给玩家）")]
        public List<LootEntry> killLoot = new();
    }
}
