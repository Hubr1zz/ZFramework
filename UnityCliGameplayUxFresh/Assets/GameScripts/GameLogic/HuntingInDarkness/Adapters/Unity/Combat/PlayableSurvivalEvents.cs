using System;
using System.Collections.Generic;
using System.Threading;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase.CombatSystem;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Combat
{
    [CreateAssetMenu(fileName = "PlayableSurvivalEventCatalog", menuName = "Hunting in Darkness/Playable Survival Event Catalog")]
    public sealed class PlayableSurvivalEventCatalog : ScriptableObject
    {
        [SerializeField] private List<EventData> events = new();
        [SerializeField] private List<PlayableTraitSurvivalEventAddition> traitEventAdditions = new();
        [SerializeField] private List<PlayableSurvivalCardDefinition> survivalCards = new();

        public IReadOnlyList<EventData> Events => events;
        public bool IsConfigured => events.Exists(gameEvent => gameEvent != null && gameEvent.drawWeight > 0);

        public List<EventData> GetEventCandidates(IReadOnlyList<string> traits)
        {
            var result = new List<EventData>();
            foreach (EventData gameEvent in events)
                if (gameEvent != null && gameEvent.drawWeight > 0)
                    result.Add(gameEvent);
            foreach (PlayableTraitSurvivalEventAddition addition in traitEventAdditions)
                if (addition?.survivalEvent != null && addition.survivalEvent.drawWeight > 0 && HasTrait(traits, addition.requiredTraitId) && !result.Contains(addition.survivalEvent))
                    result.Add(addition.survivalEvent);
            return result;
        }

        public IReadOnlyList<string> GetSurvivalCardIds(IReadOnlyList<string> traits)
        {
            var result = new List<string>();
            foreach (PlayableSurvivalCardDefinition definition in survivalCards)
                if (definition?.survivalEvent != null && HasTrait(traits, definition.requiredTraitId))
                    result.Add(GetCardId(definition));
            return result;
        }

        public bool TryGetSurvivalCardEvent(string survivalCardId, out EventData gameEvent)
        {
            foreach (PlayableSurvivalCardDefinition definition in survivalCards)
            {
                if (definition?.survivalEvent == null || !string.Equals(GetCardId(definition), survivalCardId, StringComparison.Ordinal)) continue;
                gameEvent = definition.survivalEvent;
                return true;
            }
            gameEvent = null;
            return false;
        }

        private static string GetCardId(PlayableSurvivalCardDefinition definition) => string.IsNullOrWhiteSpace(definition.survivalCardId) ? definition.survivalEvent.name : definition.survivalCardId.Trim();

        private static bool HasTrait(IReadOnlyList<string> traits, string requiredTraitId)
        {
            if (string.IsNullOrWhiteSpace(requiredTraitId)) return true;
            if (traits == null) return false;
            foreach (string trait in traits)
                if (string.Equals(trait?.Trim(), requiredTraitId.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }

    [Serializable]
    public sealed class PlayableTraitSurvivalEventAddition
    {
        public string requiredTraitId;
        public EventData survivalEvent;
    }

    [Serializable]
    public sealed class PlayableSurvivalCardDefinition
    {
        public string survivalCardId;
        public string requiredTraitId;
        public EventData survivalEvent;
    }

    public interface ISurvivalEventResolver
    {
        UniTask ResolveAsync(int characterId, HunterDamageResult damage, IPlayerInputProvider input, CancellationToken cancellationToken = default);
    }

    public static class PlayableSurvivalEventRuntime
    {
        public static PlayableSurvivalEventCatalog Catalog { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState() => Catalog = null;

        public static void Configure(PlayableSurvivalEventCatalog catalog) => Catalog = catalog;
    }

    /// <summary>在战斗输入层展示存活事件，并复用营地事件效果规则写回长期猎人状态。</summary>
    public sealed class PlayableSurvivalEventResolver : ISurvivalEventResolver
    {
        private readonly Func<int, HunterInstance> getHunter;
        private readonly Func<EventSystem> getEventSystem;
        private readonly IRandomSource random;

        public PlayableSurvivalEventResolver(Func<int, HunterInstance> getHunter, Func<EventSystem> getEventSystem, IRandomSource random = null)
        {
            this.getHunter = getHunter ?? throw new ArgumentNullException(nameof(getHunter));
            this.getEventSystem = getEventSystem ?? throw new ArgumentNullException(nameof(getEventSystem));
            this.random = random ?? new SystemRandomSource();
        }

        public async UniTask ResolveAsync(int characterId, HunterDamageResult damage, IPlayerInputProvider input, CancellationToken cancellationToken = default)
        {
            if (!damage.FatalInjuryTriggered || damage.IsDead || input == null) return;

            HunterInstance hunter = getHunter(characterId);
            EventSystem eventSystem = getEventSystem();
            string survivalCardId = damage.DeathDraw?.SurvivalEventId;
            EventData gameEvent = ResolveEvent(hunter, survivalCardId);
            if (hunter == null || eventSystem == null || gameEvent == null) return;

            string resultText = string.IsNullOrWhiteSpace(gameEvent.hiddenText) ? string.Empty : $"\n\n<color=#e8c46a>{gameEvent.hiddenText}</color>";
            string source = string.IsNullOrWhiteSpace(survivalCardId) ? "存活事件" : "生存卡";
            await input.ShowResult($"【{source} · {gameEvent.eventName}】\n\n{gameEvent.displayText}{resultText}", cancellationToken);
            foreach (EventEffect effect in gameEvent.immediateEffects)
                eventSystem.ApplyEffect(effect, hunter);
            EventBus.Publish(new SurvivalEventResolvedEvent { CharacterId = characterId, EventName = gameEvent.eventName });
        }

        private EventData ResolveEvent(HunterInstance hunter, string survivalCardId)
        {
            PlayableSurvivalEventCatalog catalog = PlayableSurvivalEventRuntime.Catalog;
            if (catalog == null) return null;
            if (!string.IsNullOrWhiteSpace(survivalCardId) && catalog.TryGetSurvivalCardEvent(survivalCardId, out EventData survivalCardEvent))
                return survivalCardEvent;

            List<EventData> candidates = catalog.GetEventCandidates(hunter?.Traits);
            List<EventData> selected = WeightedSelection.DrawWithoutReplacement(candidates, 1, gameEvent => gameEvent.drawWeight, random);
            return selected.Count > 0 ? selected[0] : null;
        }
    }

    public struct SurvivalEventResolvedEvent
    {
        public int CharacterId;
        public string EventName;
    }
}
