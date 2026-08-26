using System.Collections.Generic;
using GameplayBase.CombatSystem;
using GameplayBase;
using HuntingInDarkness.Combat;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Settlement;

namespace HuntingInDarkness.Bootstrap
{
    /// <summary>启动期验证完成的内容引用快照；安装期间不发布事件或创建 View。</summary>
    public sealed class PlayableCampaignContentCandidate
    {
        private bool installed;
        internal PlayableSettlementContentPlan SettlementPlan { get; private set; }
        internal PlayableHuntContentBundle HuntBundle { get; private set; }

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
            if (HuntBundle == null)
            {
                reason = "狩猎路线内容计划尚未准备。";
                return false;
            }

            PlayableHuntContentRuntime.ConfigureForInstallation(DefaultHuntContent);
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

        internal void PublishHuntBindings()
        {
            PlayableHuntDestinationRuntime.Configure(HuntDestinations, DefaultHuntContent, HuntBundle);
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

        internal bool TryPrepareHuntPlans(PlayableEventTableGeneration eventGeneration, out string reason)
        {
            if (installed || HuntBundle != null || SettlementPlan == null)
            {
                reason = "狩猎路线内容候选已经准备或安装。";
                return false;
            }
            bool prepared = PlayableHuntContentBundle.TryCreate(DefaultHuntContent, Destinations, eventGeneration, SettlementPlan.RegistryBundle, out PlayableHuntContentBundle bundle, out reason);
            HuntBundle = bundle;
            return prepared;
        }

        internal void ReleaseSettlementPlan(PlayableSettlementContentPlan plan)
        {
            if (ReferenceEquals(SettlementPlan, plan)) SettlementPlan = null;
        }

        internal void ReleaseHuntPlans()
        {
            HuntBundle = null;
        }

        internal void MarkInstalled() => installed = true;

        internal bool TryValidateInstalledContent(out string reason)
        {
            if (SettlementPlan == null || !ReferenceEquals(PlayableSettlementContentRuntime.CurrentPlan, SettlementPlan))
            {
                reason = "营地内容计划尚未发布。";
                return false;
            }
            if (HuntBundle == null || !ReferenceEquals(PlayableHuntContentRuntime.CurrentBundle, HuntBundle))
            {
                reason = "狩猎内容 Bundle 尚未发布。";
                return false;
            }
            if (WorkshopContent == null)
            {
                reason = "工坊内容预检失败：工坊目录为空。";
                return false;
            }
            if (!TryValidateTraitReferences(SettlementPlan.TraitCatalog, out reason))
            {
                reason = $"特性内容预检失败：{reason}";
                return false;
            }
            if (!WorkshopContent.TryValidateAgainst(SettlementPlan.Items, SettlementPlan.Inventions, SettlementPlan.Recipes, out reason))
            {
                reason = $"工坊内容预检失败：{reason}";
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
            var huntProbe = new HuntManager(new EventSystem(new SettlementInstance(), new SystemRandomSource(1979)), 1979);
            if (!huntProbe.TryBindContent(HuntBundle.DefaultRoute, out reason))
            {
                reason = $"狩猎内容投影预检失败：{reason}";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private bool TryValidateTraitReferences(PlayableTraitCatalog traits, out string reason)
        {
            foreach (HunterGrowthMilestoneDefinition milestone in GrowthMilestones.GetDefinitions())
                if (!traits.ContainsCanonicalId(milestone.GrantedTrait))
                {
                    reason = $"成长里程碑 {milestone.Id} 引用了未知或非稳定特性 ID：{milestone.GrantedTrait}";
                    return false;
                }
            foreach (WeaponMasteryFamilyDefinition family in WeaponMastery.GetFamilies())
                foreach (WeaponMasteryMilestoneDefinition milestone in family.Milestones)
                    if (!traits.ContainsCanonicalId(milestone.GrantedTrait))
                    {
                        reason = $"武器熟练里程碑 {milestone.Id} 引用了未知或非稳定特性 ID：{milestone.GrantedTrait}";
                        return false;
                    }
            foreach (SymptomDefinition symptom in Symptoms.GetDefinitions())
            {
                string internalizedTraitId = HunterSymptomRules.GetInternalizedTraitId(symptom);
                string overcomeTraitId = HunterSymptomRules.GetOvercomeTraitId(symptom);
                if (traits.ContainsCanonicalId(internalizedTraitId) && traits.ContainsCanonicalId(overcomeTraitId)) continue;
                reason = $"症状 {symptom.Id} 缺少内化或克服特性：{internalizedTraitId}/{overcomeTraitId}";
                return false;
            }
            reason = string.Empty;
            return true;
        }
    }
}
