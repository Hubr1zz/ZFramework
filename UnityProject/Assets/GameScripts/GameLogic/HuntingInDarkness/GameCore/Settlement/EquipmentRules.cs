namespace HuntingInDarkness.GameCore.Settlement
{
    /// <summary>与具体 ItemData 解耦的猎人装备容量规则。</summary>
    public static class EquipmentRules
    {
        public const int MaximumEquipmentCount = 9;
        public const int MaximumWeaponCount = 2;

        public static bool CanEquip(int equipmentCount, int weaponCount, bool isWeapon, out string reason)
        {
            if (equipmentCount < 0 || weaponCount < 0)
            {
                reason = "装备数量状态无效";
                return false;
            }
            if (equipmentCount >= MaximumEquipmentCount)
            {
                reason = "装备栏已满";
                return false;
            }
            if (isWeapon && weaponCount >= MaximumWeaponCount)
            {
                reason = "武器数量已达上限";
                return false;
            }
            reason = string.Empty;
            return true;
        }
    }
}
