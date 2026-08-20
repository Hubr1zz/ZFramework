using System;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;

namespace HuntingInDarkness.Settlement
{
    /// <summary>把 ItemData 防具配置映射到纯 GameCore 装备规则。</summary>
    public static class PlayableEquipmentRules
    {
        public static bool CanEquip(HunterInstance hunter, ItemData item, out string reason)
        {
            if (hunter == null || item == null)
            {
                reason = "猎人或装备无效";
                return false;
            }
            if (!hunter.IsAvailable)
            {
                reason = hunter.IsAlive ? "已退休猎人不能装备物品" : "已死亡猎人不能装备物品";
                return false;
            }
            if (item.itemType == ItemType.Resource)
            {
                reason = "资源不能放入装备栏";
                return false;
            }

            int runtimeCount = hunter.Equipment?.Count ?? 0;
            int savedCount = Math.Max(hunter.EquippedItemIds?.Count ?? 0, hunter.EquippedItemNames?.Count ?? 0);
            int weaponCount = hunter.Equipment?.FindAll(instance => instance?.Data?.itemType == ItemType.Weapon).Count ?? 0;
            if (!EquipmentRules.CanEquip(Math.Max(runtimeCount, savedCount), weaponCount, item.itemType == ItemType.Weapon, out reason))
                return false;
            if (item.itemType != ItemType.Armor)
                return true;

            ArmorCoverage occupied = ArmorCoverage.None;
            if (hunter.Equipment != null)
            {
                foreach (ItemInstance equipped in hunter.Equipment)
                    if (equipped?.Data?.itemType == ItemType.Armor)
                        occupied |= GetCoverage(equipped.Data);
            }

            return ArmorCoverageRules.CanEquip(occupied, GetCoverage(item), out reason);
        }

        public static ArmorCoverage GetCoverage(ItemData item)
        {
            if (item?.itemType != ItemType.Armor || item.armorStats == null) return ArmorCoverage.None;

            ArmorCoverage coverage = ArmorCoverage.None;
            if (item.armorStats.armorHead > 0) coverage |= ArmorCoverage.Head;
            if (item.armorStats.armorBody > 0) coverage |= ArmorCoverage.Torso;
            if (item.armorStats.armorArms > 0) coverage |= ArmorCoverage.Arms;
            if (item.armorStats.armorLegs > 0) coverage |= ArmorCoverage.Legs;
            return coverage;
        }
    }
}
