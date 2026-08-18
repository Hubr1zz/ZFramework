using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    [Serializable]
    public sealed class PlayableSymptomDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
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

        public SymptomDefinition ToDomain()
        {
            return new SymptomDefinition(id, displayName, description, new SymptomStatModifiers(strengthModifier, accuracyModifier, evasionModifier, movementModifier), new SymptomStatModifiers(internalizedStrength, internalizedAccuracy, internalizedEvasion, internalizedMovement), internalizationThreshold, reflectionWillpowerCost, overcomeCourageRequirement, overcomeGrowthCost);
        }
    }

    [CreateAssetMenu(fileName = "PlayableSymptomCatalog", menuName = "Hunting in Darkness/Symptom Catalog")]
    public sealed class PlayableSymptomCatalog : ScriptableObject
    {
        [SerializeField] private List<PlayableSymptomDefinition> symptoms = new();

        public bool IsConfigured => Validate();

        public IReadOnlyList<SymptomDefinition> GetDefinitions()
        {
            var definitions = new List<SymptomDefinition>();
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

        private bool TryGet(Predicate<PlayableSymptomDefinition> predicate, out SymptomDefinition definition)
        {
            PlayableSymptomDefinition symptom = symptoms.Find(item => IsValid(item) && predicate(item));
            definition = symptom?.ToDomain();
            return definition != null;
        }

        private static bool IsValid(PlayableSymptomDefinition symptom)
        {
            return symptom != null && !string.IsNullOrWhiteSpace(symptom.Id) && !string.IsNullOrWhiteSpace(symptom.DisplayName);
        }

        private bool Validate()
        {
            if (symptoms.Count == 0) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlayableSymptomDefinition symptom in symptoms)
                if (!IsValid(symptom) || !ids.Add(symptom.Id) || !names.Add(symptom.DisplayName))
                    return false;
            return true;
        }
    }
}
