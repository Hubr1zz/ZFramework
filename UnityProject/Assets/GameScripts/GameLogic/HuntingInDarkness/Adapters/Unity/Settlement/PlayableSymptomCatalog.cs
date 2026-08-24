using System;
using System.Collections.Generic;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    public enum SymptomReferenceKind
    {
        None,
        StableId,
        DisplayName,
        LegacyAlias
    }

    [Serializable]
    public sealed class PlayableSymptomDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private List<string> legacyAliases = new();
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private int strengthModifier;
        [SerializeField] private int accuracyModifier;
        [SerializeField] private int evasionModifier;
        [SerializeField] private int movementModifier;
        [Header("内化奖励")]
        [SerializeField] private int internalizedStrength;
        [SerializeField] private int internalizedAccuracy;
        [SerializeField] private int internalizedEvasion;
        [SerializeField] private int internalizedMovement;
        [SerializeField, Min(1)] private int internalizationThreshold = 2;
        [SerializeField, Min(0)] private int reflectionWillpowerCost = 1;
        [Header("克服条件")]
        [SerializeField, Min(0)] private int overcomeCourageRequirement = 2;
        [SerializeField, Min(0)] private int overcomeGrowthCost = 1;

        public string Id => id;
        public string DisplayName => displayName;
        public IReadOnlyList<string> LegacyAliases => legacyAliases != null ? legacyAliases : Array.Empty<string>();

        public SymptomDefinition ToDomain()
        {
            return new SymptomDefinition(id, displayName, description, new SymptomStatModifiers(strengthModifier, accuracyModifier, evasionModifier, movementModifier), new SymptomStatModifiers(internalizedStrength, internalizedAccuracy, internalizedEvasion, internalizedMovement), internalizationThreshold, reflectionWillpowerCost, overcomeCourageRequirement, overcomeGrowthCost);
        }
    }

    [CreateAssetMenu(fileName = "PlayableSymptomCatalog", menuName = "Hunting in Darkness/Symptom Catalog")]
    public sealed class PlayableSymptomCatalog : ScriptableObject, ISettlementSymptomContent
    {
        [SerializeField] private List<PlayableSymptomDefinition> symptoms = new();

        public bool IsConfigured => Validate();

        public IReadOnlyList<SymptomDefinition> GetDefinitions()
        {
            var definitions = new List<SymptomDefinition>();
            if (symptoms == null) return definitions;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlayableSymptomDefinition symptom in symptoms)
            {
                if (!IsValid(symptom) || !ids.Add(symptom.Id) || !names.Add(symptom.DisplayName)) continue;
                definitions.Add(symptom.ToDomain());
            }
            return definitions;
        }

        public bool TryGetById(string id, out SymptomDefinition definition)
        {
            return TryGet(item => string.Equals(item.Id, id, StringComparison.Ordinal), out definition);
        }

        public bool TryGetByDisplayName(string displayName, out SymptomDefinition definition)
        {
            return TryGet(item => string.Equals(item.DisplayName, displayName, StringComparison.Ordinal), out definition);
        }

        public bool TryResolveReference(string reference, out SymptomDefinition definition, out SymptomReferenceKind kind)
        {
            kind = SymptomReferenceKind.None;
            if (string.IsNullOrWhiteSpace(reference))
            {
                definition = null;
                return false;
            }
            string normalized = reference.Trim();
            if (TryGetById(normalized, out definition))
            {
                kind = SymptomReferenceKind.StableId;
                return true;
            }
            if (TryGetByDisplayName(normalized, out definition))
            {
                kind = SymptomReferenceKind.DisplayName;
                return true;
            }
            if (TryGet(item => ContainsAlias(item, normalized), out definition))
            {
                kind = SymptomReferenceKind.LegacyAlias;
                return true;
            }
            return false;
        }

        private bool TryGet(Predicate<PlayableSymptomDefinition> predicate, out SymptomDefinition definition)
        {
            if (symptoms == null)
            {
                definition = null;
                return false;
            }
            PlayableSymptomDefinition symptom = symptoms.Find(item => IsValid(item) && predicate(item));
            definition = symptom?.ToDomain();
            return definition != null;
        }

        private static bool IsValid(PlayableSymptomDefinition symptom)
        {
            if (symptom == null || string.IsNullOrWhiteSpace(symptom.Id) || string.IsNullOrWhiteSpace(symptom.DisplayName)) return false;
            if (!string.Equals(symptom.Id, symptom.Id.Trim(), StringComparison.Ordinal) || !string.Equals(symptom.DisplayName, symptom.DisplayName.Trim(), StringComparison.Ordinal)) return false;
            foreach (string alias in symptom.LegacyAliases)
                if (string.IsNullOrWhiteSpace(alias) || !string.Equals(alias, alias.Trim(), StringComparison.Ordinal))
                    return false;
            return true;
        }

        private bool Validate()
        {
            if (symptoms == null || symptoms.Count == 0) return false;
            var references = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlayableSymptomDefinition symptom in symptoms)
            {
                if (!IsValid(symptom) || !references.Add(symptom.Id.Trim()) || !references.Add(symptom.DisplayName.Trim()))
                    return false;
                foreach (string alias in symptom.LegacyAliases)
                    if (!references.Add(alias.Trim()))
                        return false;
            }
            return true;
        }

        private static bool ContainsAlias(PlayableSymptomDefinition symptom, string reference)
        {
            if (symptom?.LegacyAliases == null) return false;
            foreach (string alias in symptom.LegacyAliases)
                if (string.Equals(alias?.Trim(), reference, StringComparison.Ordinal))
                    return true;
            return false;
        }
    }
}
