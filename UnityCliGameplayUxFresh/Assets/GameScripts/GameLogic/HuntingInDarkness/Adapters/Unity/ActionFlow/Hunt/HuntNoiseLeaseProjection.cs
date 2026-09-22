using System;
using CardGame.ActionQueue;
using Core;
using HuntingInDarkness.Data;

namespace HuntingInDarkness.ActionFlow.Hunt
{
    public sealed class HuntNoiseLeaseProjection : IPlayableCampaignPersistentEffectProjection
    {
        private readonly IActionEnvironmentInstallerRegistry installerRegistry;
        private IDisposable installerRegistration;
        private string installedLeaseId;
        private int installedModifier;

        public HuntNoiseLeaseProjection(IActionEnvironmentInstallerRegistry installerRegistry)
        {
            this.installerRegistry = installerRegistry ?? throw new ArgumentNullException(nameof(installerRegistry));
        }

        public bool TrySynchronize(SettlementInstance settlement, out string reason)
        {
            PendingHuntNoiseLease lease = settlement?.PendingHuntNoiseLease;
            if (lease == null)
            {
                ClearRegistration();
                reason = string.Empty;
                return true;
            }
            if (IsSerializedNullPlaceholder(lease))
            {
                settlement.PendingHuntNoiseLease = null;
                ClearRegistration();
                reason = string.Empty;
                return true;
            }
            if (!HuntNoiseLeaseInstaller.TryValidate(lease, out reason))
                return false;
            if (installerRegistration != null && string.Equals(installedLeaseId, lease.LeaseId, StringComparison.Ordinal) && installedModifier == lease.NoiseModifier)
            {
                reason = string.Empty;
                return true;
            }

            IDisposable candidate;
            try
            {
                candidate = installerRegistry.Register(new HuntNoiseLeaseInstaller(lease.LeaseId, lease.NoiseModifier));
            }
            catch (Exception exception)
            {
                reason = $"狩猎风险租约投影安装失败：{exception.Message}";
                return false;
            }

            ClearRegistration();
            installerRegistration = candidate;
            installedLeaseId = lease.LeaseId;
            installedModifier = lease.NoiseModifier;
            reason = string.Empty;
            return true;
        }

        public bool TryClear(SettlementInstance settlement, out string reason)
        {
            if (settlement?.PendingHuntNoiseLease != null && !IsSerializedNullPlaceholder(settlement.PendingHuntNoiseLease) && !HuntNoiseLeaseInstaller.TryValidate(settlement.PendingHuntNoiseLease, out reason))
                return false;
            if (settlement != null)
                settlement.PendingHuntNoiseLease = null;
            ClearRegistration();
            reason = string.Empty;
            return true;
        }

        public void Dispose() => ClearRegistration();

        private void ClearRegistration()
        {
            installerRegistration?.Dispose();
            installerRegistration = null;
            installedLeaseId = null;
            installedModifier = 0;
        }

        private static bool IsSerializedNullPlaceholder(PendingHuntNoiseLease lease) => lease != null && lease.SchemaVersion == PendingHuntNoiseLease.CurrentSchemaVersion && string.IsNullOrWhiteSpace(lease.LeaseId) && string.IsNullOrWhiteSpace(lease.SourceEventId) && lease.NoiseModifier == 0;
    }

    internal sealed class HuntNoiseLeaseInstaller : IActionEnvironmentInstaller
    {
        private readonly string leaseId;
        private readonly int modifier;

        public HuntNoiseLeaseInstaller(string leaseId, int modifier)
        {
            this.leaseId = leaseId;
            this.modifier = modifier;
        }

        public bool Supports(ActionEnvironmentKind kind) => kind == ActionEnvironmentKind.Hunt;

        public void Install(IActionEnvironment environment, ActionEnvironmentInstallation installation)
        {
            if (environment == null) throw new ArgumentNullException(nameof(environment));
            if (installation == null) throw new ArgumentNullException(nameof(installation));
            installation.Add(environment.Reactors.RegisterGlobal(new HuntNoiseLeaseReactor(leaseId, modifier)));
        }

        public static bool TryValidate(PendingHuntNoiseLease lease, out string reason)
        {
            reason = string.Empty;
            if (lease == null)
            {
                reason = "狩猎风险租约不能为空。";
                return false;
            }
            string sourceEventId = lease.SourceEventId?.Trim() ?? string.Empty;
            if (lease.SchemaVersion != PendingHuntNoiseLease.CurrentSchemaVersion)
            {
                reason = "狩猎风险租约版本无效。";
                return false;
            }
            if (sourceEventId.Length == 0 || sourceEventId.Length > 64)
            {
                reason = "狩猎风险租约来源事件无效。";
                return false;
            }
            if (!string.Equals(lease.LeaseId, $"hunt-noise:{sourceEventId}", StringComparison.Ordinal))
            {
                reason = "狩猎风险租约 ID 与来源事件不匹配。";
                return false;
            }
            if (lease.NoiseModifier < 1 || lease.NoiseModifier > 10)
            {
                reason = "狩猎风险租约修正值无效。";
                return false;
            }
            return true;
        }
    }
}
