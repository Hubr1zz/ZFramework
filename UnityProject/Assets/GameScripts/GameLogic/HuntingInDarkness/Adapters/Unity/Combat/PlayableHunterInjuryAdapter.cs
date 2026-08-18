using System;
using System.Collections.Generic;
using GameplayBase.CombatSystem;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunters;

namespace HuntingInDarkness.Combat
{
    /// <summary>在营地存档模型与单场战斗的部位伤势模型之间投影可持久化状态。</summary>
    public static class PlayableHunterInjuryAdapter
    {
        private static readonly IRandomSource restoreRandom = new SystemRandomSource(1);
        private static readonly IArmorMitigationRule restoreArmorRule = new RestoreHealthArmorRule();

        public static void Apply(HunterInstance hunter, CharacterCombatStats combatStats)
        {
            if (hunter == null || combatStats == null) return;

            var maximum = hunter.MaxHP ?? new HuntingInDarkness.GameCore.Settlement.HunterHitPoints();
            var current = hunter.HP ?? new HuntingInDarkness.GameCore.Settlement.HunterHitPoints();
            GetArmor(hunter, out int headArmor, out int torsoArmor, out int armsArmor, out int legsArmor);
            var profile = new HunterInjuryProfile(
                new HunterBodyPartDefinition(HunterBodyPart.Head, maximum.head, headArmor),
                new HunterBodyPartDefinition(HunterBodyPart.Torso, maximum.body, torsoArmor),
                new HunterBodyPartDefinition(HunterBodyPart.Arms, maximum.arms, armsArmor),
                new HunterBodyPartDefinition(HunterBodyPart.Legs, maximum.legs, legsArmor));
            combatStats.InitializeInjuryState(profile, CreateDeathDeck(hunter));
            RestorePermanentInjuries(hunter, combatStats.InjuryState);
            combatStats.AddPermanentWounds(combatStats.InjuryState.PermanentInjuries.Count);
            RestorePart(combatStats, HunterBodyPart.Head, maximum.head, current.head);
            RestorePart(combatStats, HunterBodyPart.Torso, maximum.body, current.body);
            RestorePart(combatStats, HunterBodyPart.Arms, maximum.arms, current.arms);
            RestorePart(combatStats, HunterBodyPart.Legs, maximum.legs, current.legs);
        }

        public static void Sync(HunterInstance hunter, CharacterCombatStats combatStats)
        {
            if (hunter == null || combatStats == null) return;

            HunterInjuryState state = combatStats.InjuryState;
            hunter.HP ??= new HuntingInDarkness.GameCore.Settlement.HunterHitPoints();
            hunter.HP.head = state.GetPart(HunterBodyPart.Head).CurrentHealth;
            hunter.HP.body = state.GetPart(HunterBodyPart.Torso).CurrentHealth;
            hunter.HP.arms = state.GetPart(HunterBodyPart.Arms).CurrentHealth;
            hunter.HP.legs = state.GetPart(HunterBodyPart.Legs).CurrentHealth;
            hunter.SurvivalCards = state.DeathDeck.SurvivalCardCount;
            hunter.DeathCards = state.DeathDeck.DeathCardCount;
            hunter.PermConditions ??= new List<string>();
            hunter.PermanentInjuryIds ??= new List<string>();
            foreach (PermanentInjury injury in state.PermanentInjuries)
            {
                if (!string.IsNullOrWhiteSpace(injury.Id) && !hunter.PermanentInjuryIds.Contains(injury.Id))
                {
                    hunter.PermanentInjuryIds.Add(injury.Id);
                    ApplyStatModifiers(hunter, injury.StatModifiers);
                }
                if (!string.IsNullOrWhiteSpace(injury.DisplayName) && !hunter.PermConditions.Contains(injury.DisplayName))
                    hunter.PermConditions.Add(injury.DisplayName);
            }
        }

        private static DeathDeck CreateDeathDeck(HunterInstance hunter)
        {
            int survivalCount = Math.Max(0, hunter.SurvivalCards);
            int deathCount = Math.Max(0, hunter.DeathCards);
            if (survivalCount + deathCount == 0) survivalCount = 1;

            var cards = new List<DeathCardType>(survivalCount + deathCount);
            for (int i = 0; i < survivalCount; i++) cards.Add(DeathCardType.Survive);
            for (int i = 0; i < deathCount; i++) cards.Add(DeathCardType.Death);
            return new DeathDeck(cards);
        }

        private static void RestorePart(CharacterCombatStats combatStats, HunterBodyPart bodyPart, int maximum, int current)
        {
            int damage = Math.Max(0, Math.Max(1, maximum) - Math.Max(0, current));
            if (damage > 0)
                combatStats.ApplyDamage(bodyPart, damage, restoreRandom, restoreArmorRule);
        }

        private static void RestorePermanentInjuries(HunterInstance hunter, HunterInjuryState state)
        {
            if (hunter.PermanentInjuryIds == null || PlayablePermanentInjuryRuntime.Catalog == null)
                return;
            foreach (string injuryId in hunter.PermanentInjuryIds)
                if (PlayablePermanentInjuryRuntime.Catalog.TryGet(injuryId, out PermanentInjury injury))
                    state.AddPermanentInjury(injury);
        }

        private static void ApplyStatModifiers(HunterInstance hunter, PermanentInjuryStatModifiers modifiers)
        {
            hunter.Stats ??= new HuntingInDarkness.GameCore.Settlement.HunterStats();
            hunter.Stats.strength = AddClamped(hunter.Stats.strength, modifiers.Strength, 0);
            hunter.Stats.accuracy = AddClamped(hunter.Stats.accuracy, modifiers.Accuracy, 0);
            hunter.Stats.evasion = AddClamped(hunter.Stats.evasion, modifiers.Evasion, 0);
            hunter.Stats.movement = AddClamped(hunter.Stats.movement, modifiers.Movement, 1);
        }

        private static int AddClamped(int current, int modifier, int minimum) => (int)Math.Max(minimum, Math.Min(int.MaxValue, (long)current + modifier));

        private static void GetArmor(HunterInstance hunter, out int head, out int torso, out int arms, out int legs)
        {
            head = 0;
            torso = 0;
            arms = 0;
            legs = 0;
            if (hunter.Equipment == null) return;

            foreach (ItemInstance item in hunter.Equipment)
            {
                if (item?.Data?.itemType != ItemType.Armor) continue;
                ArmorStats armor = item?.Data?.armorStats;
                if (armor == null) continue;
                head += Math.Max(0, armor.armorHead);
                torso += Math.Max(0, armor.armorBody);
                arms += Math.Max(0, armor.armorArms);
                legs += Math.Max(0, armor.armorLegs);
            }
        }

        private sealed class RestoreHealthArmorRule : IArmorMitigationRule
        {
            public int GetDamageAfterArmor(int incomingDamage, int armor) => Math.Max(0, incomingDamage);
        }
    }
}
