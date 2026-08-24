using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunt;
using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    [Serializable]
    public sealed class PlayableHuntNoiseProfile
    {
        [SerializeField] private string profileId;
        [SerializeField, Min(2)] private int deckSize = 10;
        [SerializeField, Min(0)] private int baseNoisePerHunter = 1;
        [SerializeField, Min(0)] private int maxDangerCards = 8;
        [SerializeField] private List<EventData> dangerEvents = new();

        public string ProfileId => profileId?.Trim() ?? string.Empty;
        public int MaxDangerCards => Math.Max(0, maxDangerCards);
        public bool IsEnabled => ProfileId.Length > 0;
        public bool IsConfigured => IsEnabled && deckSize >= 2 && maxDangerCards >= 0 && maxDangerCards <= deckSize && HasUniqueDangerEvents();

        public bool TryCreatePlan(IReadOnlyList<HunterInstance> hunters, out NoiseCheckPlan plan)
        {
            plan = default;
            if (!IsConfigured) return false;
            int livingHunterCount = 0;
            var equipmentNoiseValues = new List<int>();
            if (hunters != null)
                foreach (HunterInstance hunter in hunters)
                {
                    if (hunter?.IsAlive != true) continue;
                    livingHunterCount++;
                    if (hunter.Equipment == null) continue;
                    foreach (ItemInstance item in hunter.Equipment)
                    {
                        if (item?.Data == null) continue;
                        int count = Math.Max(1, item.Count);
                        equipmentNoiseValues.Add((int)Math.Min(int.MaxValue, (long)item.Data.HuntNoise * count));
                    }
                }
            plan = HuntNoiseRules.CreatePlan(livingHunterCount, equipmentNoiseValues, new HuntNoiseDefinition(deckSize, baseNoisePerHunter, maxDangerCards));
            return plan.IsEnabled;
        }

        public IReadOnlyList<EventData> GetEligibleDangerEvents(int currentYear)
        {
            var available = new List<EventData>();
            foreach (EventData gameEvent in dangerEvents)
                if (gameEvent != null && gameEvent.minYear <= currentYear && (gameEvent.maxYear <= 0 || currentYear <= gameEvent.maxYear))
                    available.Add(gameEvent);
            return available;
        }

        public bool TryValidateContinuousCoverage(int firstYear, out int firstMissingYear)
        {
            int nextYear = Math.Max(1, firstYear);
            if (dangerEvents == null)
            {
                firstMissingYear = nextYear;
                return false;
            }
            var orderedEvents = dangerEvents.FindAll(gameEvent => gameEvent != null);
            orderedEvents.Sort((left, right) => left.minYear.CompareTo(right.minYear));
            foreach (EventData gameEvent in orderedEvents)
            {
                int eventFirstYear = Math.Max(1, gameEvent.minYear);
                int eventLastYear = gameEvent.maxYear <= 0 ? int.MaxValue : gameEvent.maxYear;
                if (eventLastYear < nextYear) continue;
                if (eventFirstYear > nextYear)
                {
                    firstMissingYear = nextYear;
                    return false;
                }
                if (eventLastYear == int.MaxValue)
                {
                    firstMissingYear = 0;
                    return true;
                }
                nextYear = Math.Max(nextYear, eventLastYear + 1);
            }

            firstMissingYear = nextYear;
            return false;
        }

        private bool HasUniqueDangerEvents()
        {
            if (dangerEvents == null || dangerEvents.Count == 0) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (EventData gameEvent in dangerEvents)
                if (gameEvent == null || !gameEvent.HasExplicitContentId || gameEvent.category != EventCategory.Hunt || gameEvent.drawWeight <= 0 || !ids.Add(gameEvent.ContentId))
                    return false;
            return true;
        }
    }

    public readonly struct PlayableHuntNoiseResolution
    {
        public PlayableHuntNoiseResolution(string interactionId, string destinationId, NoiseCheckPlan plan, int cardValue, EventData selectedEvent)
        {
            InteractionId = interactionId ?? string.Empty;
            DestinationId = destinationId ?? string.Empty;
            Plan = plan;
            CardValue = cardValue;
            SelectedEvent = selectedEvent;
        }

        public string InteractionId { get; }
        public string DestinationId { get; }
        public NoiseCheckPlan Plan { get; }
        public int CardValue { get; }
        public EventData SelectedEvent { get; }
        public bool HasValidCard => Plan.IsEnabled && CardValue >= 1 && CardValue <= Plan.DeckSize;
        public bool IsDanger => HasValidCard && Plan.IsDangerCard(CardValue);
        public bool IsResolved => HasValidCard && (!IsDanger || SelectedEvent != null);
        public string EventId => SelectedEvent != null ? SelectedEvent.ContentId : string.Empty;
        public string EventDisplayName => SelectedEvent != null ? SelectedEvent.eventName : string.Empty;
    }
}
