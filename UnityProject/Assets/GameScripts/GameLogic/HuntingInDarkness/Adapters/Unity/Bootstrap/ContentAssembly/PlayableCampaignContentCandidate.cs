using System.Collections.Generic;
using GameplayBase.CombatSystem;
using GameplayBase;
using HuntingInDarkness.Combat;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.Data;

namespace HuntingInDarkness.Bootstrap
{
    /// <summary>启动期验证完成的内容引用快照；安装期间不发布事件或创建 View。</summary>
    public sealed class PlayableCampaignContentCandidate
    {
        private bool installed;
        internal PlayableSettlementContentPlan SettlementPlan { get; private set; }

        internal PlayableCampaignContentCandidate(PlayableBootstrapSettings settings)
        {
            DefaultBattleSetup = settings.CreateBattleSetup();
            InitialPhase = settings.InitialPhase;
            CellSize = settings.CellSize;
            DefaultHuntContent = settings.HuntContent;
            HuntDestinations = settings.HuntDestinations;
            Destinations = new List<PlayableHuntDestination>(settings.HuntDestinations.Destinations);
            SettlementContent = settings.SettlementContent;
            WorkshopContent = settings.WorkshopContent;
            CombatEquipment = settings.CombatEquipment;
            SurvivalEvents = settings.SurvivalEvents;
            PermanentInjuries = settings.PermanentInjuries;
            Symptoms = settings.Symptoms;
            GrowthMilestones = settings.GrowthMilestones;
            WeaponMastery = settings.WeaponMastery;
            EncounterCatalog = settings.EncounterCatalog;
            DefaultEncounterId = settings.DefaultEncounterId;
        }

        public BattleSetup DefaultBattleSetup { get; }
        public GamePhase InitialPhase { get; }
        public float CellSize { get; }
        public PlayableHuntContentCatalog DefaultHuntContent { get; }
        public PlayableHuntDestinationCatalog HuntDestinations { get; }
        public IReadOnlyList<PlayableHuntDestination> Destinations { get; }
        public PlayableSettlementContentCatalog SettlementContent { get; }
        public PlayableWorkshopCatalog WorkshopContent { get; }
        public PlayableCombatEquipmentCatalog CombatEquipment { get; }
        public PlayableSurvivalEventCatalog SurvivalEvents { get; }
        public PlayablePermanentInjuryCatalog PermanentInjuries { get; }
        public PlayableSymptomCatalog Symptoms { get; }
        public PlayableGrowthMilestoneCatalog GrowthMilestones { get; }
        public PlayableWeaponMasteryCatalog WeaponMastery { get; }
        public PlayableEncounterCatalog EncounterCatalog { get; }
        public string DefaultEncounterId { get; }

        internal bool TryInstallBindings(out string reason)
        {
            if (installed)
            {
                reason = "战役内容候选已经安装。";
                return false;
            }
            if (DefaultBattleSetup == null)
            {
                reason = "默认遭遇配置为空。";
                return false;
            }

            PlayableHuntContentRuntime.Configure(DefaultHuntContent);
            PlayableHuntDestinationRuntime.Configure(HuntDestinations, DefaultHuntContent);
            PlayableSettlementContentRuntime.ConfigureForInstallation(SettlementContent);
            PlayableHunterCombatAdapter.Configure(CombatEquipment);
            PlayableSurvivalEventRuntime.Configure(SurvivalEvents);
            PlayablePermanentInjuryRuntime.Configure(PermanentInjuries);
            PlayableSymptomRuntime.Configure(Symptoms);
            PlayableGrowthMilestoneRuntime.Configure(GrowthMilestones);
            PlayableWeaponMasteryRuntime.Configure(WeaponMastery);
            PlayableEncounterRuntime.Configure(EncounterCatalog, DefaultEncounterId, DefaultBattleSetup);
            reason = string.Empty;
            return true;
        }

        internal bool TryPrepareSettlementPlan(PlayableEventTableGeneration eventGeneration, out string reason)
        {
            if (installed || SettlementPlan != null)
            {
                reason = "营地内容候选已经准备或安装。";
                return false;
            }
            bool prepared = SettlementContent.TryPreparePlan(eventGeneration, out PlayableSettlementContentPlan plan, out reason);
            SettlementPlan = plan;
            return prepared;
        }

        internal void ReleaseSettlementPlan(PlayableSettlementContentPlan plan)
        {
            if (ReferenceEquals(SettlementPlan, plan)) SettlementPlan = null;
        }

        internal void MarkInstalled() => installed = true;

        internal bool TryValidateInstalledContent(out string reason)
        {
            if (SettlementPlan == null || !ReferenceEquals(PlayableSettlementContentRuntime.CurrentPlan, SettlementPlan))
            {
                reason = "营地内容计划尚未发布。";
                return false;
            }
            var settlementProbe = new SettlementManager(1979);
            if (!SettlementPlan.TryApplyTo(settlementProbe, out reason))
            {
                reason = $"营地内容投影预检失败：{reason}";
                return false;
            }
            if (settlementProbe.Data.Hunters.Count == 0)
            {
                reason = "营地内容预检没有产生可用的初始猎人。";
                return false;
            }
            reason = string.Empty;
            return true;
        }
    }
}
