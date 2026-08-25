using System;
using System.Collections.Generic;
using System.Linq;
using HuntingInDarkness.GameCore.Foundation;

namespace HuntingInDarkness.GameCore.Settlement
{
    public static class ResourceRules
    {
        public static int Get<TEntry>(IReadOnlyList<TEntry> resources, string name)
            where TEntry : ResourceAmount
        {
            TEntry entry = resources.FirstOrDefault(item => item.Key == name);
            return entry?.Value ?? 0;
        }

        public static void Add<TEntry>(
            IList<TEntry> resources,
            string name,
            int amount,
            Func<TEntry> createEntry)
            where TEntry : ResourceAmount
        {
            TEntry entry = resources.FirstOrDefault(item => item.Key == name);
            if (entry == null)
            {
                entry = createEntry();
                entry.Key = name;
                resources.Add(entry);
            }
            entry.Value = Math.Max(0, entry.Value + amount);
        }

        public static bool Spend<TEntry>(
            IList<TEntry> resources,
            string name,
            int amount,
            Func<TEntry> createEntry)
            where TEntry : ResourceAmount
        {
            if (amount <= 0) return amount == 0;
            if (Get((IReadOnlyList<TEntry>)resources, name) < amount)
                return false;
            Add(resources, name, -amount, createEntry);
            return true;
        }
    }

    public static class DepartureRules
    {
        public const int MaximumHunters = 4;

        public static bool CanDepart(IReadOnlyCollection<int> hunterIds, out string reason)
        {
            if (hunterIds == null || hunterIds.Count == 0)
            {
                reason = "未选择猎人";
                return false;
            }
            if (hunterIds.Count > MaximumHunters)
            {
                reason = "猎人数超过4人上限";
                return false;
            }
            reason = string.Empty;
            return true;
        }
    }

    public static class InventionRules
    {
        public static bool CanUnlock(
            InventionDefinition definition,
            Func<string, bool> isUnlocked,
            Func<string, int> getResource,
            out string reason)
        {
            if (definition == null)
            {
                reason = "无效发明";
                return false;
            }
            if (isUnlocked(definition.Id))
            {
                reason = "已解锁";
                return false;
            }
            foreach (string prerequisite in definition.Prerequisites)
            {
                if (!isUnlocked(prerequisite))
                {
                    reason = $"需先解锁：{prerequisite}";
                    return false;
                }
            }
            foreach (string exclusive in definition.ExclusiveWith)
            {
                if (isUnlocked(exclusive))
                {
                    reason = $"与 {exclusive} 互斥";
                    return false;
                }
            }
            foreach (ResourceCost cost in definition.Costs)
            {
                if (string.IsNullOrEmpty(cost.ResourceId) || cost.Amount <= 0)
                {
                    reason = "发明成本配置无效";
                    return false;
                }
                int have = getResource(cost.ResourceId);
                int required = definition.Costs.Where(candidate => candidate.ResourceId == cost.ResourceId).Sum(candidate => candidate.Amount);
                if (have < required)
                {
                    reason = $"资源不足：{cost.ResourceId} 需要 {required}，当前 {have}";
                    return false;
                }
            }
            reason = string.Empty;
            return true;
        }
    }

    public static class InventionEffectRules
    {
        public static bool IsEligible(HunterState hunter, InventionEffectTarget target)
        {
            if (hunter == null || hunter.IsDead) return false;
            return target == InventionEffectTarget.AliveHunters || target == InventionEffectTarget.AllLivingAndFutureHunters || target == InventionEffectTarget.AvailableHunters && hunter.IsAvailable;
        }

        public static bool TryApply(HunterState hunter, InventionEffectKind kind, int value, out int previousValue, out int currentValue)
        {
            previousValue = 0;
            currentValue = 0;
            if (hunter == null || kind == InventionEffectKind.None) return false;
            switch (kind)
            {
                case InventionEffectKind.ModifyWillpowerMaximum:
                    previousValue = hunter.WillpowerMax;
                    hunter.WillpowerMax = ClampToInt((long)hunter.WillpowerMax + value, 0);
                    hunter.Willpower = Math.Max(0, Math.Min(hunter.Willpower, hunter.WillpowerMax));
                    currentValue = hunter.WillpowerMax;
                    return true;
                case InventionEffectKind.ModifyStrength:
                    if (hunter.Stats == null) return false;
                    previousValue = hunter.Stats.strength;
                    hunter.Stats.strength = ClampToInt((long)hunter.Stats.strength + value, int.MinValue);
                    currentValue = hunter.Stats.strength;
                    return true;
                default:
                    return false;
            }
        }

        private static int ClampToInt(long value, int minimum)
        {
            if (value < minimum) return minimum;
            if (value > int.MaxValue) return int.MaxValue;
            return (int)value;
        }
    }

    public static class WorkshopRules
    {
        public static bool IsUnlocked(
            CraftRecipeDefinition recipe,
            Func<string, bool> isInventionUnlocked,
            Func<string, bool> hasDiscoveredMaterial)
        {
            if (recipe == null) return false;
            if (!string.IsNullOrEmpty(recipe.RequiredInventionId) &&
                !isInventionUnlocked(recipe.RequiredInventionId)) return false;
            if (!recipe.UnlockedByMaterial) return true;
            return recipe.Ingredients.Any(cost => hasDiscoveredMaterial(cost.ResourceId));
        }

        public static bool CanCraft(
            CraftRecipeDefinition recipe,
            Func<string, bool> isInventionUnlocked,
            Func<string, bool> hasDiscoveredMaterial,
            Func<string, int> getResource,
            out string reason)
        {
            if (recipe == null)
            {
                reason = "无效配方";
                return false;
            }
            if (!IsUnlocked(recipe, isInventionUnlocked, hasDiscoveredMaterial))
            {
                reason = "配方未解锁";
                return false;
            }
            if (string.IsNullOrEmpty(recipe.OutputId))
            {
                reason = "产出物品未配置";
                return false;
            }
            if (recipe.OutputCount <= 0)
            {
                reason = "产出数量必须大于0";
                return false;
            }
            foreach (ResourceCost ingredient in recipe.Ingredients)
            {
                if (string.IsNullOrEmpty(ingredient.ResourceId) || ingredient.Amount <= 0)
                {
                    reason = "配方材料配置无效";
                    return false;
                }
                int have = getResource(ingredient.ResourceId);
                int required = recipe.Ingredients.Where(candidate => candidate.ResourceId == ingredient.ResourceId).Sum(candidate => candidate.Amount);
                if (have < required)
                {
                    reason = $"资源不足：{ingredient.ResourceId} 需要 {required}，当前 {have}";
                    return false;
                }
            }
            reason = string.Empty;
            return true;
        }
    }

    public readonly struct RerollOutcome
    {
        public bool Success { get; }
        public int NewRoll { get; }
        public int FinalRoll { get; }

        public RerollOutcome(bool success, int newRoll, int finalRoll)
        {
            Success = success;
            NewRoll = newRoll;
            FinalRoll = finalRoll;
        }
    }

    public static class EventRules
    {
        public static int RollDice(IRandomSource random, int diceCount, int sides)
        {
            int total = 0;
            for (int i = 0; i < diceCount; i++)
                total += random.Next(1, sides + 1);
            return total;
        }

        public static bool CheckSucceeded(int roll, int bonus, int target) =>
            roll + bonus >= target;

        public static RerollOutcome TryReroll(
            HunterState hunter,
            int currentRoll,
            int diceCount,
            int sides,
            IRandomSource random)
        {
            if (!hunter.SpendWillpower())
                return new RerollOutcome(false, 0, currentRoll);
            int newRoll = RollDice(random, diceCount, sides);
            return new RerollOutcome(true, newRoll, Math.Max(currentRoll, newRoll));
        }

        public static RerollOutcome TryReroll(HunterState hunter, int currentRoll, int newRoll)
            => TryReroll(hunter, currentRoll, newRoll, 1, 10);

        public static RerollOutcome TryReroll(HunterState hunter, int currentRoll, int newRoll, int minimumRoll, int maximumRoll)
        {
            if (minimumRoll > maximumRoll || newRoll < minimumRoll || newRoll > maximumRoll) return new RerollOutcome(false, 0, currentRoll);
            if (!hunter.SpendWillpower()) return new RerollOutcome(false, 0, currentRoll);
            return new RerollOutcome(true, newRoll, Math.Max(currentRoll, newRoll));
        }

        public static int ClampWillpower(int current, int delta, int maximum) =>
            Math.Max(0, Math.Min(current + delta, maximum));
    }

    public enum SettlementEffectKind
    {
        AddResource,
        RemoveResource,
        AddWillpower,
        RemoveWillpower,
        AddLuck,
        AddInsanity,
        AddCourage,
        AddUnderstanding,
        AddTrait,
        AddAilment,
        UnlockInvention,
        TriggerCombat,
        AdvanceYear,
        Unsupported
    }

    public readonly struct SettlementEffectOutcome
    {
        public bool Handled { get; }
        public string Reason { get; }
        public bool ResourceChanged { get; }
        public string ResourceId { get; }
        public int OldAmount { get; }
        public int NewAmount { get; }
        public bool TriggerCombat { get; }
        public bool AdvanceYear { get; }

        public SettlementEffectOutcome(
            bool handled,
            bool resourceChanged = false,
            string resourceId = "",
            int oldAmount = 0,
            int newAmount = 0,
            bool triggerCombat = false,
            bool advanceYear = false,
            string reason = "")
        {
            Handled = handled;
            Reason = reason ?? string.Empty;
            ResourceChanged = resourceChanged;
            ResourceId = resourceId;
            OldAmount = oldAmount;
            NewAmount = newAmount;
            TriggerCombat = triggerCombat;
            AdvanceYear = advanceYear;
        }
    }

    public static class SettlementEffectRules
    {
        public static SettlementEffectOutcome Apply(
            SettlementEffectKind kind,
            string targetName,
            int value,
            HunterState selectedHunter,
            HunterState directTarget,
            IEnumerable<HunterState> hunters,
            Func<string, int> getResource,
            Action<string, int> addResource,
            Func<string, int, bool> spendResource,
            Action<string> unlockInvention)
        {
            switch (kind)
            {
                case SettlementEffectKind.AddResource:
                    int oldAmount = getResource(targetName);
                    addResource(targetName, value);
                    return new SettlementEffectOutcome(
                        true, true, targetName, oldAmount, getResource(targetName));
                case SettlementEffectKind.RemoveResource:
                    if (spendResource(targetName, value))
                        return new SettlementEffectOutcome(true);
                    return new SettlementEffectOutcome(false, reason: $"资源不足：{targetName} 需要 {value}，当前 {getResource(targetName)}");
                case SettlementEffectKind.AddWillpower:
                    if (!ApplyToTargets(targetName, selectedHunter, hunters, hunter =>
                        hunter.Willpower = EventRules.ClampWillpower(
                            hunter.Willpower, value, hunter.WillpowerMax)))
                        return MissingTarget(targetName);
                    return new SettlementEffectOutcome(true);
                case SettlementEffectKind.RemoveWillpower:
                    if (!ApplyToTargets(targetName, selectedHunter, hunters, hunter =>
                        hunter.Willpower = EventRules.ClampWillpower(
                            hunter.Willpower, -value, hunter.WillpowerMax)))
                        return MissingTarget(targetName);
                    return new SettlementEffectOutcome(true);
                case SettlementEffectKind.AddLuck:
                    if (!ApplyToTargets(targetName, selectedHunter, hunters, hunter => hunter.Luck += value))
                        return MissingTarget(targetName);
                    return new SettlementEffectOutcome(true);
                case SettlementEffectKind.AddInsanity:
                    if (!ApplyToTargets(targetName, selectedHunter, hunters, hunter => hunter.Insanity += value))
                        return MissingTarget(targetName);
                    return new SettlementEffectOutcome(true);
                case SettlementEffectKind.AddCourage:
                    if (!ApplyToTargets(targetName, selectedHunter, hunters, hunter => hunter.Courage = Math.Max(0, Math.Min(HunterAdvancementRules.MaximumGrowthAttribute, hunter.Courage + value))))
                        return MissingTarget(targetName);
                    return new SettlementEffectOutcome(true);
                case SettlementEffectKind.AddUnderstanding:
                    if (!ApplyToTargets(targetName, selectedHunter, hunters, hunter => hunter.Understanding = Math.Max(0, Math.Min(HunterAdvancementRules.MaximumGrowthAttribute, hunter.Understanding + value))))
                        return MissingTarget(targetName);
                    return new SettlementEffectOutcome(true);
                case SettlementEffectKind.AddTrait:
                    if (directTarget == null)
                        return MissingTarget(targetName);
                    if (!directTarget.Traits.Contains(targetName)) directTarget.Traits.Add(targetName);
                    return new SettlementEffectOutcome(true);
                case SettlementEffectKind.AddAilment:
                    if (directTarget == null)
                        return MissingTarget(targetName);
                    if (!directTarget.Ailments.Contains(targetName)) directTarget.Ailments.Add(targetName);
                    return new SettlementEffectOutcome(true);
                case SettlementEffectKind.UnlockInvention:
                    unlockInvention(targetName);
                    return new SettlementEffectOutcome(true);
                case SettlementEffectKind.TriggerCombat:
                    return new SettlementEffectOutcome(true, triggerCombat: true);
                case SettlementEffectKind.AdvanceYear:
                    return new SettlementEffectOutcome(true, advanceYear: true);
                default:
                    return new SettlementEffectOutcome(false);
            }
        }

        private static SettlementEffectOutcome MissingTarget(string targetName) => new(false, reason: $"未找到效果目标：{targetName}");

        private static bool ApplyToTargets(
            string targetSpec,
            HunterState selectedHunter,
            IEnumerable<HunterState> hunters,
            Action<HunterState> action)
        {
            if (targetSpec == "all")
            {
                bool applied = false;
                foreach (HunterState hunter in hunters)
                {
                    if (!hunter.IsAlive) continue;
                    action(hunter);
                    applied = true;
                }
                return applied;
            }
            if (targetSpec == "selected" && selectedHunter != null)
            {
                action(selectedHunter);
                return true;
            }
            if (int.TryParse(targetSpec, out int id))
            {
                HunterState target = hunters.FirstOrDefault(hunter => hunter.InstanceId == id);
                if (target == null) return false;
                action(target);
                return true;
            }
            if (selectedHunter == null) return false;
            action(selectedHunter);
            return true;
        }
    }

    public static class SettlementTimelineRules
    {
        public static int AdvanceYear(int currentYear) => currentYear + 1;

        public static bool IsAvailableForYear(int year, int minimumYear, int maximumYear) =>
            year >= minimumYear && (maximumYear <= 0 || year <= maximumYear);
    }
}
