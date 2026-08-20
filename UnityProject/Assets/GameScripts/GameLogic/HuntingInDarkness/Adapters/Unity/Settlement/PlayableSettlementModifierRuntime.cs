using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;

namespace HuntingInDarkness.Settlement
{
    public readonly struct PlayableSettlementModifierChange
    {
        public PlayableSettlementModifierChange(int hunterId, SettlementModifierProjectionChange projection)
        {
            HunterId = hunterId;
            Projection = projection;
        }

        public int HunterId { get; }
        public SettlementModifierProjectionChange Projection { get; }
    }

    internal sealed class SettlementModifierRegistrationPlan
    {
        public SettlementModifierRegistrationPlan(List<SettlementModifierState> modifiers)
        {
            Modifiers = modifiers;
        }

        public List<SettlementModifierState> Modifiers { get; }
    }

    /// <summary>把发明的战役级持续效果映射为存档来源，并幂等投影到猎人状态。</summary>
    public static class PlayableSettlementModifierRuntime
    {
        public const int CurrentSchemaVersion = 1;

        public static bool TryCreateInventionModifiers(InventionData invention, ICollection<SettlementModifierState> result, out string reason)
        {
            if (invention == null || string.IsNullOrWhiteSpace(invention.ContentId))
            {
                reason = "持续修正缺少来源发明。";
                return false;
            }

            foreach (InventionPassiveEffect effect in invention.unlockEffects ?? new List<InventionPassiveEffect>())
            {
                if (effect == null || effect.lifetime != InventionEffectLifetime.Campaign) continue;
                string modifierId = effect.modifierId?.Trim() ?? string.Empty;
                if (modifierId.Length == 0 || effect.kind == InventionEffectKind.None || effect.value == 0 || effect.target != InventionEffectTarget.AllLivingAndFutureHunters)
                {
                    reason = $"发明 {invention.ContentId} 包含无效的战役持续效果。";
                    return false;
                }
                result.Add(new SettlementModifierState
                {
                    ModifierId = modifierId,
                    SourceKind = SettlementModifierSourceKind.Invention,
                    SourceId = invention.ContentId,
                    Kind = effect.kind,
                    Target = effect.target,
                    ConfiguredValue = effect.value,
                    Value = effect.value,
                    HasValueOverride = false
                });
            }

            reason = string.Empty;
            return true;
        }

        internal static bool TryCreateRegistrationPlan(SettlementInstance settlement, IReadOnlyList<SettlementModifierState> additions, out SettlementModifierRegistrationPlan plan, out string reason)
        {
            plan = null;
            if (settlement == null)
            {
                reason = "营地状态为空。";
                return false;
            }

            var next = CloneModifiers(settlement.ActiveModifiers);
            foreach (SettlementModifierState addition in additions ?? Array.Empty<SettlementModifierState>())
            {
                SettlementModifierState existing = next.Find(candidate => candidate.ModifierId == addition.ModifierId);
                if (existing == null)
                {
                    next.Add(CloneModifier(addition));
                    continue;
                }
                if (!HasSameIdentity(existing, addition))
                {
                    reason = $"持续修正 ID 与已有来源冲突：{addition.ModifierId}";
                    return false;
                }
                existing.ConfiguredValue = addition.ConfiguredValue;
                existing.Value = addition.Value;
            }
            next.Sort((left, right) => string.CompareOrdinal(left.ModifierId, right.ModifierId));
            if (!ValidateRoster(settlement, next, out reason)) return false;
            plan = new SettlementModifierRegistrationPlan(next);
            return true;
        }

        internal static List<PlayableSettlementModifierChange> ApplyRegistrationPlan(SettlementInstance settlement, SettlementModifierRegistrationPlan plan)
        {
            settlement.ActiveModifiers = plan.Modifiers;
            settlement.SettlementModifierSchemaVersion = CurrentSchemaVersion;
            return ReconcileRoster(settlement);
        }

        public static bool TryReconcileHunter(SettlementInstance settlement, HunterState hunter, out string reason)
        {
            if (settlement == null)
            {
                reason = "营地状态为空。";
                return false;
            }
            return SettlementModifierRules.TryReconcileHunter(hunter, settlement.ActiveModifiers, null, out reason);
        }

        public static bool Synchronize(SettlementInstance settlement, IReadOnlyList<InventionData> inventions, Action<string> reportError = null)
        {
            if (settlement == null) return false;
            if (settlement.SettlementModifierSchemaVersion > CurrentSchemaVersion) return true;
            if (!TryBuildDesiredModifiers(settlement, inventions, out List<SettlementModifierState> desired, out string reason))
            {
                reportError?.Invoke(reason);
                return false;
            }

            if (settlement.SettlementModifierSchemaVersion <= 0)
            {
                if (!ValidateRoster(settlement, desired, out reason))
                {
                    reportError?.Invoke(reason);
                    return false;
                }
                settlement.ActiveModifiers = desired;
                foreach (HunterInstance hunter in settlement.Hunters ?? new List<HunterInstance>())
                    foreach (SettlementModifierState modifier in desired)
                        SettlementModifierRules.SeedLegacyContribution(hunter, modifier);
                settlement.SettlementModifierSchemaVersion = CurrentSchemaVersion;
                return true;
            }

            List<SettlementModifierState> next = MergeWithPersistedEffectiveValues(settlement.ActiveModifiers, desired, out reason);
            if (next == null || !ValidateRoster(settlement, next, out reason))
            {
                reportError?.Invoke(reason);
                return false;
            }
            settlement.ActiveModifiers = next;
            ReconcileRoster(settlement);
            settlement.SettlementModifierSchemaVersion = CurrentSchemaVersion;
            return true;
        }

        private static bool TryBuildDesiredModifiers(SettlementInstance settlement, IReadOnlyList<InventionData> inventions, out List<SettlementModifierState> desired, out string reason)
        {
            desired = new List<SettlementModifierState>();
            foreach (InventionData invention in inventions ?? Array.Empty<InventionData>())
            {
                if (invention == null || !settlement.IsInventionUnlocked(invention.ContentId)) continue;
                if (!TryCreateInventionModifiers(invention, desired, out reason)) return false;
            }
            desired.Sort((left, right) => string.CompareOrdinal(left.ModifierId, right.ModifierId));
            var probe = new HunterState();
            if (!SettlementModifierRules.TryValidateProjection(probe, desired, out reason)) return false;
            return true;
        }

        private static List<SettlementModifierState> MergeWithPersistedEffectiveValues(IReadOnlyList<SettlementModifierState> persisted, IReadOnlyList<SettlementModifierState> desired, out string reason)
        {
            var persistedById = new Dictionary<string, SettlementModifierState>(StringComparer.Ordinal);
            foreach (SettlementModifierState modifier in persisted ?? Array.Empty<SettlementModifierState>())
            {
                string modifierId = modifier?.ModifierId?.Trim() ?? string.Empty;
                if (modifierId.Length == 0 || !persistedById.TryAdd(modifierId, modifier))
                {
                    reason = "存档包含重复或空白的持续修正。";
                    return null;
                }
            }

            var next = new List<SettlementModifierState>();
            foreach (SettlementModifierState configured in desired)
            {
                SettlementModifierState merged = CloneModifier(configured);
                if (persistedById.TryGetValue(configured.ModifierId, out SettlementModifierState existing))
                {
                    if (!HasSameIdentity(existing, configured))
                    {
                        reason = $"持续修正 ID 与存档来源冲突：{configured.ModifierId}";
                        return null;
                    }
                    merged.Value = existing.HasValueOverride ? existing.Value : configured.Value;
                    merged.HasValueOverride = existing.HasValueOverride;
                }
                next.Add(merged);
            }
            next.Sort((left, right) => string.CompareOrdinal(left.ModifierId, right.ModifierId));
            reason = string.Empty;
            return next;
        }

        private static bool ValidateRoster(SettlementInstance settlement, IReadOnlyList<SettlementModifierState> modifiers, out string reason)
        {
            if (!SettlementModifierRules.TryValidateProjection(new HunterState(), modifiers, out reason)) return false;
            foreach (HunterInstance hunter in settlement.Hunters ?? new List<HunterInstance>())
                if (!SettlementModifierRules.TryValidateProjection(hunter, modifiers, out reason))
                    return false;
            reason = string.Empty;
            return true;
        }

        private static List<PlayableSettlementModifierChange> ReconcileRoster(SettlementInstance settlement)
        {
            var result = new List<PlayableSettlementModifierChange>();
            var projectionChanges = new List<SettlementModifierProjectionChange>();
            foreach (HunterInstance hunter in settlement.Hunters ?? new List<HunterInstance>())
            {
                projectionChanges.Clear();
                if (!SettlementModifierRules.TryReconcileHunter(hunter, settlement.ActiveModifiers, projectionChanges, out string reason))
                    throw new InvalidOperationException(reason);
                foreach (SettlementModifierProjectionChange change in projectionChanges)
                    result.Add(new PlayableSettlementModifierChange(hunter.InstanceId, change));
            }
            return result;
        }

        private static List<SettlementModifierState> CloneModifiers(IReadOnlyList<SettlementModifierState> modifiers)
        {
            var result = new List<SettlementModifierState>();
            foreach (SettlementModifierState modifier in modifiers ?? Array.Empty<SettlementModifierState>())
                result.Add(CloneModifier(modifier));
            return result;
        }

        private static SettlementModifierState CloneModifier(SettlementModifierState source)
        {
            return new SettlementModifierState
            {
                ModifierId = source?.ModifierId?.Trim() ?? string.Empty,
                SourceKind = source?.SourceKind ?? SettlementModifierSourceKind.Invention,
                SourceId = source?.SourceId?.Trim() ?? string.Empty,
                Kind = source?.Kind ?? InventionEffectKind.None,
                Target = source?.Target ?? InventionEffectTarget.AvailableHunters,
                ConfiguredValue = source?.ConfiguredValue ?? 0,
                Value = source?.Value ?? 0,
                HasValueOverride = source?.HasValueOverride ?? false
            };
        }

        private static bool HasSameIdentity(SettlementModifierState left, SettlementModifierState right)
        {
            return left.SourceKind == right.SourceKind && left.SourceId == right.SourceId && left.Kind == right.Kind && left.Target == right.Target;
        }
    }
}
