using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Combat
{
    public static class PlayableWeaponMasteryRuntime
    {
        private static PlayableWeaponMasteryCatalog catalog;

        public static PlayableWeaponMasteryCatalog Catalog => catalog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            catalog = null;
        }

        public static void Configure(PlayableWeaponMasteryCatalog masteryCatalog)
        {
            catalog = masteryCatalog != null && masteryCatalog.IsConfigured ? masteryCatalog : null;
        }

        public static bool TryAward(HunterInstance hunter, string weaponName, out WeaponMasteryGainOutcome outcome)
        {
            outcome = default;
            if (hunter == null || string.IsNullOrWhiteSpace(weaponName)) return false;
            WeaponMasteryFamilyDefinition family = ResolveOrCreateFallback(weaponName);
            return WeaponMasteryRules.TryGain(hunter, family, WeaponMasteryRules.ExperiencePerBattle, out outcome);
        }

        private static WeaponMasteryFamilyDefinition ResolveOrCreateFallback(string weaponName)
        {
            if (catalog != null && catalog.TryResolve(weaponName, out WeaponMasteryFamilyDefinition family)) return family;
            return new WeaponMasteryFamilyDefinition($"weapon:{weaponName}", weaponName, new List<WeaponMasteryMilestoneDefinition>());
        }
    }
}
