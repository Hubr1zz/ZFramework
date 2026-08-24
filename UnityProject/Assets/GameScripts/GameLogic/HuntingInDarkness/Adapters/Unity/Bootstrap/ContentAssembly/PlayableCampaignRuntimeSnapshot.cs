using System.Collections.Generic;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.Bootstrap
{
    /// <summary>内容候选发布前的兼容静态桥接快照；只恢复引用与索引，不触碰玩家实例状态。</summary>
    internal sealed class PlayableCampaignRuntimeSnapshot
    {
        private readonly PlayableHuntContentCatalog huntContent = PlayableHuntContentRuntime.Catalog;
        private readonly PlayableHuntDestinationRuntime.RuntimeState huntDestination = PlayableHuntDestinationRuntime.CaptureState();
        private readonly PlayableSettlementContentCatalog settlementContent = PlayableSettlementContentRuntime.Catalog;
        private readonly PlayableHunterCombatAdapter.RuntimeState hunterCombat = PlayableHunterCombatAdapter.CaptureState();
        private readonly PlayableSurvivalEventCatalog survivalEvents = PlayableSurvivalEventRuntime.Catalog;
        private readonly PlayablePermanentInjuryCatalog permanentInjuries = PlayablePermanentInjuryRuntime.Catalog;
        private readonly PlayableSymptomCatalog symptoms = PlayableSymptomRuntime.Catalog;
        private readonly PlayableGrowthMilestoneCatalog growthMilestones = PlayableGrowthMilestoneRuntime.Catalog;
        private readonly PlayableWeaponMasteryCatalog weaponMastery = PlayableWeaponMasteryRuntime.Catalog;
        private readonly PlayableEncounterRuntime.RuntimeState encounter = PlayableEncounterRuntime.CaptureState();
        private readonly List<ItemData> items = new(PlayableSettlementItemRegistry.Items);
        private readonly List<InventionData> inventions = new(PlayableSettlementInventionRegistry.Inventions);
        private readonly PlayableSettlementEventRegistry.RuntimeState settlementEvents = PlayableSettlementEventRegistry.CaptureState();

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
            PlayableSettlementItemRegistry.Configure(items);
            PlayableSettlementInventionRegistry.Configure(inventions);
            PlayableSettlementEventRegistry.RestoreState(settlementEvents);
            PlayableHuntDestinationRuntime.RestoreState(huntDestination);
            PlayableHuntContentRuntime.Configure(huntContent);
        }
    }
}
