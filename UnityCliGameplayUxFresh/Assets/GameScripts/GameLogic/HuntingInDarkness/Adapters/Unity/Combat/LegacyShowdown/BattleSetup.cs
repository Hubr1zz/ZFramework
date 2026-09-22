using System.Collections.Generic;
using Config;
using GameplayBase.Config;
using SO.Combat;
using SO.Boss.ActionCard;

namespace GameplayBase.CombatSystem
{
    /// <summary>
    /// 一场战斗的装配载荷：场地规则 + 猎人小队 + Boss。
    /// 正式游戏中由狩猎阶段组装（当前小队 / 所在地图 / 触发的 Boss）并注入 GameManager；
    /// 测试场景中由 CombatTestBootstrap 直接配置。
    /// </summary>
    public class BattleSetup
    {
        public CombatFieldRulesSO       FieldRules;
        public List<CharacterConfigSO>  HunterSquad = new();
        public List<CharacterActionCardData> SharedHunterCards = new();
        public BossConfigSO             Boss;
    }
}
