using System;

namespace HuntingInDarkness.GameCore.Combat
{
    public enum AttackOutcome
    {
        Success,
        Failure,
        Aborted
    }

    [Serializable]
    public class CombatantStats
    {
        public int Strength;
        public int Speed;
        public int Evasion;
        // Legacy serialized counters. Kept for asset compatibility; hunter death is owned by
        // HunterInjuryState and must never be inferred from these totals.
        public int PermanentWounds;
        public int TemporaryWounds;

        public void AddTemporaryWounds(int count = 1)
        {
            TemporaryWounds += count;
        }

        public void AddPermanentWounds(int count = 1)
        {
            PermanentWounds += count;
        }
    }

    public sealed class WeaponProfile
    {
        public string Id { get; }
        public int StrengthBonus { get; }

        public WeaponProfile(string id, int strengthBonus)
        {
            Id = id ?? string.Empty;
            StrengthBonus = strengthBonus;
        }
    }

    public sealed class HitLocationDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int Toughness { get; }
        public int DrawWeight { get; }
        public int MaxHp { get; }
        public bool IsPersistent { get; }
        public int PreviousSuccessToughnessModifier { get; }

        public HitLocationDefinition(
            string id,
            string name,
            string description,
            int toughness,
            int drawWeight,
            int maxHp,
            bool isPersistent = false,
            int previousSuccessToughnessModifier = 0)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            Toughness = Math.Max(0, toughness);
            DrawWeight = Math.Max(0, drawWeight);
            MaxHp = Math.Max(1, maxHp);
            IsPersistent = isPersistent;
            PreviousSuccessToughnessModifier = Math.Max(0, previousSuccessToughnessModifier);
        }
    }

    public sealed class HitLocationState
    {
        public HitLocationDefinition Definition { get; }
        public int CurrentHp { get; private set; }
        public bool IsDestroyed { get; private set; }
        public bool IsFaceUp { get; private set; }
        public bool IsPersistentActive { get; private set; }

        public HitLocationState(HitLocationDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            CurrentHp = definition.MaxHp;
        }

        public void Reveal() => IsFaceUp = true;

        public void Hide()
        {
            if (!IsDestroyed && !IsPersistentActive)
                IsFaceUp = false;
        }

        public void ActivatePersistent()
        {
            if (!Definition.IsPersistent || IsDestroyed) return;
            IsPersistentActive = true;
            IsFaceUp = true;
        }

        public bool ApplyDamage(int amount)
        {
            if (amount <= 0 || IsDestroyed)
                return false;

            CurrentHp = Math.Max(0, CurrentHp - amount);
            if (CurrentHp > 0)
                return false;

            IsDestroyed = true;
            IsPersistentActive = false;
            IsFaceUp = true;
            return true;
        }

        /// <summary>
        /// Applies damage that is finalized by a later resolver. This preserves legacy effect
        /// ordering where destruction is only announced by the main wound step.
        /// </summary>
        public void ApplyPendingDamage(int amount)
        {
            if (amount > 0 && !IsDestroyed)
                CurrentHp -= amount;
        }

        /// <summary>Restores serialized/adapter state at a composition boundary.</summary>
        public void Restore(int currentHp, bool isDestroyed, bool isFaceUp, bool isPersistentActive = false)
        {
            CurrentHp = Math.Max(0, Math.Min(currentHp, Definition.MaxHp));
            IsDestroyed = isDestroyed;
            IsFaceUp = IsDestroyed || isFaceUp;
            IsPersistentActive = !IsDestroyed && Definition.IsPersistent && isPersistentActive;
            if (IsPersistentActive)
                IsFaceUp = true;
        }
    }

    public static class HitLocationSequenceRules
    {
        public static int GetEffectiveToughness(HitLocationDefinition definition, bool previousSucceeded)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return definition.Toughness + (previousSucceeded ? definition.PreviousSuccessToughnessModifier : 0);
        }

        public static bool DoesSuccessCardSucceed(HitLocationDefinition definition, int attackPower, bool previousSucceeded)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!previousSucceeded || definition.PreviousSuccessToughnessModifier <= 0) return true;
            return attackPower >= GetEffectiveToughness(definition, true);
        }
    }

    public readonly struct AttackCheck
    {
        public int AttackPower { get; }
        public AttackOutcome Outcome { get; }
        public bool IsCritical { get; }

        public AttackCheck(int attackPower, AttackOutcome outcome, bool isCritical)
        {
            AttackPower = attackPower;
            Outcome = outcome;
            IsCritical = isCritical;
        }
    }
}
