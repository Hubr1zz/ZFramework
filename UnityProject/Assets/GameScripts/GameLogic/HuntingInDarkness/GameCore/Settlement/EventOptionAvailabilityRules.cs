using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Foundation;

namespace HuntingInDarkness.GameCore.Settlement
{
    public enum EventOptionConditionKind
    {
        MinimumCourage,
        MinimumUnderstanding,
        MinimumStrength,
        MinimumAccuracy,
        MinimumEvasion,
        MinimumWillpower,
        MaximumLuck,
        HasTrait,
        HasAilment,
        MinimumResource,
        HasEquippedItem,
        HasKeyword,
        HasBloodline,
        HasActiveBloodline
    }

    public readonly struct EventOptionConditionDefinition
    {
        public EventOptionConditionKind Kind { get; }
        public string Key { get; }
        public string DisplayName { get; }
        public int Value { get; }
        public bool Inverted { get; }

        public EventOptionConditionDefinition(EventOptionConditionKind kind, string key, int value, bool inverted, string displayName = null)
        {
            Kind = kind;
            Key = key ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Key : displayName.Trim();
            Value = Math.Max(0, value);
            Inverted = inverted;
        }
    }

    public static class EventOptionAvailabilityRules
    {
        public static bool RequiresHunter(EventOptionConditionDefinition condition)
        {
            return condition.Kind != EventOptionConditionKind.MinimumResource;
        }

        public static bool Evaluate(IReadOnlyList<EventOptionConditionDefinition> conditions, HunterState hunter, Func<string, int> resourceResolver, IReadOnlyCollection<string> equippedItems, out string reason)
        {
            return Evaluate(conditions, hunter, resourceResolver, equippedItems, null, out reason);
        }

        public static bool Evaluate(IReadOnlyList<EventOptionConditionDefinition> conditions, HunterState hunter, Func<string, int> resourceResolver, IReadOnlyCollection<string> equippedItems, IReadOnlyCollection<string> keywords, out string reason)
        {
            if (conditions == null || conditions.Count == 0)
            {
                reason = "该选项没有配置可用条件。";
                return false;
            }

            foreach (EventOptionConditionDefinition condition in conditions)
            {
                bool passed = Evaluate(condition, hunter, resourceResolver, equippedItems);
                if (condition.Kind == EventOptionConditionKind.HasKeyword)
                    passed = KeywordRules.Contains(keywords, condition.Key);
                if (condition.Inverted) passed = !passed;
                if (passed) continue;
                reason = Describe(condition);
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public static string Describe(EventOptionConditionDefinition condition)
        {
            string requirement = condition.Kind switch
            {
                EventOptionConditionKind.MinimumCourage => $"胆识至少 {condition.Value}",
                EventOptionConditionKind.MinimumUnderstanding => $"知识至少 {condition.Value}",
                EventOptionConditionKind.MinimumStrength => $"力量至少 {condition.Value}",
                EventOptionConditionKind.MinimumAccuracy => $"技巧至少 {condition.Value}",
                EventOptionConditionKind.MinimumEvasion => $"敏捷至少 {condition.Value}",
                EventOptionConditionKind.MinimumWillpower => $"意志至少 {condition.Value}",
                EventOptionConditionKind.MaximumLuck => $"命运不高于 {condition.Value}",
                EventOptionConditionKind.HasTrait => $"拥有特性“{condition.Key}”",
                EventOptionConditionKind.HasAilment => $"拥有症状“{condition.Key}”",
                EventOptionConditionKind.MinimumResource => $"营地拥有 {condition.Key} ×{condition.Value}",
                EventOptionConditionKind.HasEquippedItem => $"装备“{condition.Key}”",
                EventOptionConditionKind.HasKeyword => $"拥有关键词“{KeywordRules.Normalize(condition.Key)}”",
                EventOptionConditionKind.HasBloodline => $"拥有血脉“{condition.DisplayName}”",
                EventOptionConditionKind.HasActiveBloodline => $"血脉“{condition.DisplayName}”已激活",
                _ => "满足未知条件"
            };
            return condition.Inverted ? $"不可满足：{requirement}" : $"需要{requirement}";
        }

        private static bool Evaluate(EventOptionConditionDefinition condition, HunterState hunter, Func<string, int> resourceResolver, IReadOnlyCollection<string> equippedItems)
        {
            switch (condition.Kind)
            {
                case EventOptionConditionKind.MinimumResource:
                    return !string.IsNullOrWhiteSpace(condition.Key) && resourceResolver != null && resourceResolver(condition.Key) >= condition.Value;
                case EventOptionConditionKind.MinimumCourage:
                    return hunter != null && hunter.Courage >= condition.Value;
                case EventOptionConditionKind.MinimumUnderstanding:
                    return hunter != null && hunter.Understanding >= condition.Value;
                case EventOptionConditionKind.MinimumStrength:
                    return hunter?.Stats != null && hunter.Stats.strength >= condition.Value;
                case EventOptionConditionKind.MinimumAccuracy:
                    return hunter?.Stats != null && hunter.Stats.accuracy >= condition.Value;
                case EventOptionConditionKind.MinimumEvasion:
                    return hunter?.Stats != null && hunter.Stats.evasion >= condition.Value;
                case EventOptionConditionKind.MinimumWillpower:
                    return hunter != null && hunter.Willpower >= condition.Value;
                case EventOptionConditionKind.MaximumLuck:
                    return hunter != null && hunter.Luck <= condition.Value;
                case EventOptionConditionKind.HasTrait:
                    return hunter?.Traits != null && hunter.Traits.Contains(condition.Key);
                case EventOptionConditionKind.HasAilment:
                    return hunter?.Ailments != null && hunter.Ailments.Contains(condition.Key);
                case EventOptionConditionKind.HasEquippedItem:
                    return Contains(equippedItems, condition.Key);
                case EventOptionConditionKind.HasBloodline:
                    return hunter != null && string.Equals(hunter.BloodlineId, condition.Key, StringComparison.Ordinal);
                case EventOptionConditionKind.HasActiveBloodline:
                    return hunter != null && hunter.IsBloodlineActivated && string.Equals(hunter.BloodlineId, condition.Key, StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        private static bool Contains(IReadOnlyCollection<string> values, string expected)
        {
            if (values == null) return false;
            foreach (string value in values)
                if (string.Equals(value, expected, StringComparison.Ordinal))
                    return true;
            return false;
        }
    }
}
