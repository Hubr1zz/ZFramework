using SO.Character;
using UnityEngine;

namespace HuntingInDarkness.Combat
{
    /// <summary>营地装备投影到旧战斗接口时需要的可配置降级内容。</summary>
    [CreateAssetMenu(fileName = "PlayableCombatEquipmentCatalog", menuName = "Hunting in Darkness/Combat Equipment Catalog")]
    public sealed class PlayableCombatEquipmentCatalog : ScriptableObject
    {
        [SerializeField] private WeaponData unarmedWeapon;

        public WeaponData UnarmedWeapon => unarmedWeapon;
        public bool IsConfigured => unarmedWeapon != null;
    }
}
