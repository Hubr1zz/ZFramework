// ═══════════════════════════════════════════
// 武器数据（预留）
// ═══════════════════════════════════════════

using UnityEngine;

namespace SO.Character
{
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "CardTactics/WeaponData")]
    public class WeaponData : ScriptableObject
    {
        public string weaponName;
        public int strengthBonus;
        public int speedBonus;

        public WeaponEffect effect;
    }
}