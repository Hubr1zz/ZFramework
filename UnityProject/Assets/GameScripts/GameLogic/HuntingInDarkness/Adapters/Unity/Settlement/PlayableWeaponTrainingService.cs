using Core;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;

namespace HuntingInDarkness.Settlement
{
    public static class PlayableWeaponTrainingService
    {
        public static bool CanTrain(SettlementInstance settlement, HunterInstance hunter, string masteryId, out string reason)
        {
            PlayableWeaponMasteryCatalog catalog = PlayableWeaponMasteryRuntime.Catalog;
            if (settlement == null || hunter == null || catalog == null || !catalog.TryGetFamily(masteryId, out _))
            {
                reason = "训练内容尚未配置";
                return false;
            }
            return WeaponTrainingRules.CanTrain(hunter.IsAvailable && !hunter.IsDead, settlement.IsInventionUnlocked(catalog.TrainingInventionId), settlement.GetResource(catalog.TrainingCostItem), catalog.TrainingCost, masteryId, catalog.TrainingExperience, out reason);
        }

        public static bool TryTrain(SettlementInstance settlement, HunterInstance hunter, string masteryId, out WeaponMasteryGainOutcome outcome, out string reason)
        {
            outcome = default;
            if (!CanTrain(settlement, hunter, masteryId, out reason)) return false;
            PlayableWeaponMasteryCatalog catalog = PlayableWeaponMasteryRuntime.Catalog;
            if (!catalog.TryGetFamily(masteryId, out WeaponMasteryFamilyDefinition family) || !settlement.SpendResource(catalog.TrainingCostItem, catalog.TrainingCost))
            {
                reason = "训练提交失败";
                return false;
            }
            if (!WeaponMasteryRules.TryGain(hunter, family, catalog.TrainingExperience, out outcome))
            {
                settlement.AddResource(catalog.TrainingCostItem, catalog.TrainingCost);
                reason = "熟练度已达到上限";
                return false;
            }
            EventBus.Publish(new WeaponMasteryChangedEvent
            {
                HunterId = hunter.InstanceId,
                HunterName = hunter.Name,
                WeaponName = family.DisplayName,
                MasteryId = outcome.MasteryId,
                MasteryName = outcome.MasteryName,
                OldValue = outcome.OldValue,
                NewValue = outcome.NewValue,
                ReachedMilestoneNames = new System.Collections.Generic.List<string>(outcome.ReachedMilestoneNames).ToArray(),
                Source = WeaponMasteryGainSource.Training
            });
            reason = string.Empty;
            return true;
        }
    }
}
