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

        public IReadOnlyList<EventData> Events => events;
        public bool IsConfigured => events.Exists(gameEvent => gameEvent != null && gameEvent.drawWeight > 0);
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
            EventData gameEvent = DrawEvent();
            if (hunter == null || eventSystem == null || gameEvent == null) return;

            string resultText = string.IsNullOrWhiteSpace(gameEvent.hiddenText) ? string.Empty : $"\n\n<color=#e8c46a>{gameEvent.hiddenText}</color>";
            await input.ShowResult($"【侥幸生还 · {gameEvent.eventName}】\n\n{gameEvent.displayText}{resultText}", cancellationToken);
            foreach (EventEffect effect in gameEvent.immediateEffects)
                eventSystem.ApplyEffect(effect, hunter);
            EventBus.Publish(new SurvivalEventResolvedEvent { CharacterId = characterId, EventName = gameEvent.eventName });
        }

        private EventData DrawEvent()
        {
            IReadOnlyList<EventData> configured = PlayableSurvivalEventRuntime.Catalog?.Events;
            if (configured == null) return null;

            var candidates = new List<EventData>();
            foreach (EventData gameEvent in configured)
                if (gameEvent != null && gameEvent.drawWeight > 0)
                    candidates.Add(gameEvent);
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
