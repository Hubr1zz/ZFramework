using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunters;
using UnityEngine;

namespace HuntingInDarkness.Combat
{
    [Serializable]
    public sealed class PlayablePermanentInjuryDefinition
    {
        [SerializeField] private string injuryId;
        [SerializeField] private string displayName;
        [SerializeField] private HunterBodyPart bodyPart;
        [SerializeField, Min(1)] private int drawWeight = 1;
        [SerializeField] private int strengthModifier;
        [SerializeField] private int accuracyModifier;
        [SerializeField] private int evasionModifier;
        [SerializeField] private int movementModifier;

        public string InjuryId => injuryId?.Trim() ?? string.Empty;
        public string DisplayName => displayName?.Trim() ?? string.Empty;
        public HunterBodyPart BodyPart => bodyPart;
        public int DrawWeight => Mathf.Max(1, drawWeight);
        public bool IsValid => !string.IsNullOrEmpty(InjuryId) && !string.IsNullOrEmpty(DisplayName);

        public PermanentInjury CreateInjury()
        {
            return new PermanentInjury(InjuryId, DisplayName, new PermanentInjuryStatModifiers(strengthModifier, accuracyModifier, evasionModifier, movementModifier));
        }
    }

    /// <summary>可配置永久损伤池，同时提供战斗抽取和读档身份恢复。</summary>
    [CreateAssetMenu(fileName = "PlayablePermanentInjuryCatalog", menuName = "Hunting in Darkness/Playable Permanent Injury Catalog")]
    public sealed class PlayablePermanentInjuryCatalog : ScriptableObject, IPermanentInjuryResolver
    {
        [SerializeField] private List<PlayablePermanentInjuryDefinition> injuries = new();

        public IReadOnlyList<PlayablePermanentInjuryDefinition> Injuries => injuries;
        public bool IsConfigured => injuries != null && HasUniqueIds() && HasValidDefinition(HunterBodyPart.Head) && HasValidDefinition(HunterBodyPart.Torso) && HasValidDefinition(HunterBodyPart.Arms) && HasValidDefinition(HunterBodyPart.Legs);

        public PermanentInjury Resolve(HunterBodyPart bodyPart, IRandomSource random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (injuries == null)
                return null;

            var candidates = new List<PlayablePermanentInjuryDefinition>();
            long totalWeight = 0;
            foreach (PlayablePermanentInjuryDefinition definition in injuries)
            {
                if (definition == null || !definition.IsValid || definition.BodyPart != bodyPart)
                    continue;
                candidates.Add(definition);
                totalWeight = Math.Min(int.MaxValue, totalWeight + definition.DrawWeight);
            }
            if (candidates.Count == 0 || totalWeight <= 0)
                return null;

            int roll = random.Next(0, (int)totalWeight);
            foreach (PlayablePermanentInjuryDefinition definition in candidates)
            {
                roll -= definition.DrawWeight;
                if (roll < 0)
                    return definition.CreateInjury();
            }
            return candidates[candidates.Count - 1].CreateInjury();
        }

        public bool TryGet(string injuryId, out PermanentInjury injury)
        {
            injury = null;
            if (string.IsNullOrWhiteSpace(injuryId) || injuries == null)
                return false;
            PlayablePermanentInjuryDefinition definition = injuries.Find(candidate => candidate != null && candidate.IsValid && candidate.InjuryId == injuryId);
            if (definition == null)
                return false;
            injury = definition.CreateInjury();
            return true;
        }

        private bool HasValidDefinition(HunterBodyPart bodyPart)
        {
            return injuries.Exists(definition => definition != null && definition.IsValid && definition.BodyPart == bodyPart);
        }

        private bool HasUniqueIds()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlayablePermanentInjuryDefinition definition in injuries)
                if (definition != null && definition.IsValid && !ids.Add(definition.InjuryId))
                    return false;
            return true;
        }
    }
}
