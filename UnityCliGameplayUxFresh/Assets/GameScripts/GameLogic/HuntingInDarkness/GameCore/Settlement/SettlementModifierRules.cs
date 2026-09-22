using System;
using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Settlement
{
    public readonly struct SettlementModifierProjectionChange
    {
        public SettlementModifierProjectionChange(string modifierId, InventionEffectKind kind, int previousValue, int currentValue)
        {
            ModifierId = modifierId ?? string.Empty;
            Kind = kind;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
        }

        public string ModifierId { get; }
        public InventionEffectKind Kind { get; }
        public int PreviousValue { get; }
        public int CurrentValue { get; }
    }

    public static class SettlementModifierRules
    {
        public static bool TryValidateProjection(HunterState hunter, IReadOnlyList<SettlementModifierState> modifiers, out string reason)
        {
            if (hunter == null)
            {
                reason = "猎人状态为空。";
                return false;
            }
            if (!TryBuildModifierIndex(modifiers, out _, out reason)) return false;

            var contributionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SettlementModifierContribution contribution in hunter.SettlementModifierContributions ?? new List<SettlementModifierContribution>())
            {
                string modifierId = contribution?.ModifierId?.Trim() ?? string.Empty;
                if (modifierId.Length == 0 || contribution.Kind == InventionEffectKind.None || !Enum.IsDefined(typeof(InventionEffectKind), contribution.Kind))
                {
                    reason = "猎人包含无效的营地修正贡献。";
                    return false;
                }
                if (!contributionIds.Add(modifierId))
                {
                    reason = $"猎人包含重复的营地修正贡献：{modifierId}";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        public static bool TryReconcileHunter(HunterState hunter, IReadOnlyList<SettlementModifierState> modifiers, ICollection<SettlementModifierProjectionChange> changes, out string reason)
        {
            if (!TryValidateProjection(hunter, modifiers, out reason)) return false;
            TryBuildModifierIndex(modifiers, out Dictionary<string, SettlementModifierState> modifiersById, out _);
            hunter.SettlementModifierContributions ??= new List<SettlementModifierContribution>();

            for (int index = hunter.SettlementModifierContributions.Count - 1; index >= 0; index--)
            {
                SettlementModifierContribution contribution = hunter.SettlementModifierContributions[index];
                if (modifiersById.ContainsKey(contribution.ModifierId)) continue;
                ApplyContributionDelta(hunter, contribution.ModifierId, contribution.Kind, -(long)contribution.Value, changes, out _);
                hunter.SettlementModifierContributions.RemoveAt(index);
            }

            foreach (SettlementModifierState modifier in modifiers)
            {
                SettlementModifierContribution contribution = hunter.SettlementModifierContributions.Find(candidate => candidate.ModifierId == modifier.ModifierId);
                if (contribution == null)
                {
                    if (!InventionEffectRules.IsEligible(hunter, modifier.Target)) continue;
                    ApplyContributionDelta(hunter, modifier.ModifierId, modifier.Kind, modifier.Value, changes, out int appliedValue);
                    hunter.SettlementModifierContributions.Add(new SettlementModifierContribution { ModifierId = modifier.ModifierId, Kind = modifier.Kind, Value = appliedValue });
                    continue;
                }

                if (contribution.Kind != modifier.Kind)
                {
                    ApplyContributionDelta(hunter, contribution.ModifierId, contribution.Kind, -(long)contribution.Value, changes, out _);
                    ApplyContributionDelta(hunter, modifier.ModifierId, modifier.Kind, modifier.Value, changes, out int replacementValue);
                    contribution.Kind = modifier.Kind;
                    contribution.Value = replacementValue;
                    continue;
                }

                ApplyContributionDelta(hunter, modifier.ModifierId, modifier.Kind, (long)modifier.Value - contribution.Value, changes, out int appliedDelta);
                contribution.Value = ClampToInt((long)contribution.Value + appliedDelta);
            }

            reason = string.Empty;
            return true;
        }

        public static void SeedLegacyContribution(HunterState hunter, SettlementModifierState modifier)
        {
            if (hunter == null || modifier == null || !InventionEffectRules.IsEligible(hunter, modifier.Target)) return;
            hunter.SettlementModifierContributions ??= new List<SettlementModifierContribution>();
            if (hunter.SettlementModifierContributions.Exists(candidate => candidate != null && candidate.ModifierId == modifier.ModifierId)) return;
            hunter.SettlementModifierContributions.Add(new SettlementModifierContribution { ModifierId = modifier.ModifierId, Kind = modifier.Kind, Value = modifier.Value });
        }

        private static bool TryBuildModifierIndex(IReadOnlyList<SettlementModifierState> modifiers, out Dictionary<string, SettlementModifierState> modifiersById, out string reason)
        {
            modifiersById = new Dictionary<string, SettlementModifierState>(StringComparer.Ordinal);
            foreach (SettlementModifierState modifier in modifiers ?? Array.Empty<SettlementModifierState>())
            {
                string modifierId = modifier?.ModifierId?.Trim() ?? string.Empty;
                string sourceId = modifier?.SourceId?.Trim() ?? string.Empty;
                if (modifierId.Length == 0 || sourceId.Length == 0 || modifier.Kind == InventionEffectKind.None || modifier.Value == 0 || modifier.ConfiguredValue == 0 || !Enum.IsDefined(typeof(SettlementModifierSourceKind), modifier.SourceKind) || !Enum.IsDefined(typeof(InventionEffectKind), modifier.Kind) || !Enum.IsDefined(typeof(InventionEffectTarget), modifier.Target))
                {
                    reason = "营地包含无效的持续修正来源。";
                    return false;
                }
                if (!modifiersById.TryAdd(modifierId, modifier))
                {
                    reason = $"营地包含重复的持续修正：{modifierId}";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        private static void ApplyContributionDelta(HunterState hunter, string modifierId, InventionEffectKind kind, long delta, ICollection<SettlementModifierProjectionChange> changes, out int appliedDelta)
        {
            int previousValue;
            int currentValue;
            switch (kind)
            {
                case InventionEffectKind.ModifyWillpowerMaximum:
                    previousValue = hunter.WillpowerMax;
                    currentValue = ClampToInt((long)previousValue + delta, 0);
                    hunter.WillpowerMax = currentValue;
                    hunter.Willpower = Math.Max(0, Math.Min(hunter.Willpower, hunter.WillpowerMax));
                    break;
                case InventionEffectKind.ModifyStrength:
                    hunter.Stats ??= new HunterStats();
                    previousValue = hunter.Stats.strength;
                    currentValue = ClampToInt((long)previousValue + delta);
                    hunter.Stats.strength = currentValue;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported settlement modifier kind: {kind}");
            }

            appliedDelta = ClampToInt((long)currentValue - previousValue);
            if (previousValue != currentValue)
                changes?.Add(new SettlementModifierProjectionChange(modifierId, kind, previousValue, currentValue));
        }

        private static int ClampToInt(long value, int minimum = int.MinValue)
        {
            if (value < minimum) return minimum;
            if (value > int.MaxValue) return int.MaxValue;
            return (int)value;
        }
    }
}
