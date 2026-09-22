using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Foundation;

namespace HuntingInDarkness.GameCore.Hunters
{
    public enum HunterBodyPart
    {
        Head,
        Torso,
        Arms,
        Legs
    }

    public sealed class HunterBodyPartDefinition
    {
        public HunterBodyPart Part { get; }
        public int MaxHealth { get; }
        public int Armor { get; }

        public HunterBodyPartDefinition(HunterBodyPart part, int maxHealth, int armor)
        {
            Part = part;
            MaxHealth = Math.Max(1, maxHealth);
            Armor = Math.Max(0, armor);
        }
    }

    public sealed class HunterInjuryProfile
    {
        private readonly Dictionary<HunterBodyPart, HunterBodyPartDefinition> _definitions;

        public HunterInjuryProfile(
            HunterBodyPartDefinition head,
            HunterBodyPartDefinition torso,
            HunterBodyPartDefinition arms,
            HunterBodyPartDefinition legs)
        {
            _definitions = new Dictionary<HunterBodyPart, HunterBodyPartDefinition>
            {
                [HunterBodyPart.Head] = RequirePart(head, HunterBodyPart.Head),
                [HunterBodyPart.Torso] = RequirePart(torso, HunterBodyPart.Torso),
                [HunterBodyPart.Arms] = RequirePart(arms, HunterBodyPart.Arms),
                [HunterBodyPart.Legs] = RequirePart(legs, HunterBodyPart.Legs)
            };
        }

        public HunterBodyPartDefinition Get(HunterBodyPart part) => _definitions[part];

        public static HunterInjuryProfile CreateDefault(
            int headArmor = 0,
            int torsoArmor = 0,
            int armsArmor = 0,
            int legsArmor = 0)
        {
            return new HunterInjuryProfile(
                new HunterBodyPartDefinition(HunterBodyPart.Head, 2, headArmor),
                new HunterBodyPartDefinition(HunterBodyPart.Torso, 4, torsoArmor),
                new HunterBodyPartDefinition(HunterBodyPart.Arms, 3, armsArmor),
                new HunterBodyPartDefinition(HunterBodyPart.Legs, 3, legsArmor));
        }

        private static HunterBodyPartDefinition RequirePart(
            HunterBodyPartDefinition definition,
            HunterBodyPart expected)
        {
            if (definition == null)
                throw new ArgumentNullException(expected.ToString());
            if (definition.Part != expected)
                throw new ArgumentException($"Expected {expected} definition, got {definition.Part}.");
            return definition;
        }
    }

    public sealed class HunterBodyPartState
    {
        public HunterBodyPartDefinition Definition { get; }
        public int CurrentHealth { get; private set; }
        public int Armor => Definition.Armor;

        internal HunterBodyPartState(HunterBodyPartDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            CurrentHealth = definition.MaxHealth;
        }

        internal int ApplyHealthDamage(int damage)
        {
            int previous = CurrentHealth;
            CurrentHealth = Math.Max(0, CurrentHealth - Math.Max(0, damage));
            return previous - CurrentHealth;
        }
    }

    public interface IArmorMitigationRule
    {
        int GetDamageAfterArmor(int incomingDamage, int armor);
    }

    public sealed class FlatArmorMitigationRule : IArmorMitigationRule
    {
        public static readonly FlatArmorMitigationRule Instance = new FlatArmorMitigationRule();

        private FlatArmorMitigationRule() { }

        public int GetDamageAfterArmor(int incomingDamage, int armor) =>
            Math.Max(0, incomingDamage - Math.Max(0, armor));
    }

    public sealed class PermanentInjury
    {
        public string Id { get; }
        public string DisplayName { get; }
        public PermanentInjuryStatModifiers StatModifiers { get; }

        public PermanentInjury(string id, string displayName)
            : this(id, displayName, PermanentInjuryStatModifiers.None)
        {
        }

        public PermanentInjury(string id, string displayName, PermanentInjuryStatModifiers statModifiers)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            StatModifiers = statModifiers;
        }
    }

    public readonly struct PermanentInjuryStatModifiers
    {
        public static PermanentInjuryStatModifiers None => default;

        public int Strength { get; }
        public int Accuracy { get; }
        public int Evasion { get; }
        public int Movement { get; }

        public PermanentInjuryStatModifiers(int strength, int accuracy, int evasion, int movement)
        {
            Strength = strength;
            Accuracy = accuracy;
            Evasion = evasion;
            Movement = movement;
        }
    }

    public interface IPermanentInjuryResolver
    {
        PermanentInjury Resolve(HunterBodyPart bodyPart, IRandomSource random);
    }

    public readonly struct HunterDamageResult
    {
        public HunterBodyPart BodyPart { get; }
        public int IncomingDamage { get; }
        public int ArmorPrevented { get; }
        public int HealthLost { get; }
        public int RemainingHealth { get; }
        public bool FatalInjuryTriggered { get; }
        public DeathDrawResult? DeathDraw { get; }
        public PermanentInjury PermanentInjury { get; }
        public bool IsDead { get; }

        public HunterDamageResult(
            HunterBodyPart bodyPart,
            int incomingDamage,
            int armorPrevented,
            int healthLost,
            int remainingHealth,
            bool fatalInjuryTriggered,
            DeathDrawResult? deathDraw,
            PermanentInjury permanentInjury,
            bool isDead)
        {
            BodyPart = bodyPart;
            IncomingDamage = incomingDamage;
            ArmorPrevented = armorPrevented;
            HealthLost = healthLost;
            RemainingHealth = remainingHealth;
            FatalInjuryTriggered = fatalInjuryTriggered;
            DeathDraw = deathDraw;
            PermanentInjury = permanentInjury;
            IsDead = isDead;
        }
    }

    public sealed class HunterInjuryState
    {
        private readonly Dictionary<HunterBodyPart, HunterBodyPartState> _parts;
        private readonly List<PermanentInjury> _permanentInjuries = new List<PermanentInjury>();

        public DeathDeck DeathDeck { get; }
        public bool IsDead { get; private set; }
        public IReadOnlyList<PermanentInjury> PermanentInjuries => _permanentInjuries;

        public HunterInjuryState(HunterInjuryProfile profile, DeathDeck deathDeck = null)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            DeathDeck = deathDeck ?? new DeathDeck();
            _parts = new Dictionary<HunterBodyPart, HunterBodyPartState>
            {
                [HunterBodyPart.Head] = new HunterBodyPartState(profile.Get(HunterBodyPart.Head)),
                [HunterBodyPart.Torso] = new HunterBodyPartState(profile.Get(HunterBodyPart.Torso)),
                [HunterBodyPart.Arms] = new HunterBodyPartState(profile.Get(HunterBodyPart.Arms)),
                [HunterBodyPart.Legs] = new HunterBodyPartState(profile.Get(HunterBodyPart.Legs))
            };
        }

        public HunterBodyPartState GetPart(HunterBodyPart part) => _parts[part];

        public bool AddPermanentInjury(PermanentInjury injury)
        {
            if (injury == null || string.IsNullOrWhiteSpace(injury.Id) || _permanentInjuries.Exists(current => current.Id == injury.Id))
                return false;
            _permanentInjuries.Add(injury);
            return true;
        }

        public bool WillTriggerFatalInjury(HunterBodyPart bodyPart, int incomingDamage, IArmorMitigationRule armorRule = null)
        {
            HunterBodyPartState part = GetPart(bodyPart);
            IArmorMitigationRule mitigation = armorRule ?? FlatArmorMitigationRule.Instance;
            int effectiveDamage = Math.Max(0, mitigation.GetDamageAfterArmor(Math.Max(0, incomingDamage), part.Armor));
            return !IsDead && part.CurrentHealth == 0 && effectiveDamage > 0;
        }

        public HunterDamageResult ApplyDamage(
            HunterBodyPart bodyPart,
            int incomingDamage,
            IRandomSource random,
            IArmorMitigationRule armorRule = null,
            IPermanentInjuryResolver permanentInjuryResolver = null,
            DeathDeckDrawOrder deathDrawOrder = null,
            int deathCardPosition = 0)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            HunterBodyPartState part = GetPart(bodyPart);
            int safeIncomingDamage = Math.Max(0, incomingDamage);
            IArmorMitigationRule mitigation = armorRule ?? FlatArmorMitigationRule.Instance;
            int effectiveDamage = Math.Max(0,
                mitigation.GetDamageAfterArmor(safeIncomingDamage, part.Armor));
            int armorPrevented = safeIncomingDamage - Math.Min(safeIncomingDamage, effectiveDamage);

            if (IsDead || effectiveDamage == 0)
                return CreateResult(bodyPart, safeIncomingDamage, armorPrevented, 0, part, false, null, null);

            if (part.CurrentHealth > 0)
            {
                int healthLost = part.ApplyHealthDamage(effectiveDamage);
                return CreateResult(
                    bodyPart, safeIncomingDamage, armorPrevented, healthLost, part, false, null, null);
            }

            DeathDrawResult deathDraw = deathDrawOrder != null ? DeathDeck.Draw(deathDrawOrder, deathCardPosition) : DeathDeck.Draw(random);
            PermanentInjury injury = null;
            if (!deathDraw.Survived)
            {
                IsDead = true;
            }
            else if (permanentInjuryResolver != null)
            {
                PermanentInjury resolved = permanentInjuryResolver.Resolve(bodyPart, random);
                if (AddPermanentInjury(resolved))
                    injury = resolved;
            }

            return CreateResult(
                bodyPart, safeIncomingDamage, armorPrevented, 0, part, true, deathDraw, injury);
        }

        private HunterDamageResult CreateResult(
            HunterBodyPart bodyPart,
            int incomingDamage,
            int armorPrevented,
            int healthLost,
            HunterBodyPartState part,
            bool fatalInjuryTriggered,
            DeathDrawResult? deathDraw,
            PermanentInjury injury)
        {
            return new HunterDamageResult(
                bodyPart,
                incomingDamage,
                armorPrevented,
                healthLost,
                part.CurrentHealth,
                fatalInjuryTriggered,
                deathDraw,
                injury,
                IsDead);
        }
    }
}
