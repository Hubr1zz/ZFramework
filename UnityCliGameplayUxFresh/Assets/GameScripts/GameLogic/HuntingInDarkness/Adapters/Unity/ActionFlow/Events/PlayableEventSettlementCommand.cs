using System;
using Core;
using HuntingInDarkness.Data;

namespace HuntingInDarkness.ActionFlow.Events
{
    public readonly struct PlayableHuntNoiseLeaseChange
    {
        public PlayableHuntNoiseLeaseChange(string leaseId, int noiseModifier, bool changed)
        {
            LeaseId = leaseId ?? string.Empty;
            NoiseModifier = noiseModifier;
            Changed = changed;
        }

        public string LeaseId { get; }
        public int NoiseModifier { get; }
        public bool Changed { get; }
    }

    public interface IPlayableEventSettlementCommand
    {
        bool CanApply(EventEffect effect, out string reason);
        bool TryApply(EventEffect effect, out PlayableHuntNoiseLeaseChange change, out string reason);
    }

    public sealed class SettlementHuntNoiseLeaseCommand : IPlayableEventSettlementCommand
    {
        private readonly SettlementInstance settlement;
        private readonly IPlayableCampaignPersistentEffectProjection persistentEffectProjection;

        public SettlementHuntNoiseLeaseCommand(SettlementInstance settlement, IPlayableCampaignPersistentEffectProjection persistentEffectProjection = null)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.persistentEffectProjection = persistentEffectProjection;
        }

        public bool TryApply(EventEffect effect, out PlayableHuntNoiseLeaseChange change, out string reason)
        {
            change = default;
            reason = string.Empty;
            if (effect == null || effect.effectType != EventEffectType.CreateHuntNoiseLease)
            {
                reason = "风险租约效果无效。";
                return false;
            }
            string sourceEventId = effect.targetName?.Trim() ?? string.Empty;
            if (sourceEventId.Length == 0 || sourceEventId.Length > 64 || effect.value < 1 || effect.value > 10 || !string.IsNullOrWhiteSpace(effect.bodyPart))
            {
                reason = "风险租约参数无效。";
                return false;
            }

            string leaseId = $"hunt-noise:{sourceEventId}";
            PendingHuntNoiseLease pending = settlement.PendingHuntNoiseLease;
            if (pending != null)
            {
                if (!string.Equals(pending.LeaseId?.Trim(), leaseId, StringComparison.Ordinal) || pending.NoiseModifier != effect.value)
                {
                    reason = "已有另一份待消费的狩猎风险租约。";
                    return false;
                }
                if (persistentEffectProjection != null && !persistentEffectProjection.TrySynchronize(settlement, out reason))
                    return false;
                change = new PlayableHuntNoiseLeaseChange(leaseId, pending.NoiseModifier, false);
                return true;
            }

            PendingHuntNoiseLease previous = settlement.PendingHuntNoiseLease;
            settlement.PendingHuntNoiseLease = new PendingHuntNoiseLease
            {
                LeaseId = leaseId,
                SourceEventId = sourceEventId,
                NoiseModifier = effect.value
            };
            if (persistentEffectProjection != null && !persistentEffectProjection.TrySynchronize(settlement, out reason))
            {
                settlement.PendingHuntNoiseLease = previous;
                persistentEffectProjection.TrySynchronize(settlement, out _);
                return false;
            }
            change = new PlayableHuntNoiseLeaseChange(leaseId, effect.value, true);
            return true;
        }

        public bool CanApply(EventEffect effect, out string reason)
        {
            reason = string.Empty;
            if (effect == null || effect.effectType != EventEffectType.CreateHuntNoiseLease)
            {
                reason = "风险租约效果无效。";
                return false;
            }
            string sourceEventId = effect.targetName?.Trim() ?? string.Empty;
            if (sourceEventId.Length == 0 || sourceEventId.Length > 64 || effect.value < 1 || effect.value > 10 || !string.IsNullOrWhiteSpace(effect.bodyPart))
            {
                reason = "风险租约参数无效。";
                return false;
            }
            PendingHuntNoiseLease pending = settlement.PendingHuntNoiseLease;
            if (pending == null) return true;
            string leaseId = $"hunt-noise:{sourceEventId}";
            if (string.Equals(pending.LeaseId?.Trim(), leaseId, StringComparison.Ordinal) && pending.NoiseModifier == effect.value) return true;
            reason = "已有另一份待消费的狩猎风险租约。";
            return false;
        }
    }
}
