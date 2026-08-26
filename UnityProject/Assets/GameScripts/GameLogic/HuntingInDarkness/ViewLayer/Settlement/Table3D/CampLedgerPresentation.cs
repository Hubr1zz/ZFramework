using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;

namespace UI
{
    /// <summary>把年鉴持久化标识投影成玩家可读文本，不持有场景对象。</summary>
    public static class CampLedgerPresentation
    {
        public static string FormatResources(IReadOnlyList<string> resources)
        {
            if (resources == null || resources.Count == 0) return "无";
            var counts = new Dictionary<string, int>();
            foreach (string resource in resources)
            {
                string displayName = PlayableSettlementItemRegistry.GetDisplayName(resource);
                if (string.IsNullOrWhiteSpace(displayName)) continue;
                counts.TryGetValue(displayName, out int count);
                counts[displayName] = count + 1;
            }
            if (counts.Count == 0) return "无";
            var labels = new List<string>();
            foreach (KeyValuePair<string, int> pair in counts)
                labels.Add($"{pair.Key}×{pair.Value}");
            return string.Join("、", labels);
        }

        public static string FormatEventMemory(SettlementEventMemory memory)
        {
            if (memory == null) return "无事件结果记录";
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(memory.OptionText)) parts.Add(memory.SelectionMode == EventResolutionSelectionMode.Automatic ? $"自动结算：{memory.OptionText}" : $"选择：{memory.OptionText}");
            if (memory.HasCheck) parts.Add($"{FormatCheckType(memory.CheckType)} {memory.Total}/{memory.Target} · {(memory.Success ? "成功" : "失败")}");
            else parts.Add(memory.Success ? "结果：完成" : "结果：未完成");
            if (!string.IsNullOrWhiteSpace(memory.ResultText)) parts.Add(memory.ResultText);
            var effects = new List<string>();
            foreach (SettlementEventMemoryEffect effect in memory.Effects ?? new List<SettlementEventMemoryEffect>())
            {
                if (effect == null) continue;
                string label = FormatEffectType(effect.EffectType);
                string targetId = string.IsNullOrWhiteSpace(effect.ResolvedTargetId) ? effect.TargetName : effect.ResolvedTargetId;
                string targetName = FormatTarget(targetId);
                if (!string.IsNullOrWhiteSpace(targetName)) label += $"（{targetName}）";
                if (effect.StateChanged) label += $" {effect.PreviousValue}→{effect.CurrentValue} ({effect.CurrentValue - effect.PreviousValue:+#;-#;0})";
                if (!effect.Applied && !string.IsNullOrWhiteSpace(effect.Reason)) label += $"未生效：{FormatFailureReason(effect.Reason)}";
                else if (!effect.Applied) label += "未生效";
                effects.Add(label);
            }
            if (effects.Count > 0) parts.Add($"效果：{string.Join("、", effects)}");
            return string.Join("；", parts);
        }

        private static string FormatCheckType(string value)
        {
            if (!System.Enum.TryParse(value, out CheckType checkType)) return value ?? string.Empty;
            return checkType switch
            {
                CheckType.Courage => "胆识",
                CheckType.Luck => "幸运",
                CheckType.Strength => "力量",
                CheckType.Evasion => "敏捷",
                CheckType.Understanding => "理解",
                CheckType.Custom => "特质",
                _ => "无判定"
            };
        }

        private static string FormatEffectType(string value)
        {
            if (!System.Enum.TryParse(value, out EventEffectType effectType)) return string.IsNullOrWhiteSpace(value) ? "效果" : value;
            return effectType switch
            {
                EventEffectType.AddResource => "获得资源",
                EventEffectType.RemoveResource => "消耗资源",
                EventEffectType.AddWillpower => "意志",
                EventEffectType.RemoveWillpower => "失去意志",
                EventEffectType.AddLuck => "幸运",
                EventEffectType.AddInsanity => "压抑",
                EventEffectType.AddCourage => "胆识",
                EventEffectType.AddUnderstanding => "理解",
                EventEffectType.AddTrait => "获得特性",
                EventEffectType.AddAilment => "获得症状",
                EventEffectType.KillHunter => "猎人死亡",
                EventEffectType.UnlockInvention => "解锁发明",
                EventEffectType.TriggerCombat => "遭遇战斗",
                EventEffectType.AdvanceYear => "推进年份",
                EventEffectType.ScheduleEvent => "安排事件",
                EventEffectType.ActivateBloodline => "激活血脉",
                EventEffectType.AddRecoverableWound => "普通伤势",
                EventEffectType.ExhaustCurrentHuntTileResources => "耗尽地块资源",
                EventEffectType.CreateHuntNoiseLease => "增加下次狩猎风险",
                _ => value
            };
        }

        private static string FormatTarget(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            if (string.Equals(value, "selected", System.StringComparison.OrdinalIgnoreCase)) return "所选猎人";
            string displayName = PlayableSettlementItemRegistry.GetDisplayName(value);
            return string.IsNullOrWhiteSpace(displayName) ? value : displayName;
        }

        private static string FormatFailureReason(string value)
        {
            if (value.Contains("resource", System.StringComparison.OrdinalIgnoreCase)) return "资源不足";
            if (value.Contains("hunter", System.StringComparison.OrdinalIgnoreCase)) return "目标猎人不可用";
            return value;
        }
    }
}
