using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    /// <summary>出猎编队桌使用的只读猎人决策摘要，不拥有或修改玩法状态。</summary>
    public readonly struct HuntDepartureHunterPresentation
    {
        private const int VisibleTraitCount = 3;
        private const int VisibleEquipmentCount = 3;
        private const int VisibleNameLength = 10;

        private HuntDepartureHunterPresentation(string title, string body, string footer)
        {
            Title = title;
            Body = body;
            Footer = footer;
        }

        public string Title { get; }
        public string Body { get; }
        public string Footer { get; }

        public static HuntDepartureHunterPresentation Create(HunterInstance hunter)
        {
            if (hunter == null)
                return new HuntDepartureHunterPresentation("无效猎人", "猎人数据缺失。", "请选择另一张猎人卡");
            string body = $"头 {FormatHealth(hunter.HP?.head, hunter.MaxHP?.head)}  躯 {FormatHealth(hunter.HP?.body, hunter.MaxHP?.body)}  臂 {FormatHealth(hunter.HP?.arms, hunter.MaxHP?.arms)}  腿 {FormatHealth(hunter.HP?.legs, hunter.MaxHP?.legs)}\n" +
                $"意志 {hunter.Willpower}/{hunter.WillpowerMax}  力 {hunter.Stats?.strength ?? 0}  准 {hunter.Stats?.accuracy ?? 0}  敏 {hunter.Stats?.evasion ?? 0}  移 {hunter.Stats?.movement ?? 0}  速 {hunter.Stats?.speed ?? 0}\n" +
                $"特性 · {BuildTraitSummary(hunter.Traits)}\n" +
                $"装备 · {BuildEquipmentSummary(hunter.Equipment)}\n" +
                $"装备噪音 {FormatSigned(PlayableHuntNoiseProfile.GetEquipmentNoiseContribution(hunter))}";
            return new HuntDepartureHunterPresentation($"{BoundName(hunter.Name, 16)} · 出猎整备", body, "点击查看其他猎人 · 拖拽只调整编队");
        }

        private static string FormatHealth(int? current, int? maximum) => current.HasValue && maximum.HasValue ? $"{current.Value}/{maximum.Value}" : "?/?";

        private static string BuildTraitSummary(IReadOnlyList<string> traits)
        {
            if (traits == null || traits.Count == 0) return "无";
            var names = new List<string>();
            int count = Math.Min(VisibleTraitCount, traits.Count);
            for (int index = 0; index < count; index++)
                names.Add(BoundName(PlayableTraitRegistry.GetDisplayName(traits[index])));
            string remaining = traits.Count > count ? $" +{traits.Count - count}" : string.Empty;
            return $"{string.Join("、", names)}{remaining}";
        }

        private static string BuildEquipmentSummary(IReadOnlyList<ItemInstance> equipment)
        {
            if (equipment == null || equipment.Count == 0) return "无";
            var names = new List<string>();
            int validCount = 0;
            foreach (ItemInstance item in equipment)
            {
                if (item?.Data == null) continue;
                validCount++;
                if (names.Count >= VisibleEquipmentCount) continue;
                string count = item.Count > 1 ? $"×{item.Count}" : string.Empty;
                names.Add($"{BoundName(item.Data.itemName)}{count}");
            }
            if (validCount == 0) return "无";
            string remaining = validCount > names.Count ? $" +{validCount - names.Count}" : string.Empty;
            return $"{string.Join("、", names)}{remaining}";
        }

        private static string BoundName(string value) => BoundName(value, VisibleNameLength);

        private static string BoundName(string value, int maximumLength)
        {
            string name = string.IsNullOrWhiteSpace(value) ? "未知" : value.Trim();
            return name.Length > maximumLength ? $"{name.Substring(0, maximumLength)}…" : name;
        }

        private static string FormatSigned(int value) => value > 0 ? $"+{value}" : value.ToString();
    }
}
