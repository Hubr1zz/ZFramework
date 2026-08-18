using System;
using System.Collections.Generic;
using Core;
using GameplayBase;
using GameplayBase.CombatSystem;
using HuntingInDarkness.Data;
using SO.Character;
using UnityEngine;

namespace HuntingInDarkness.Combat
{
    /// <summary>把营地猎人状态投影到现有决战运行时，不让两套数据模型直接互相依赖。</summary>
    public static class PlayableHunterCombatAdapter
    {
        private static readonly Dictionary<ItemData, WeaponData> runtimeWeapons = new();
        private static readonly Dictionary<WeaponData, CombatWeaponProfile> weaponProfiles = new();
        private static PlayableCombatEquipmentCatalog catalog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            runtimeWeapons.Clear();
            weaponProfiles.Clear();
            catalog = null;
        }

        public static void Configure(PlayableCombatEquipmentCatalog equipmentCatalog)
        {
            catalog = equipmentCatalog;
            runtimeWeapons.Clear();
            weaponProfiles.Clear();
        }

        public static CombatRosterBindingResult Apply(IReadOnlyList<HunterInstance> hunters, IReadOnlyList<CharacterRuntimeData> characters, IReadOnlyDictionary<int, UI.CharacterEntity> characterViews, TimelineManager timeline)
        {
            if (characters == null) return new CombatRosterBindingResult(0, 0);

            var deployableHunters = new List<HunterInstance>();
            if (hunters != null)
                foreach (var hunter in hunters)
                    if (hunter?.IsAvailable == true)
                        deployableHunters.Add(hunter);

            bool hasActiveRoster = deployableHunters.Count > 0;
            if (!hasActiveRoster)
            {
                foreach (var character in characters)
                {
                    if (character == null) continue;
                    character.SetCombatActive(true);
                    if (characterViews != null && characterViews.TryGetValue(character.Id, out var view) && view != null)
                        view.gameObject.SetActive(true);
                }
                return new CombatRosterBindingResult(0, 0);
            }

            int boundCount = Math.Min(deployableHunters.Count, characters.Count);
            for (int index = 0; index < characters.Count; index++)
            {
                var character = characters[index];
                if (character == null) continue;

                bool isActive = index < boundCount;
                character.SetCombatActive(isActive);
                if (characterViews != null && characterViews.TryGetValue(character.Id, out var view) && view != null)
                    view.gameObject.SetActive(isActive);
                if (!isActive) continue;

                BindHunter(deployableHunters[index], character, timeline);
            }

            return new CombatRosterBindingResult(deployableHunters.Count, boundCount);
        }

        public static IReadOnlyList<ICharacterState> FilterActiveCharacters(IReadOnlyList<CharacterRuntimeData> characters)
        {
            if (characters == null) return Array.Empty<ICharacterState>();

            var result = new List<ICharacterState>(characters.Count);
            foreach (var character in characters)
                if (character?.IsCombatActive == true)
                    result.Add(character);
            return result;
        }

        public static bool IsCharacterActive(CharacterRuntimeData character) => character?.IsCombatActive == true;

        public static void DeactivateCharacter(CharacterRuntimeData character) => character?.SetCombatActive(false);

        public static bool TryGetWeaponProfile(WeaponData weapon, out CombatWeaponProfile profile)
        {
            if (weapon != null && weaponProfiles.TryGetValue(weapon, out profile)) return true;
            profile = default;
            return false;
        }

        public static bool IsWithinRange(WeaponData weapon, int distance)
        {
            return !TryGetWeaponProfile(weapon, out var profile) || distance <= profile.Range;
        }

        public static void ActivateWeapon(CharacterRuntimeData character, WeaponData weapon)
        {
            if (character == null || weapon == null) return;

            character.EquippedWeapon = weapon;
            if (character.CombatStats == null) return;
            character.CombatStats.Speed = TryGetWeaponProfile(weapon, out var profile) ? profile.Speed : Math.Max(1, weapon.speedBonus);
        }

        private static void BindHunter(HunterInstance hunter, CharacterRuntimeData character, TimelineManager timeline)
        {
            character.Name = hunter.Name;
            character.Willpower = Math.Max(0, hunter.Willpower);
            character.CombatStats = new CharacterCombatStats
            {
                Strength = Math.Max(0, hunter.Stats?.strength ?? 0),
                Evasion = Math.Max(0, hunter.Stats?.evasion ?? 0)
            };
            PlayableHunterInjuryAdapter.Apply(hunter, character.CombatStats);

            var weapons = GetHunterWeapons(hunter);
            if (weapons.Count == 0 && catalog?.UnarmedWeapon != null)
            {
                weapons.Add(catalog.UnarmedWeapon);
                weaponProfiles[catalog.UnarmedWeapon] = new CombatWeaponProfile(1, 0, 1, string.Empty);
            }
            character.SetAvailableWeapons(weapons);
            ActivateWeapon(character, weapons.Count > 0 ? weapons[0] : null);

            timeline?.RegisterCharacter(character.Id, character.Willpower);
        }

        private static List<WeaponData> GetHunterWeapons(HunterInstance hunter)
        {
            var weapons = new List<WeaponData>(2);
            if (hunter.Equipment == null) return weapons;

            foreach (var item in hunter.Equipment)
            {
                if (item?.Data?.itemType != ItemType.Weapon) continue;
                weapons.Add(GetRuntimeWeapon(item.Data));
                if (weapons.Count == 2) break;
            }
            return weapons;
        }

        private static WeaponData GetRuntimeWeapon(ItemData item)
        {
            if (runtimeWeapons.TryGetValue(item, out var weapon) && weapon != null) return weapon;

            weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.name = $"RuntimeWeapon_{item.name}";
            weapon.hideFlags = HideFlags.HideAndDontSave;
            weapon.weaponName = item.itemName;
            weapon.strengthBonus = Math.Max(0, item.weaponStats?.power ?? 0);
            weapon.speedBonus = Math.Max(1, item.weaponStats?.speed ?? 1);
            runtimeWeapons[item] = weapon;
            weaponProfiles[weapon] = new CombatWeaponProfile(Math.Max(1, item.weaponStats?.speed ?? 1), item.weaponStats?.accuracy ?? 0, Math.Max(1, item.weaponStats?.range ?? 1), item.weaponStats?.specialRule ?? string.Empty);
            return weapon;
        }
    }

    public readonly struct CombatWeaponProfile
    {
        public int Speed { get; }
        public int Accuracy { get; }
        public int Range { get; }
        public string SpecialRule { get; }

        public CombatWeaponProfile(int speed, int accuracy, int range, string specialRule)
        {
            Speed = speed;
            Accuracy = accuracy;
            Range = range;
            SpecialRule = specialRule;
        }
    }

    public readonly struct CombatRosterBindingResult
    {
        public int RequestedHunterCount { get; }
        public int BoundHunterCount { get; }
        public bool IsComplete => RequestedHunterCount == BoundHunterCount;

        public CombatRosterBindingResult(int requestedHunterCount, int boundHunterCount)
        {
            RequestedHunterCount = requestedHunterCount;
            BoundHunterCount = boundHunterCount;
        }
    }
}
