using System;
using System.Collections.Generic;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;

namespace HuntingInDarkness.Settlement
{
    internal sealed class SettlementEventResourceAvailability : IPlayableEventResourceAvailability
    {
        private readonly SettlementInstance settlement;

        public SettlementEventResourceAvailability(SettlementInstance settlement)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
        }

        public PlayableEventResourceScope Scope => PlayableEventResourceScope.Settlement;

        public int GetAvailableAmount(string resourceId)
        {
            string resolvedId = PlayableSettlementItemRegistry.ResolveContentId(resourceId);
            if (string.IsNullOrWhiteSpace(resolvedId)) return 0;
            int amount = settlement.GetResource(resolvedId);
            return amount < 0 ? 0 : amount;
        }
    }

    public static class PlayableEventOptionAvailability
    {
        public static bool RequiresHunter(EventOption option)
        {
            if (option == null) return false;
            if (ContainsHunterEffect(option.successEffects) || ContainsHunterEffect(option.failEffects)) return true;
            if (option.conditions == null || option.alwaysAvailable) return false;
            foreach (EventOptionCondition condition in option.conditions)
                if (condition != null && EventOptionAvailabilityRules.RequiresHunter(condition.ToDomain()))
                    return true;
            return false;
        }

        public static bool HasHunterDeathEffect(EventOption option)
        {
            return option != null && (ContainsEffect(option.successEffects, EventEffectType.KillHunter) || ContainsEffect(option.failEffects, EventEffectType.KillHunter));
        }

        private static bool ContainsEffect(IReadOnlyList<EventEffect> effects, EventEffectType effectType)
        {
            if (effects == null) return false;
            foreach (EventEffect effect in effects)
                if (effect != null && effect.effectType == effectType)
                    return true;
            return false;
        }

        private static bool ContainsHunterEffect(IReadOnlyList<EventEffect> effects)
        {
            if (effects == null) return false;
            foreach (EventEffect effect in effects)
            {
                if (effect == null) continue;
                switch (effect.effectType)
                {
                    case EventEffectType.AddWillpower:
                    case EventEffectType.RemoveWillpower:
                    case EventEffectType.AddLuck:
                    case EventEffectType.AddInsanity:
                    case EventEffectType.AddCourage:
                    case EventEffectType.AddUnderstanding:
                    case EventEffectType.AddTrait:
                    case EventEffectType.AddAilment:
                    case EventEffectType.AddRecoverableWound:
                    case EventEffectType.KillHunter:
                    case EventEffectType.ActivateBloodline:
                    case EventEffectType.RemoveItem:
                        return true;
                }
            }
            return false;
        }

        public static bool CanUse(EventOption option, HunterInstance hunter, SettlementInstance settlement, out string reason)
        {
            return CanUse(option, hunter, settlement, null, out reason);
        }

        public static bool CanUse(EventOption option, HunterInstance hunter, SettlementInstance settlement, IPlayableEventResourceAvailability resourceAvailability, out string reason)
        {
            if (option == null)
            {
                reason = "事件选项不存在。";
                return false;
            }
            if (option.alwaysAvailable)
            {
                reason = string.Empty;
                return true;
            }

            var definitions = new List<EventOptionConditionDefinition>();
            if (option.conditions != null)
                foreach (EventOptionCondition condition in option.conditions)
                    if (condition != null)
                        definitions.Add(condition.ToDomain());
            IReadOnlyCollection<string> equippedItems = PlayableSettlementItemRegistry.CollectAliases(hunter?.EquippedItemIds, hunter?.EquippedItemNames);
            IReadOnlyCollection<string> keywords = PlayableSettlementItemRegistry.CollectKeywords(equippedItems, hunter?.Traits, hunter?.Ailments);
            Func<string, int> resourceResolver = resourceAvailability != null
                ? resourceAvailability.GetAvailableAmount
                : key => settlement != null ? settlement.GetResource(PlayableSettlementItemRegistry.ResolveContentId(key)) : 0;
            IPlayableEventItemAvailability itemAvailability = resourceAvailability as IPlayableEventItemAvailability;
            Func<string, int> carriedItemResolver = itemAvailability != null ? key => itemAvailability.GetAvailableAmount(key, hunter) : null;
            return EventOptionAvailabilityRules.Evaluate(definitions, hunter, resourceResolver, equippedItems, keywords, carriedItemResolver, out reason);
        }

        public static string GetRequirements(EventOption option)
        {
            return GetRequirements(option, null);
        }

        public static string GetRequirements(EventOption option, IPlayableEventResourceAvailability resourceAvailability)
        {
            if (option == null || option.alwaysAvailable) return string.Empty;
            var requirements = new List<string>();
            if (option.conditions != null)
                foreach (EventOptionCondition condition in option.conditions)
                {
                    if (condition == null) continue;
                    EventOptionConditionDefinition definition = condition.ToDomain();
                    if (definition.Kind == EventOptionConditionKind.MinimumResource && resourceAvailability != null)
                    {
                        string owner = resourceAvailability.Scope == PlayableEventResourceScope.HuntCollectibles ? "小队携带" : "营地拥有";
                        string resourceName = PlayableSettlementItemRegistry.TryGet(PlayableSettlementItemRegistry.ResolveContentId(definition.Key), out ItemData item) && item != null && !string.IsNullOrWhiteSpace(item.itemName) ? item.itemName : definition.DisplayName;
                        requirements.Add($"需要{owner} {resourceName} ×{definition.Value}");
                        continue;
                    }
                    if (definition.Kind == EventOptionConditionKind.MinimumCarriedItem)
                    {
                        string itemName = PlayableSettlementItemRegistry.TryGet(PlayableSettlementItemRegistry.ResolveContentId(definition.Key), out ItemData item) && item != null && !string.IsNullOrWhiteSpace(item.itemName) ? item.itemName : definition.DisplayName;
                        requirements.Add($"需要该猎人携带 {itemName} ×{definition.Value}");
                        continue;
                    }
                    requirements.Add(EventOptionAvailabilityRules.Describe(definition));
                }
            return requirements.Count == 0 ? "条件尚未配置" : string.Join("；", requirements);
        }
    }
}
