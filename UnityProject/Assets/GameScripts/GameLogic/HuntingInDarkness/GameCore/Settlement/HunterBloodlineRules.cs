using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Foundation;

namespace HuntingInDarkness.GameCore.Settlement
{
    public sealed class HunterBloodlineDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string ActivationHint { get; }
        public int DrawWeight { get; }

        public HunterBloodlineDefinition(string id, string displayName, string description, string activationHint, int drawWeight)
        {
            Id = id?.Trim() ?? string.Empty;
            DisplayName = displayName?.Trim() ?? string.Empty;
            Description = description?.Trim() ?? string.Empty;
            ActivationHint = activationHint?.Trim() ?? string.Empty;
            DrawWeight = drawWeight;
        }
    }

    /// <summary>血脉只在猎人获得时抽取一次；激活效果由后续内容通过稳定 ID 接入。</summary>
    public static class HunterBloodlineRules
    {
        public static bool TryAssign(HunterState hunter, IReadOnlyList<HunterBloodlineDefinition> definitions, IRandomSource random, out HunterBloodlineDefinition assigned, out string reason)
        {
            assigned = null;
            if (hunter == null)
            {
                reason = "猎人不存在。";
                return false;
            }
            if (definitions == null || definitions.Count == 0)
            {
                reason = "没有可用的血脉内容。";
                return false;
            }
            if (random == null)
            {
                reason = "血脉随机源尚未配置。";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(hunter.BloodlineId))
            {
                assigned = Find(definitions, hunter.BloodlineId);
                if (assigned == null)
                {
                    reason = $"找不到猎人的血脉内容：{hunter.BloodlineId}";
                    return false;
                }

                hunter.BloodlineId = assigned.Id;
                hunter.BloodlineName = assigned.DisplayName;
                reason = string.Empty;
                return true;
            }

            int totalWeight = 0;
            var validDefinitions = new List<HunterBloodlineDefinition>();
            var knownIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (HunterBloodlineDefinition definition in definitions)
            {
                if (definition == null || definition.Id.Length == 0 || definition.DisplayName.Length == 0 || definition.DrawWeight <= 0 || !knownIds.Add(definition.Id))
                    continue;
                if (totalWeight > int.MaxValue - definition.DrawWeight)
                {
                    reason = "血脉权重总和超出可表示范围。";
                    return false;
                }
                validDefinitions.Add(definition);
                totalWeight += definition.DrawWeight;
            }
            if (validDefinitions.Count == 0)
            {
                reason = "血脉内容没有有效条目。";
                return false;
            }

            int roll = random.Next(0, totalWeight);
            foreach (HunterBloodlineDefinition definition in validDefinitions)
            {
                if (roll < definition.DrawWeight)
                {
                    assigned = definition;
                    break;
                }
                roll -= definition.DrawWeight;
            }
            assigned ??= validDefinitions[validDefinitions.Count - 1];
            hunter.BloodlineId = assigned.Id;
            hunter.BloodlineName = assigned.DisplayName;
            hunter.IsBloodlineActivated = false;
            reason = string.Empty;
            return true;
        }

        public static bool TryActivate(HunterState hunter, string bloodlineId, out string reason)
        {
            if (hunter == null || string.IsNullOrWhiteSpace(hunter.BloodlineId))
            {
                reason = "猎人尚未拥有血脉。";
                return false;
            }
            if (!string.Equals(hunter.BloodlineId, bloodlineId?.Trim(), StringComparison.Ordinal))
            {
                reason = "激活请求与猎人的血脉不匹配。";
                return false;
            }

            hunter.IsBloodlineActivated = true;
            reason = string.Empty;
            return true;
        }

        private static HunterBloodlineDefinition Find(IReadOnlyList<HunterBloodlineDefinition> definitions, string id)
        {
            string normalizedId = id?.Trim() ?? string.Empty;
            foreach (HunterBloodlineDefinition definition in definitions)
                if (definition != null && string.Equals(definition.Id, normalizedId, StringComparison.Ordinal))
                    return definition;
            return null;
        }
    }
}
