using System;
using System.Collections.Generic;
using Core;
using GameplayBase.CombatSystem;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;

namespace HuntingInDarkness.Combat
{
    /// <summary>
    /// 将决战中的有效伤害记录转换为营地猎人熟练度。
    /// 每场胜利最多增加一次，且只有第一武器格（空手时为拳头）有资格。
    /// </summary>
    public sealed class PlayableWeaponMasteryTracker : IDisposable
    {
        private readonly Dictionary<int, HunterInstance> huntersByCharacterId = new();
        private readonly Dictionary<int, string> primaryWeaponByCharacterId = new();
        private readonly Dictionary<int, HashSet<string>> effectiveWeaponsByCharacterId = new();
        private bool subscribed;

        public void Bind(IReadOnlyList<HunterInstance> hunters, IReadOnlyList<CharacterRuntimeData> characters)
        {
            ClearBattle();
            EnsureSubscribed();
            if (hunters == null || characters == null) return;

            var aliveHunters = new List<HunterInstance>();
            foreach (HunterInstance hunter in hunters)
                if (hunter != null && hunter.IsAlive)
                    aliveHunters.Add(hunter);

            int count = Math.Min(aliveHunters.Count, characters.Count);
            for (int index = 0; index < count; index++)
            {
                CharacterRuntimeData character = characters[index];
                if (character == null) continue;

                huntersByCharacterId[character.Id] = aliveHunters[index];
                List<SO.Character.WeaponData> weapons = character.GetAvailableWeapons();
                if (weapons.Count > 0 && weapons[0] != null)
                    primaryWeaponByCharacterId[character.Id] = weapons[0].weaponName;
            }
        }

        public int SettleVictory()
        {
            int awardCount = 0;
            foreach (var pair in huntersByCharacterId)
            {
                int characterId = pair.Key;
                HunterInstance hunter = pair.Value;
                if (!primaryWeaponByCharacterId.TryGetValue(characterId, out string primaryWeapon)) continue;
                if (!effectiveWeaponsByCharacterId.TryGetValue(characterId, out HashSet<string> effectiveWeapons)) continue;
                if (!WeaponMasteryRules.CanGain(hunter.IsAlive, primaryWeapon, effectiveWeapons)) continue;

                if (!PlayableWeaponMasteryRuntime.TryAward(hunter, primaryWeapon, out WeaponMasteryGainOutcome outcome)) continue;
                awardCount++;
                EventBus.Publish(new WeaponMasteryChangedEvent
                {
                    HunterId = hunter.InstanceId,
                    HunterName = hunter.Name,
                    WeaponName = primaryWeapon,
                    MasteryId = outcome.MasteryId,
                    MasteryName = outcome.MasteryName,
                    OldValue = outcome.OldValue,
                    NewValue = outcome.NewValue,
                    ReachedMilestoneNames = new List<string>(outcome.ReachedMilestoneNames).ToArray(),
                    Source = WeaponMasteryGainSource.Combat
                });
            }

            ClearBattle();
            return awardCount;
        }

        public void Dispose()
        {
            if (subscribed)
                EventBus.Unsubscribe<EffectiveWeaponDamageEvent>(OnEffectiveWeaponDamage);
            subscribed = false;
            ClearBattle();
        }

        private void EnsureSubscribed()
        {
            if (subscribed) return;
            EventBus.Subscribe<EffectiveWeaponDamageEvent>(OnEffectiveWeaponDamage);
            subscribed = true;
        }

        private void OnEffectiveWeaponDamage(EffectiveWeaponDamageEvent evt)
        {
            if (!huntersByCharacterId.ContainsKey(evt.CharacterId) || string.IsNullOrWhiteSpace(evt.WeaponName)) return;
            if (!effectiveWeaponsByCharacterId.TryGetValue(evt.CharacterId, out HashSet<string> weapons))
            {
                weapons = new HashSet<string>(StringComparer.Ordinal);
                effectiveWeaponsByCharacterId[evt.CharacterId] = weapons;
            }
            weapons.Add(evt.WeaponName);
        }

        private void ClearBattle()
        {
            huntersByCharacterId.Clear();
            primaryWeaponByCharacterId.Clear();
            effectiveWeaponsByCharacterId.Clear();
        }
    }
}
