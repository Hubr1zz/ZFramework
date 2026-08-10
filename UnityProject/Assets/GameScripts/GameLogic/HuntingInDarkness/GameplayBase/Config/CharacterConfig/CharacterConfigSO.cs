using System.Collections.Generic;
using Core;
using GameplayBase.CombatSystem;
using SO.Boss.ActionCard;
using SO.Character;
using UnityEngine;

namespace GameplayBase.Config
{
    [CreateAssetMenu(fileName = "NewCharacterConfig", menuName = "CardTactics/Config/CharacterConfig")]
    public class CharacterConfigSO : ScriptableObject
    {
        public string characterName;
        public List<CharacterActionCardData> startingCards = new();
        public CharacterCombatStats combatStats;
        public WeaponData startingWeapon;
        [Min(0)] public int startingWillpower = 2;
        [Min(0)] public int startingCombatInspiration;
        // 出生位置已迁移至 CombatFieldRulesSO.hunterSpawnSlots（按战斗场地决定）
    }
}
