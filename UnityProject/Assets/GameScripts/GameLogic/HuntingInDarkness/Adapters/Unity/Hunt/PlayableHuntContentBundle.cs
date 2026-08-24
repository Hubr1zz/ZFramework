using System;
using System.Collections.Generic;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    public sealed class PlayableHuntRoutePlan
    {
        internal PlayableHuntRoutePlan(PlayableHuntContentBundle owner, PlayableHuntDestination destination, string destinationId, int minimumYear, HexTileData startingTile, IReadOnlyList<HexTileData> tilePool, IReadOnlyList<EventData> huntEvents, PlayableHuntNoiseProfile noiseProfile)
        {
            Owner = owner;
            Destination = destination;
            DestinationId = destinationId?.Trim() ?? string.Empty;
            MinimumYear = Math.Max(1, minimumYear);
            StartingTile = startingTile;
            TilePool = Freeze(tilePool);
            HuntEvents = Freeze(huntEvents);
            NoiseProfile = noiseProfile;
        }

        internal PlayableHuntContentBundle Owner { get; }
        internal PlayableHuntDestination Destination { get; }
        public string DestinationId { get; }
        public int MinimumYear { get; }
        public string ContentBundleId => Owner?.BundleId ?? string.Empty;
        public HexTileData StartingTile { get; }
        public IReadOnlyList<HexTileData> TilePool { get; }
        public IReadOnlyList<EventData> HuntEvents { get; }
        public PlayableHuntNoiseProfile NoiseProfile { get; }
        public bool IsUsable => Owner?.IsUsable == true && StartingTile != null && TilePool.Count > 0 && NoiseProfile?.IsConfigured == true;

        private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values) => new List<T>(values ?? Array.Empty<T>()).AsReadOnly();
    }

    /// <summary>与事件世代、营地 Registry 同时准备和发布的战役级狩猎内容快照。</summary>
    public sealed class PlayableHuntContentBundle : IDisposable
    {
        private readonly Dictionary<string, PlayableHuntRoutePlan> routesById = new(StringComparer.Ordinal);
        private readonly List<UnityEngine.Object> ownedObjects = new();
        private bool retired;

        private PlayableHuntContentBundle(PlayableEventTableGeneration eventGeneration, PlayableSettlementRegistryBundle registryBundle)
        {
            EventGeneration = eventGeneration ?? throw new ArgumentNullException(nameof(eventGeneration));
            RegistryBundle = registryBundle ?? throw new ArgumentNullException(nameof(registryBundle));
        }

        public string BundleId { get; private set; }
        public PlayableHuntRoutePlan DefaultRoute { get; private set; }
        public bool HasSelectableDestinations => routesById.Count > 0;
        public bool IsUsable => !retired && EventGeneration.IsUsable;
        internal PlayableEventTableGeneration EventGeneration { get; }
        internal PlayableSettlementRegistryBundle RegistryBundle { get; }

        internal static bool TryCreate(PlayableHuntContentCatalog defaultContent, IReadOnlyList<PlayableHuntDestination> destinations, PlayableEventTableGeneration eventGeneration, PlayableSettlementRegistryBundle registryBundle, out PlayableHuntContentBundle bundle, out string reason)
        {
            bundle = null;
            if (eventGeneration?.IsUsable != true || registryBundle == null)
            {
                reason = "狩猎内容依赖的事件世代或物品 Registry 不可用。";
                return false;
            }
            var candidate = new PlayableHuntContentBundle(eventGeneration, registryBundle);
            try
            {
                if (!candidate.TryCreateRoute(null, string.Empty, 1, defaultContent, out PlayableHuntRoutePlan defaultRoute, out reason))
                {
                    candidate.Dispose();
                    return false;
                }
                candidate.DefaultRoute = defaultRoute;
                foreach (PlayableHuntDestination destination in destinations ?? Array.Empty<PlayableHuntDestination>())
                {
                    if (destination == null || !candidate.TryCreateRoute(destination, destination.DestinationId, destination.MinimumYear, destination.HuntContent, out PlayableHuntRoutePlan route, out reason))
                    {
                        candidate.Dispose();
                        return false;
                    }
                    if (!candidate.routesById.TryAdd(route.DestinationId, route))
                    {
                        reason = $"狩猎目的地稳定 ID 重复：{route.DestinationId}";
                        candidate.Dispose();
                        return false;
                    }
                }
                candidate.BundleId = candidate.BuildManifestId();
                bundle = candidate;
                reason = string.Empty;
                return true;
            }
            catch
            {
                candidate.Dispose();
                throw;
            }
        }

        /// <summary>兼容环境使用当前已发布内容世代创建快照；正式启动事务使用 staged generation 重载。</summary>
        public static bool TryCreateSnapshot(PlayableHuntContentCatalog defaultContent, IReadOnlyList<PlayableHuntDestination> destinations, out PlayableHuntContentBundle bundle, out string reason)
        {
            PlayableEventTableRuntime.GetEvents();
            return TryCreate(defaultContent, destinations, PlayableEventTableRuntime.CurrentGeneration, PlayableSettlementContentRuntime.RegistryBundle, out bundle, out reason);
        }

        public bool TryResolveRoute(string destinationId, int currentYear, out PlayableHuntRoutePlan route, out string reason)
        {
            route = null;
            if (!IsUsable)
            {
                reason = "狩猎内容世代已经退役。";
                return false;
            }
            string normalizedId = destinationId?.Trim() ?? string.Empty;
            if (normalizedId.Length == 0)
                route = DefaultRoute;
            else if (!routesById.TryGetValue(normalizedId, out route))
            {
                reason = $"未知狩猎目的地：{normalizedId}";
                return false;
            }
            if (currentYear < route.MinimumYear)
            {
                reason = $"目的地 {normalizedId} 最早在第 {route.MinimumYear} 年开放。";
                route = null;
                return false;
            }
            reason = string.Empty;
            return true;
        }

        internal bool Owns(PlayableHuntRoutePlan route) => route != null && ReferenceEquals(route.Owner, this);
        internal bool Leases(PlayableEventTableGeneration generation) => !retired && ReferenceEquals(EventGeneration, generation);

        public void Dispose()
        {
            if (ReferenceEquals(PlayableHuntContentRuntime.CurrentBundle, this))
            {
                Debug.LogError("[PlayableHuntContent] 当前发布的 Hunt Bundle 不能由外部直接退役。");
                return;
            }
            Retire();
        }

        internal void Retire()
        {
            if (retired) return;
            retired = true;
            Exception firstException = null;
            foreach (UnityEngine.Object ownedObject in ownedObjects)
            {
                if (ownedObject == null) continue;
                try
                {
                    DestroyOwnedObject(ownedObject);
                }
                catch (Exception exception)
                {
                    firstException ??= exception;
                }
            }
            ownedObjects.Clear();
            routesById.Clear();
            DefaultRoute = null;
            if (firstException != null) throw firstException;
        }

        private bool TryCreateRoute(PlayableHuntDestination destination, string destinationId, int minimumYear, PlayableHuntContentCatalog catalog, out PlayableHuntRoutePlan route, out string reason)
        {
            route = null;
            if (catalog == null || !catalog.IsConfigured)
            {
                reason = $"狩猎路线 {destinationId} 的内容目录未完整配置。";
                return false;
            }
            if (!TryCloneTile(catalog.StartingTile, out HexTileData startingTile, out reason)) return false;
            var tilePool = new List<HexTileData>();
            var knownTileIds = new HashSet<string>(StringComparer.Ordinal) { startingTile.ContentId };
            foreach (HexTileData sourceTile in catalog.TilePool)
            {
                if (sourceTile == null) continue;
                if (!TryCloneTile(sourceTile, out HexTileData tile, out reason)) return false;
                if (!knownTileIds.Add(tile.ContentId))
                {
                    reason = $"狩猎路线 {destinationId} 的地块稳定 ID 重复：{tile.ContentId}";
                    return false;
                }
                tilePool.Add(tile);
            }
            if (tilePool.Count == 0)
            {
                reason = $"狩猎路线 {destinationId} 没有可生成地块。";
                return false;
            }
            List<EventData> huntEvents = PlayableEventTableRuntime.ExtendHunt(catalog.EventPool, EventGeneration.Events);
            var canonicalEvents = new Dictionary<string, EventData>(StringComparer.Ordinal);
            foreach (EventData gameEvent in huntEvents) canonicalEvents[gameEvent.ContentId] = gameEvent;
            PlayableHuntNoiseProfile noiseProfile = catalog.NoiseProfile.CreateSnapshot(canonicalEvents);
            if (!ValidateEvents(startingTile, tilePool, huntEvents, noiseProfile, minimumYear, out reason)) return false;
            route = new PlayableHuntRoutePlan(this, destination, destinationId, minimumYear, startingTile, tilePool, huntEvents, noiseProfile);
            return true;
        }

        private bool TryCloneTile(HexTileData source, out HexTileData clone, out string reason)
        {
            clone = null;
            if (source == null || !source.HasExplicitContentId || source.spawnWeight <= 0 || source.groupSize <= 0 || source.maxResourcePoints < 0)
            {
                reason = $"狩猎地块缺少显式稳定 ID 或生成规则无效：{source?.name}";
                return false;
            }
            clone = ScriptableObject.CreateInstance<HexTileData>();
            clone.name = $"{source.name} (Hunt Snapshot)";
            clone.ConfigureContentId(source.ContentId);
            clone.tileName = source.tileName;
            clone.tileType = source.tileType;
            clone.description = source.description;
            clone.tileRevealEvent = source.tileRevealEvent;
            clone.tileRule = source.tileRule;
            clone.spawnWeight = source.spawnWeight;
            clone.spawnInGroup = source.spawnInGroup;
            clone.groupSize = source.groupSize;
            clone.mustBeAdjacent = source.mustBeAdjacent;
            clone.maxResourcePoints = source.maxResourcePoints;
            clone.bossEncounterWeight = source.bossEncounterWeight;
            clone.bossEncounterId = source.bossEncounterId;
            clone.resourcePoints = new List<ResourcePointConfig>();
            foreach (ResourcePointConfig point in source.resourcePoints ?? new List<ResourcePointConfig>())
            {
                if (point?.resource == null || point.resource.itemType != ItemType.Resource || point.spawnWeight <= 0 || point.drawCount <= 0 || point.maxPerTile < 0 || !RegistryBundle.TryGetItem(point.resource.ContentId, out ItemData registered) || !ReferenceEquals(registered, point.resource))
                {
                    reason = $"狩猎地块 {source.ContentId} 引用了计划外资源或无效生成规则。";
                    DestroyOwnedObject(clone);
                    clone = null;
                    return false;
                }
                clone.resourcePoints.Add(new ResourcePointConfig { resource = point.resource, spawnWeight = point.spawnWeight, drawCount = point.drawCount, maxPerTile = point.maxPerTile });
            }
            ownedObjects.Add(clone);
            reason = string.Empty;
            return true;
        }

        private static bool ValidateEvents(HexTileData startingTile, IReadOnlyList<HexTileData> tilePool, IReadOnlyList<EventData> huntEvents, PlayableHuntNoiseProfile noiseProfile, int firstYear, out string reason)
        {
            var knownEvents = new HashSet<EventData>(huntEvents ?? Array.Empty<EventData>());
            if (startingTile.tileRevealEvent != null && !knownEvents.Contains(startingTile.tileRevealEvent))
            {
                reason = $"起始地块引用了路线事件池之外的事件：{startingTile.tileRevealEvent.ContentId}";
                return false;
            }
            foreach (HexTileData tile in tilePool)
                if (tile.tileRevealEvent != null && !knownEvents.Contains(tile.tileRevealEvent))
                {
                    reason = $"地块 {tile.ContentId} 引用了路线事件池之外的事件：{tile.tileRevealEvent.ContentId}";
                    return false;
                }
            foreach (EventData gameEvent in huntEvents)
                if (gameEvent == null || !gameEvent.HasExplicitContentId || gameEvent.category != EventCategory.Hunt)
                {
                    reason = "狩猎事件缺少显式稳定 ID 或类别不是 Hunt。";
                    return false;
                }
            foreach (EventData dangerEvent in noiseProfile?.GetDangerEvents() ?? Array.Empty<EventData>())
                if (dangerEvent == null || !knownEvents.Contains(dangerEvent))
                {
                    reason = $"噪音牌堆引用了路线事件池之外的事件：{dangerEvent?.ContentId}";
                    return false;
                }
            int missingYear = firstYear;
            if (noiseProfile?.IsConfigured != true || !noiseProfile.TryValidateContinuousCoverage(firstYear, out missingYear))
            {
                reason = $"狩猎噪音事件从第 {missingYear} 年起没有连续覆盖。";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private string BuildManifestId()
        {
            var routeKeys = new List<string> { BuildRouteManifest(DefaultRoute) };
            foreach (PlayableHuntRoutePlan route in routesById.Values) routeKeys.Add(BuildRouteManifest(route));
            routeKeys.Sort(StringComparer.Ordinal);
            return $"hunt-v1|{string.Join("|", routeKeys)}";
        }

        private static string BuildRouteManifest(PlayableHuntRoutePlan route)
        {
            var ids = new List<string> { route.StartingTile.ContentId };
            foreach (HexTileData tile in route.TilePool) ids.Add(tile.ContentId);
            foreach (EventData gameEvent in route.HuntEvents) ids.Add(gameEvent.ContentId);
            ids.Sort(StringComparer.Ordinal);
            return $"{route.DestinationId}:{route.MinimumYear}:{route.NoiseProfile.ManifestKey}:{string.Join(",", ids)}";
        }

        private static void DestroyOwnedObject(UnityEngine.Object ownedObject)
        {
            if (ownedObject == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(ownedObject);
            else
                UnityEngine.Object.DestroyImmediate(ownedObject);
        }
    }
}
