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
                if (catalog.TryResolveReference(ailment, out SymptomDefinition definition, out _))
                    HunterSymptomRules.Register(hunter, definition);

            if (hunter.SymptomStates == null) return;
            foreach (HunterSymptomState state in hunter.SymptomStates)
            {
                if (state == null || !catalog.TryGetById(state.SymptomId, out SymptomDefinition definition)) continue;
                NormalizeProjection(hunter, definition, state.IsOvercome);
            }
        }

        public static bool TryAcquire(HunterInstance hunter, string symptomId, out SymptomDefinition definition, out bool added, out string reason)
        {
            definition = null;
            added = false;
            if (hunter == null || !hunter.IsAlive)
            {
                reason = "事件没有可获得症状的存活猎人。";
                return false;
            }
            if (catalog == null)
            {
                reason = "症状目录尚未配置。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(symptomId) || !catalog.TryGetById(symptomId, out definition))
            {
                reason = $"未注册症状：{symptomId}";
                return false;
            }

            HunterSymptomState previous = HunterSymptomRules.Find(hunter, definition.Id);
            HunterSymptomState state = HunterSymptomRules.Register(hunter, definition);
            if (state == null)
            {
                reason = "症状状态无法写入猎人。";
                return false;
            }
            added = previous == null;
            NormalizeProjection(hunter, definition, state.IsOvercome);
            reason = string.Empty;
            return true;
        }

        private static void NormalizeProjection(HunterState hunter, SymptomDefinition definition, bool isOvercome)
        {
            if (hunter?.Ailments == null || catalog == null) return;
            hunter.Ailments.RemoveAll(value => catalog.TryResolveReference(value, out SymptomDefinition resolved, out _) && resolved.Id == definition.Id);
            if (!isOvercome)
                hunter.Ailments.Add(definition.DisplayName);
        }
    }
}
