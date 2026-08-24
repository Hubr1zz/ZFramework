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
            if (huntContent == null)
            {
                reason = "这个目的地尚未准备好。";
                return false;
            }
            if (!huntContent.IsAvailableForYear(currentYear, out reason))
            {
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

        public bool TryGetById(string destinationId, out PlayableHuntDestination result)
        {
            string normalizedId = destinationId?.Trim() ?? string.Empty;
            foreach (PlayableHuntDestination destination in destinations)
                if (destination != null && string.Equals(destination.DestinationId, normalizedId, StringComparison.Ordinal))
                {
                    result = destination;
                    return true;
                }
            result = null;
            return false;
        }
    }

    /// <summary>保存当前一次狩猎的路线选择，并把所选 Unity 内容映射给既有 Hunt Adapter。</summary>
    public static class PlayableHuntDestinationRuntime
    {
        internal readonly struct RuntimeState
        {
            public RuntimeState(PlayableHuntDestinationCatalog catalog, PlayableHuntContentCatalog fallbackContent, PlayableHuntDestination activeDestination, PlayableHuntContentBundle contentBundle, PlayableHuntRoutePlan activePlan)
            {
                Catalog = catalog;
                FallbackContent = fallbackContent;
                ActiveDestination = activeDestination;
                ContentBundle = contentBundle;
                ActivePlan = activePlan;
            }

            public PlayableHuntDestinationCatalog Catalog { get; }
            public PlayableHuntContentCatalog FallbackContent { get; }
            public PlayableHuntDestination ActiveDestination { get; }
            public PlayableHuntContentBundle ContentBundle { get; }
            public PlayableHuntRoutePlan ActivePlan { get; }
        }

        private static PlayableHuntDestinationCatalog catalog;
        private static PlayableHuntContentCatalog fallbackContent;
        private static PlayableHuntContentBundle contentBundle;
        private static PlayableHuntRoutePlan activePlan;

        public static PlayableHuntDestination ActiveDestination { get; private set; }
        public static PlayableHuntRoutePlan ActiveRoutePlan => activePlan ?? contentBundle?.DefaultRoute;
        public static string ActiveDisplayName => ActiveDestination?.DisplayName ?? "未知地域";
        public static PlayableHuntDestinationCatalog Catalog => catalog;

        internal static RuntimeState CaptureState() => new(catalog, fallbackContent, ActiveDestination, contentBundle, activePlan);

        internal static void RestoreState(RuntimeState state)
        {
            catalog = state.Catalog;
            fallbackContent = state.FallbackContent;
            ActiveDestination = state.ActiveDestination;
            contentBundle = state.ContentBundle;
            activePlan = state.ActivePlan;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            catalog = null;
            fallbackContent = null;
            contentBundle = null;
            activePlan = null;
            ActiveDestination = null;
        }

        public static void Configure(PlayableHuntDestinationCatalog destinationCatalog, PlayableHuntContentCatalog defaultContent)
        {
            catalog = destinationCatalog;
            fallbackContent = defaultContent;
            ActiveDestination = null;
            contentBundle = null;
            activePlan = null;
            PlayableHuntContentRuntime.Configure(defaultContent);
        }

        internal static void Configure(PlayableHuntDestinationCatalog destinationCatalog, PlayableHuntContentCatalog defaultContent, PlayableHuntContentBundle preparedBundle)
        {
            catalog = destinationCatalog;
            fallbackContent = defaultContent;
            ActiveDestination = null;
            contentBundle = preparedBundle;
            activePlan = null;
        }

        public static bool TrySelect(PlayableHuntDestination destination, int currentYear, out string reason)
        {
            if (!CanSelect(destination, currentYear, out reason)) return false;

            PlayableHuntRoutePlan resolvedPlan = null;
            if (contentBundle != null && !contentBundle.TryResolveRoute(destination.DestinationId, currentYear, out resolvedPlan, out reason)) return false;
            ActiveDestination = destination;
            activePlan = resolvedPlan;
            if (contentBundle != null && activePlan == null)
            {
                ActiveDestination = null;
                reason = "目的地内容计划尚未准备。";
                return false;
            }
            if (contentBundle == null) PlayableHuntContentRuntime.Configure(destination.HuntContent);
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
            if (contentBundle != null)
            {
                if (!contentBundle.TryResolveRoute(destination.DestinationId, currentYear, out PlayableHuntRoutePlan route, out reason)) return false;
                if (ReferenceEquals(route.Destination, destination)) return true;
                reason = "这个目的地不属于当前内容 Bundle。";
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
            if (contentBundle != null)
            {
                if (contentBundle.HasSelectableDestinations)
                {
                    reason = "请选择狩猎目的地。";
                    return false;
                }
                return contentBundle.TryResolveRoute(string.Empty, currentYear, out _, out reason);
            }
            if (catalog != null && catalog.GetAvailable(currentYear).Count > 0)
            {
                reason = "请选择狩猎目的地。";
                return false;
            }
            if (fallbackContent == null)
            {
                reason = "默认狩猎内容尚未配置。";
                return false;
            }
            return fallbackContent.IsAvailableForYear(currentYear, out reason);
        }

        public static bool TrySelectForDeparture(PlayableHuntDestination destination, int currentYear, out string reason)
        {
            if (destination != null) return TrySelect(destination, currentYear, out reason);
            if (!CanSelectForDeparture(null, currentYear, out reason)) return false;
            RestoreSelection(null);
            reason = string.Empty;
            return true;
        }

        public static bool TryResolveRouteForDeparture(PlayableHuntDestination destination, int currentYear, out PlayableHuntRoutePlan route, out string reason)
        {
            route = null;
            if (!CanSelectForDeparture(destination, currentYear, out reason)) return false;
            if (contentBundle == null)
            {
                reason = "狩猎内容 Bundle 尚未安装。";
                return false;
            }
            return contentBundle.TryResolveRoute(destination?.DestinationId, currentYear, out route, out reason);
        }

        public static bool TryResolveRouteForRestore(string destinationId, int year, string contentBundleId, out PlayableHuntRoutePlan route, out string reason)
        {
            route = null;
            if (contentBundle == null || !ReferenceEquals(contentBundle, PlayableHuntContentRuntime.CurrentBundle))
            {
                reason = "狩猎内容 Bundle 尚未发布或已经变化。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(contentBundleId) || !string.Equals(contentBundle.BundleId, contentBundleId.Trim(), StringComparison.Ordinal))
            {
                reason = "活动狩猎存档与当前内容 Bundle 不兼容。";
                return false;
            }
            return contentBundle.TryResolveRoute(destinationId, year, out route, out reason);
        }

        internal static bool TryCommitRoute(PlayableHuntRoutePlan route, out string reason)
        {
            if (route?.IsUsable != true || contentBundle == null || !ReferenceEquals(contentBundle, PlayableHuntContentRuntime.CurrentBundle) || !contentBundle.Owns(route))
            {
                reason = "不能提交已经失效或不属于当前内容 Bundle 的狩猎路线。";
                return false;
            }
            activePlan = route;
            ActiveDestination = route.Destination;
            reason = string.Empty;
            return true;
        }

        public static void RestoreSelection(PlayableHuntDestination destination)
        {
            if (destination != null && catalog != null && ContainsReference(catalog.Destinations, destination))
            {
                ActiveDestination = destination;
                activePlan = contentBundle != null && contentBundle.TryResolveRoute(destination.DestinationId, destination.MinimumYear, out PlayableHuntRoutePlan resolvedPlan, out _) ? resolvedPlan : null;
                if (contentBundle == null) PlayableHuntContentRuntime.Configure(destination.HuntContent);
                return;
            }

            ActiveDestination = null;
            activePlan = null;
            if (contentBundle == null) PlayableHuntContentRuntime.Configure(fallbackContent);
        }

        public static bool TryRestoreSelection(string destinationId, out string reason)
        {
            string normalizedId = destinationId?.Trim() ?? string.Empty;
            if (normalizedId.Length == 0)
            {
                RestoreSelection(null);
                bool hasFallback = contentBundle?.DefaultRoute?.IsUsable == true || fallbackContent != null;
                reason = hasFallback ? string.Empty : "默认狩猎内容尚未配置。";
                return hasFallback;
            }
            if (catalog == null || !catalog.TryGetById(normalizedId, out PlayableHuntDestination destination))
            {
                reason = $"狩猎目的地内容缺失：{normalizedId}";
                return false;
            }
            RestoreSelection(destination);
            reason = string.Empty;
            return true;
        }

        public static void ApplyTo(HuntManager manager)
        {
            TryApplyTo(manager, out _);
        }

        public static bool TryApplyTo(HuntManager manager, out string reason)
        {
            if (manager == null)
            {
                reason = "狩猎管理器为空。";
                return false;
            }
            if (contentBundle != null)
            {
                return manager.TryBindContent(activePlan ?? contentBundle.DefaultRoute, out reason);
            }
            if (ActiveDestination == null) PlayableHuntContentRuntime.Configure(fallbackContent);
            PlayableHuntContentRuntime.ApplyTo(manager);
            bool configured = manager.StartingTileConfig != null && manager.TilePool.Count > 0 && manager.NoiseProfile?.IsConfigured == true;
            reason = configured ? string.Empty : "兼容狩猎内容未完整配置。";
            return configured;
        }

        private static bool ContainsReference(IReadOnlyList<PlayableHuntDestination> destinations, PlayableHuntDestination target)
        {
            if (destinations == null) return false;
            foreach (PlayableHuntDestination destination in destinations)
                if (ReferenceEquals(destination, target))
                    return true;
            return false;
        }

        internal static void ReleaseBundle(PlayableHuntContentBundle bundle)
        {
            if (bundle == null || !ReferenceEquals(contentBundle, bundle)) return;
            contentBundle = null;
            activePlan = null;
            ActiveDestination = null;
        }
    }
}
