using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Foundation;

namespace HuntingInDarkness.GameCore.Settlement
{
    public enum HunterAvailabilityState
    {
        Active,
        Retired
    }

    [Serializable]
    public class ResourceAmount
    {
        public string Key;
        public int Value;
    }

    [Serializable]
    public class NamedFlag
    {
        public string Key;
        public bool Value;
    }

    [Serializable]
    public class HunterStats
    {
        public int strength;
        public int accuracy;
        public int evasion;
        public int movement = 5;
        public int luck;
        public int speed;
        public int armorHead;
        public int armorBody;
        public int armorArms;
        public int armorLegs;
    }

    [Serializable]
    public class HunterHitPoints
    {
        public int head = 2;
        public int body = 4;
        public int arms = 3;
        public int legs = 3;

        public int Total => head + body + arms + legs;
    }

    [Serializable]
    public class WeaponMasteryState
    {
        public string MasteryId;
        public string DisplayName;
        public int Experience;
        public List<string> ClaimedMilestoneIds = new List<string>();
    }

    [Serializable]
    public class HunterState
    {
        public int InstanceId;
        public string Name;
        public bool IsAlive = true;
        public HunterAvailabilityState Availability = HunterAvailabilityState.Active;
        public int Age = 1;
        public int Willpower = 2;
        public int WillpowerMax = 2;
        public int Luck;
        public int Insanity;
        public HunterStats Stats = new HunterStats();
        public HunterHitPoints HP = new HunterHitPoints();
        public HunterHitPoints MaxHP = new HunterHitPoints();
        /// <summary>旧版全局熟练度兼容镜像。新逻辑以 WeaponMasteries 为准。</summary>
        public int WeaponProficiency;
        public List<WeaponMasteryState> WeaponMasteries = new List<WeaponMasteryState>();
        public int Courage;
        public int Understanding;
        public int UnspentGrowth;
        public List<int> EquipmentIds = new List<int>();
        public List<int> CollectibleIds = new List<int>();
        public List<string> Traits = new List<string>();
        public List<string> Ailments = new List<string>();
        public List<string> TempConditions = new List<string>();
        public List<string> PermConditions = new List<string>();
        public List<string> PermanentInjuryIds = new List<string>();
        public List<HunterSymptomState> SymptomStates = new List<HunterSymptomState>();
        public List<string> ClaimedGrowthMilestoneIds = new List<string>();
        public List<SettlementModifierContribution> SettlementModifierContributions = new List<SettlementModifierContribution>();
        public int SurvivalCards = 1;
        public int DeathCards;

        public bool IsDead => !IsAlive || HP.head <= 0 || HP.body <= 0;
        public bool IsAvailable => IsAlive && Availability == HunterAvailabilityState.Active;

        public bool RollDeath(IRandomSource random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            int total = SurvivalCards + DeathCards;
            if (total <= 0)
            {
                IsAlive = false;
                return true;
            }

            bool died = random.Next(0, total) < DeathCards;
            if (died) IsAlive = false;
            else DeathCards++;
            return died;
        }

        public bool SpendWillpower(int amount = 1)
        {
            if (Willpower < amount) return false;
            Willpower -= amount;
            Luck++;
            return true;
        }
    }

    public readonly struct ResourceCost
    {
        public string ResourceId { get; }
        public int Amount { get; }

        public ResourceCost(string resourceId, int amount)
        {
            ResourceId = resourceId ?? string.Empty;
            Amount = amount;
        }
    }

    public sealed class InventionDefinition
    {
        public string Id { get; }
        public IReadOnlyList<string> Prerequisites { get; }
        public IReadOnlyList<string> ExclusiveWith { get; }
        public IReadOnlyList<ResourceCost> Costs { get; }

        public InventionDefinition(
            string id,
            IReadOnlyList<string> prerequisites,
            IReadOnlyList<string> exclusiveWith,
            IReadOnlyList<ResourceCost> costs)
        {
            Id = id ?? string.Empty;
            Prerequisites = prerequisites ?? Array.Empty<string>();
            ExclusiveWith = exclusiveWith ?? Array.Empty<string>();
            Costs = costs ?? Array.Empty<ResourceCost>();
        }
    }

    public enum InventionEffectKind
    {
        None,
        ModifyWillpowerMaximum,
        ModifyStrength
    }

    public enum InventionEffectTarget
    {
        AvailableHunters,
        AliveHunters,
        AllLivingAndFutureHunters
    }

    public enum InventionEffectLifetime
    {
        Unlock,
        Campaign
    }

    public enum SettlementModifierSourceKind
    {
        Invention
    }

    [Serializable]
    public sealed class SettlementModifierState
    {
        public string ModifierId;
        public SettlementModifierSourceKind SourceKind;
        public string SourceId;
        public InventionEffectKind Kind;
        public InventionEffectTarget Target;
        public int ConfiguredValue;
        public int Value;
        public bool HasValueOverride;
    }

    [Serializable]
    public sealed class SettlementModifierContribution
    {
        public string ModifierId;
        public InventionEffectKind Kind;
        public int Value;
    }

    public sealed class CraftRecipeDefinition
    {
        public string Id { get; }
        public string RequiredInventionId { get; }
        public bool UnlockedByMaterial { get; }
        public IReadOnlyList<ResourceCost> Ingredients { get; }
        public string OutputId { get; }
        public int OutputCount { get; }

        public CraftRecipeDefinition(
            string id,
            string requiredInventionId,
            bool unlockedByMaterial,
            IReadOnlyList<ResourceCost> ingredients,
            string outputId,
            int outputCount)
        {
            Id = id ?? string.Empty;
            RequiredInventionId = requiredInventionId ?? string.Empty;
            UnlockedByMaterial = unlockedByMaterial;
            Ingredients = ingredients ?? Array.Empty<ResourceCost>();
            OutputId = outputId ?? string.Empty;
            OutputCount = outputCount;
        }
    }
}
