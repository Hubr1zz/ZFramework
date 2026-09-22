using System;
using System.Collections.Generic;
using Config;
using GameplayBase.CombatSystem;
using SO.Boss.ActionCard;
using SO.Combat;
using UnityEngine;

namespace HuntingInDarkness.Combat
{
    [Serializable]
    public sealed class PlayableEncounterDefinition
    {
        [SerializeField] private string encounterId;
        [SerializeField] private CombatFieldRulesSO fieldRules;
        [SerializeField] private List<CharacterActionCardData> sharedHunterCards = new();
        [SerializeField] private BossConfigSO boss;

        public string EncounterId => encounterId;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(encounterId) && boss != null;

        public BattleSetup CreateSetup(BattleSetup fallback)
        {
            return new BattleSetup
            {
                FieldRules = fieldRules != null ? fieldRules : fallback?.FieldRules,
                HunterSquad = fallback?.HunterSquad != null ? new List<GameplayBase.Config.CharacterConfigSO>(fallback.HunterSquad) : new List<GameplayBase.Config.CharacterConfigSO>(),
                SharedHunterCards = sharedHunterCards.Count > 0 ? new List<CharacterActionCardData>(sharedHunterCards) : fallback?.SharedHunterCards != null ? new List<CharacterActionCardData>(fallback.SharedHunterCards) : new List<CharacterActionCardData>(),
                Boss = boss
            };
        }
    }

    [CreateAssetMenu(fileName = "PlayableEncounterCatalog", menuName = "Hunting in Darkness/Encounter Catalog")]
    public sealed class PlayableEncounterCatalog : ScriptableObject
    {
        [SerializeField] private List<PlayableEncounterDefinition> encounters = new();

        public IReadOnlyList<PlayableEncounterDefinition> Encounters => encounters;
        public bool IsConfigured
        {
            get
            {
                if (encounters == null || encounters.Count == 0) return false;
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (PlayableEncounterDefinition encounter in encounters)
                    if (encounter?.IsConfigured != true || !ids.Add(encounter.EncounterId))
                        return false;
                return true;
            }
        }

        public bool TryCreateSetup(string encounterId, BattleSetup fallback, out BattleSetup setup)
        {
            setup = null;
            if (!IsConfigured || string.IsNullOrWhiteSpace(encounterId)) return false;
            foreach (PlayableEncounterDefinition encounter in encounters)
            {
                if (encounter?.IsConfigured != true || !string.Equals(encounter.EncounterId, encounterId, StringComparison.Ordinal)) continue;
                setup = encounter.CreateSetup(fallback);
                return true;
            }
            return false;
        }
    }

    /// <summary>组合根配置、Campaign Host 消费的遭遇目录桥；后续可替换为读表 Provider。</summary>
    public static class PlayableEncounterRuntime
    {
        internal readonly struct RuntimeState
        {
            public RuntimeState(PlayableEncounterCatalog catalog, string defaultEncounterId, BattleSetup fallbackSetup)
            {
                Catalog = catalog;
                DefaultEncounterId = defaultEncounterId;
                FallbackSetup = fallbackSetup;
            }

            public PlayableEncounterCatalog Catalog { get; }
            public string DefaultEncounterId { get; }
            public BattleSetup FallbackSetup { get; }
        }

        private static PlayableEncounterCatalog catalog;
        private static BattleSetup fallbackSetup;

        public static string DefaultEncounterId { get; private set; } = "default";

        internal static RuntimeState CaptureState() => new(catalog, DefaultEncounterId, CloneSetup(fallbackSetup));

        internal static void RestoreState(RuntimeState state) => Configure(state.Catalog, state.DefaultEncounterId, state.FallbackSetup);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            catalog = null;
            fallbackSetup = null;
            DefaultEncounterId = "default";
        }

        public static void Configure(PlayableEncounterCatalog encounterCatalog, string defaultEncounterId, BattleSetup defaultSetup)
        {
            catalog = encounterCatalog;
            fallbackSetup = CloneSetup(defaultSetup);
            DefaultEncounterId = string.IsNullOrWhiteSpace(defaultEncounterId) ? "default" : defaultEncounterId.Trim();
        }

        public static bool TryCreateSetup(string encounterId, out BattleSetup setup, out string reason)
        {
            string resolvedId = string.IsNullOrWhiteSpace(encounterId) ? DefaultEncounterId : encounterId.Trim();
            if (catalog != null)
            {
                if (catalog.TryCreateSetup(resolvedId, fallbackSetup, out setup))
                {
                    reason = string.Empty;
                    return true;
                }
                setup = null;
                reason = $"遭遇目录中不存在可用配置：{resolvedId}";
                return false;
            }
            if (string.Equals(resolvedId, DefaultEncounterId, StringComparison.Ordinal) && fallbackSetup?.Boss != null)
            {
                setup = CloneSetup(fallbackSetup);
                reason = string.Empty;
                return true;
            }

            setup = null;
            reason = $"默认遭遇配置不可用：{resolvedId}";
            return false;
        }

        private static BattleSetup CloneSetup(BattleSetup source)
        {
            if (source == null) return null;
            return new BattleSetup
            {
                FieldRules = source.FieldRules,
                HunterSquad = source.HunterSquad != null ? new List<GameplayBase.Config.CharacterConfigSO>(source.HunterSquad) : new List<GameplayBase.Config.CharacterConfigSO>(),
                SharedHunterCards = source.SharedHunterCards != null ? new List<CharacterActionCardData>(source.SharedHunterCards) : new List<CharacterActionCardData>(),
                Boss = source.Boss
            };
        }
    }
}
