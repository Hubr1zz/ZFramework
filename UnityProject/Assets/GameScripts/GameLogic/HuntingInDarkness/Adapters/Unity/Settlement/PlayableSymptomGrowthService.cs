using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;

namespace HuntingInDarkness.Settlement
{
    public sealed class PlayableSymptomGrowthService
    {
        private readonly Func<SettlementInstance> settlementProvider;
        private readonly PlayableSymptomCatalog catalog;

        public PlayableSymptomGrowthService(Func<SettlementInstance> settlementProvider, PlayableSymptomCatalog catalog)
        {
            this.settlementProvider = settlementProvider;
            this.catalog = catalog;
        }

        public IReadOnlyList<SymptomDefinition> GetSymptoms(HunterInstance hunter)
        {
            var definitions = new List<SymptomDefinition>();
            if (hunter?.SymptomStates == null || catalog == null) return definitions;
            foreach (HunterSymptomState state in hunter.SymptomStates)
                if (state != null && !state.IsOvercome && catalog.TryGetById(state.SymptomId, out SymptomDefinition definition))
                    definitions.Add(definition);
            return definitions;
        }

        public bool HasActionableHunter()
        {
            SettlementInstance settlement = settlementProvider?.Invoke();
            if (settlement == null) return false;
            PlayableSymptomRuntime.Synchronize(settlement);
            foreach (HunterInstance hunter in settlement.GetAvailableHunters())
                if (GetSymptoms(hunter).Count > 0)
                    return true;
            return false;
        }

        public bool TryInternalize(HunterInstance hunter, SymptomDefinition definition, out string reason)
        {
            SettlementInstance settlement = settlementProvider?.Invoke();
            if (settlement == null)
            {
                reason = "营地尚未准备好。";
                return false;
            }
            return HunterSymptomRules.TryInternalize(hunter, definition, settlement.CurrentYear, out reason);
        }

        public bool TryOvercome(HunterInstance hunter, SymptomDefinition definition, out string reason)
        {
            return HunterSymptomRules.TryOvercome(hunter, definition, out reason);
        }
    }
}
