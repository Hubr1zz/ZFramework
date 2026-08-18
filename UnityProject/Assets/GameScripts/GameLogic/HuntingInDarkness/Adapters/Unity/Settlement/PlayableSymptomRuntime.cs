using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;

namespace HuntingInDarkness.Settlement
{
    public static class PlayableSymptomRuntime
    {
        private static PlayableSymptomCatalog catalog;

        public static PlayableSymptomCatalog Catalog => catalog;

        public static void Configure(PlayableSymptomCatalog symptomCatalog)
        {
            catalog = symptomCatalog;
        }

        public static void Synchronize(SettlementInstance settlement)
        {
            if (settlement?.Hunters == null || catalog == null) return;
            foreach (HunterInstance hunter in settlement.Hunters)
                SynchronizeHunter(hunter);
        }

        public static void SynchronizeHunter(HunterInstance hunter)
        {
            if (hunter == null || catalog == null) return;
            hunter.Ailments ??= new System.Collections.Generic.List<string>();
            foreach (string ailment in hunter.Ailments.ToArray())
                if (catalog.TryGetByDisplayName(ailment, out SymptomDefinition definition))
                    HunterSymptomRules.Register(hunter, definition);

            if (hunter.SymptomStates == null) return;
            foreach (HunterSymptomState state in hunter.SymptomStates)
            {
                if (state == null || !catalog.TryGetById(state.SymptomId, out SymptomDefinition definition)) continue;
                if (state.IsOvercome)
                {
                    hunter.Ailments.RemoveAll(value => value == definition.DisplayName);
                    continue;
                }
                AddAilmentIfMissing(hunter, definition.DisplayName);
            }
        }

        private static void AddAilmentIfMissing(HunterState hunter, string displayName)
        {
            if (hunter.Ailments == null) return;
            if (!hunter.Ailments.Contains(displayName)) hunter.Ailments.Add(displayName);
        }
    }
}
