using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;

namespace HuntingInDarkness.Settlement
{
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
                    case EventEffectType.KillHunter:
                    case EventEffectType.ActivateBloodline:
                        return true;
                }
            }
            return false;
        }

        public static bool CanUse(EventOption option, HunterInstance hunter, SettlementInstance settlement, out string reason)
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
            return EventOptionAvailabilityRules.Evaluate(definitions, hunter, key => settlement != null ? settlement.GetResource(PlayableSettlementItemRegistry.ResolveContentId(key)) : 0, equippedItems, keywords, out reason);
        }

        public static string GetRequirements(EventOption option)
        {
            if (option == null || option.alwaysAvailable) return string.Empty;
            var requirements = new List<string>();
            if (option.conditions != null)
                foreach (EventOptionCondition condition in option.conditions)
                    if (condition != null)
                        requirements.Add(EventOptionAvailabilityRules.Describe(condition.ToDomain()));
            return requirements.Count == 0 ? "条件尚未配置" : string.Join("；", requirements);
        }
    }
}
