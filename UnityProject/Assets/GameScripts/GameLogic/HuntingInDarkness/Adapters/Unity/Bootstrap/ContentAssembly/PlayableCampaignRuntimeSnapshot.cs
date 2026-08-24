using HuntingInDarkness.Combat;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.Bootstrap
{
    /// <summary>内容候选发布前的兼容静态桥接快照；只恢复引用与索引，不触碰玩家实例状态。</summary>
    internal sealed class PlayableCampaignRuntimeSnapshot
    {
        private readonly PlayableHuntContentCatalog huntContent = PlayableHuntContentRuntime.Catalog;
        private readonly PlayableHuntContentBundle huntBundle = PlayableHuntContentRuntime.CurrentBundle;
        private readonly PlayableHuntDestinationRuntime.RuntimeState huntDestination = PlayableHuntDestinationRuntime.CaptureState();
        private readonly PlayableSettlementContentCatalog settlementContent = PlayableSettlementContentRuntime.Catalog;
        private readonly PlayableHunterCombatAdapter.RuntimeState hunterCombat = PlayableHunterCombatAdapter.CaptureState();
        private readonly PlayableSurvivalEventCatalog survivalEvents = PlayableSurvivalEventRuntime.Catalog;
        private readonly PlayablePermanentInjuryCatalog permanentInjuries = PlayablePermanentInjuryRuntime.Catalog;
        private readonly PlayableSymptomCatalog symptoms = PlayableSymptomRuntime.Catalog;
        private readonly PlayableGrowthMilestoneCatalog growthMilestones = PlayableGrowthMilestoneRuntime.Catalog;
        private readonly PlayableWeaponMasteryCatalog weaponMastery = PlayableWeaponMasteryRuntime.Catalog;
        private readonly PlayableEncounterRuntime.RuntimeState encounter = PlayableEncounterRuntime.CaptureState();
        private readonly PlayableSettlementRegistryBundle settlementRegistryBundle = PlayableSettlementContentRuntime.CaptureLegacyRegistryBundle();

        public void Restore()
        {
            PlayableSettlementContentRuntime.ConfigureForInstallation(settlementContent);
            PlayableHunterCombatAdapter.RestoreState(hunterCombat);
            PlayableSurvivalEventRuntime.Configure(survivalEvents);
            PlayablePermanentInjuryRuntime.Configure(permanentInjuries);
            PlayableSymptomRuntime.Configure(symptoms);
            PlayableGrowthMilestoneRuntime.Configure(growthMilestones);
            PlayableWeaponMasteryRuntime.Configure(weaponMastery);
            PlayableEncounterRuntime.RestoreState(encounter);
            PlayableSettlementContentRuntime.RestoreLegacyRegistryBundle(settlementRegistryBundle);
            PlayableHuntContentRuntime.SwapBundle(huntBundle);
            PlayableHuntContentRuntime.ConfigureForInstallation(huntContent);
            PlayableHuntDestinationRuntime.RestoreState(huntDestination);
        }
    }
}
