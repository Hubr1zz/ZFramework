using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Hunt;
using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    [Serializable]
    public sealed class PlayableHuntDestination
    {
        [SerializeField] private string destinationId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private string resourceHint;
        [SerializeField] private string dangerHint;
        [SerializeField, Min(1)] private int minimumYear = 1;
        [SerializeField] private PlayableHuntContentCatalog huntContent;

        public string DestinationId => destinationId;
        public string DisplayName => displayName;
        public string Description => description;
        public string ResourceHint => resourceHint;
        public string DangerHint => dangerHint;
        public int MinimumYear => Mathf.Max(1, minimumYear);
        public PlayableHuntContentCatalog HuntContent => huntContent;
        public bool IsConfigured => HuntDestinationRules.CanSelect(destinationId, displayName, MinimumYear, MinimumYear, out _) && huntContent != null && huntContent.IsConfigured;

        public bool IsAvailable(int currentYear, out string reason)
        {
            if (huntContent == null || !huntContent.IsConfigured)
            {
                reason = "这个目的地尚未准备好。";
                return false;
            }

            return HuntDestinationRules.CanSelect(destinationId, displayName, currentYear, MinimumYear, out reason);
        }
    }

    [CreateAssetMenu(fileName = "PlayableHuntDestinationCatalog", menuName = "Hunting in Darkness/Hunt Destination Catalog")]
    public sealed class PlayableHuntDestinationCatalog : ScriptableObject
    {
        [SerializeField] private List<PlayableHuntDestination> destinations = new();

        public IReadOnlyList<PlayableHuntDestination> Destinations => destinations;
        public bool IsConfigured => destinations.Exists(destination => destination != null && destination.IsConfigured);

        public List<PlayableHuntDestination> GetAvailable(int currentYear)
        {
            var result = new List<PlayableHuntDestination>();
            foreach (PlayableHuntDestination destination in destinations)
                if (destination != null && destination.IsAvailable(currentYear, out _))
                    result.Add(destination);
            return result;
        }
    }

    /// <summary>保存当前一次狩猎的路线选择，并把所选 Unity 内容映射给既有 Hunt Adapter。</summary>
    public static class PlayableHuntDestinationRuntime
    {
        private static PlayableHuntDestinationCatalog catalog;
        private static PlayableHuntContentCatalog fallbackContent;

        public static PlayableHuntDestination ActiveDestination { get; private set; }
        public static string ActiveDisplayName => ActiveDestination?.DisplayName ?? "未知地域";
        public static PlayableHuntDestinationCatalog Catalog => catalog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            catalog = null;
            fallbackContent = null;
            ActiveDestination = null;
        }

        public static void Configure(PlayableHuntDestinationCatalog destinationCatalog, PlayableHuntContentCatalog defaultContent)
        {
            catalog = destinationCatalog;
            fallbackContent = defaultContent;
            ActiveDestination = null;
            PlayableHuntContentRuntime.Configure(defaultContent);
        }

        public static bool TrySelect(PlayableHuntDestination destination, int currentYear, out string reason)
        {
            if (!CanSelect(destination, currentYear, out reason)) return false;

            ActiveDestination = destination;
            PlayableHuntContentRuntime.Configure(destination.HuntContent);
            reason = string.Empty;
            return true;
        }

        public static bool CanSelect(PlayableHuntDestination destination, int currentYear, out string reason)
        {
            if (destination == null)
            {
                reason = "没有选择狩猎目的地。";
                return false;
            }
            if (!destination.IsAvailable(currentYear, out reason)) return false;
            if (catalog != null && ContainsReference(catalog.Destinations, destination))
            {
                reason = string.Empty;
                return true;
            }

            reason = "这个目的地不属于当前战役。";
            return false;
        }

        public static bool CanSelectForDeparture(PlayableHuntDestination destination, int currentYear, out string reason)
        {
            if (destination != null) return CanSelect(destination, currentYear, out reason);
            if (catalog != null && catalog.GetAvailable(currentYear).Count > 0)
            {
                reason = "请选择狩猎目的地。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static bool TrySelectForDeparture(PlayableHuntDestination destination, int currentYear, out string reason)
        {
            if (destination != null) return TrySelect(destination, currentYear, out reason);
            if (!CanSelectForDeparture(null, currentYear, out reason)) return false;
            RestoreSelection(null);
            reason = string.Empty;
            return true;
        }

        public static void RestoreSelection(PlayableHuntDestination destination)
        {
            if (destination != null && catalog != null && ContainsReference(catalog.Destinations, destination))
            {
                ActiveDestination = destination;
                PlayableHuntContentRuntime.Configure(destination.HuntContent);
                return;
            }

            ActiveDestination = null;
            PlayableHuntContentRuntime.Configure(fallbackContent);
        }

        public static void ApplyTo(HuntManager manager)
        {
            if (ActiveDestination == null)
                PlayableHuntContentRuntime.Configure(fallbackContent);
            PlayableHuntContentRuntime.ApplyTo(manager);
        }

        private static bool ContainsReference(IReadOnlyList<PlayableHuntDestination> destinations, PlayableHuntDestination target)
        {
            if (destinations == null) return false;
            foreach (PlayableHuntDestination destination in destinations)
                if (ReferenceEquals(destination, target))
                    return true;
            return false;
        }
    }
}
