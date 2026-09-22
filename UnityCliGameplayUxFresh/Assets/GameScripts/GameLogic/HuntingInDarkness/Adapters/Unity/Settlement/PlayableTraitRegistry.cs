using System;
using System.Collections.Generic;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;

namespace HuntingInDarkness.Settlement
{
    public static class PlayableTraitRegistry
    {
        public const int CurrentIdentitySchemaVersion = 1;

        private static PlayableTraitCatalog Catalog => PlayableSettlementContentRuntime.TraitCatalog;

        public static bool TryGet(string identifier, out PlayableTraitDefinition definition)
        {
            if (Catalog != null) return Catalog.TryResolve(identifier, out definition);
            definition = null;
            return false;
        }

        public static string ResolveContentId(string identifier) => Catalog?.ResolveContentId(identifier) ?? identifier?.Trim() ?? string.Empty;

        public static string GetDisplayName(string identifier) => Catalog?.GetDisplayName(identifier) ?? identifier?.Trim() ?? string.Empty;

        public static string GetDisplayNames(IReadOnlyCollection<string> identifiers)
        {
            if (identifiers == null || identifiers.Count == 0) return string.Empty;
            var displayNames = new List<string>();
            foreach (string identifier in identifiers)
                displayNames.Add(GetDisplayName(identifier));
            return string.Join("、", displayNames);
        }

        public static void AddKeywords(ISet<string> keywords, IReadOnlyCollection<string> traitIds)
        {
            if (Catalog != null)
            {
                Catalog.AddKeywords(keywords, traitIds);
                return;
            }
            foreach (string traitId in traitIds ?? Array.Empty<string>())
                KeywordRules.TryAdd(keywords, traitId);
        }

        public static bool MigratePersistentState(SettlementInstance settlement)
        {
            return MigratePersistentState(settlement, Catalog);
        }

        public static bool MigratePersistentState(SettlementInstance settlement, PlayableTraitCatalog catalog)
        {
            if (settlement == null || settlement.TraitIdentitySchemaVersion > CurrentIdentitySchemaVersion) return false;
            bool changed = false;
            foreach (HunterInstance hunter in settlement.Hunters ?? new List<HunterInstance>())
            {
                if (hunter == null) continue;
                hunter.Traits ??= new List<string>();
                var normalized = new List<string>();
                var known = new HashSet<string>(StringComparer.Ordinal);
                foreach (string trait in hunter.Traits)
                {
                    string traitId = catalog?.ResolveContentId(trait) ?? trait?.Trim() ?? string.Empty;
                    if (traitId.Length > 0 && known.Add(traitId)) normalized.Add(traitId);
                }
                if (!Same(hunter.Traits, normalized))
                {
                    hunter.Traits = normalized;
                    changed = true;
                }
            }
            if (settlement.TraitIdentitySchemaVersion < CurrentIdentitySchemaVersion)
            {
                settlement.TraitIdentitySchemaVersion = CurrentIdentitySchemaVersion;
                changed = true;
            }
            return changed;
        }

        private static bool Same(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (left.Count != right.Count) return false;
            for (int index = 0; index < left.Count; index++)
                if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
                    return false;
            return true;
        }
    }
}
