using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
        public bool TryResolveItem(string contentId, out ItemData item)
        {
            if (Owner != null) return Owner.TryResolveItem(contentId, out item);
            item = null;
            return false;
        }

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
                if (!candidate.TryBuildCanonicalEventSet(out List<EventData> canonicalEvents, out reason))
                {
                    candidate.Dispose();
                    return false;
                }
                candidate.BundleId = candidate.BuildManifestId(canonicalEvents);
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
        internal bool TryResolveItem(string contentId, out ItemData item)
        {
            if (!retired) return RegistryBundle.TryGetItem(contentId, out item);
            item = null;
            return false;
        }

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

        private bool TryBuildCanonicalEventSet(out List<EventData> events, out string reason)
        {
            var canonicalEvents = new Dictionary<string, EventData>(StringComparer.Ordinal);
            if (!TryAddCanonicalEvents(RegistryBundle.Events, canonicalEvents, out reason) || !TryAddCanonicalEvents(EventGeneration.Events, canonicalEvents, out reason))
            {
                events = null;
                return false;
            }
            var routes = new List<PlayableHuntRoutePlan> { DefaultRoute };
            routes.AddRange(routesById.Values);
            foreach (PlayableHuntRoutePlan route in routes)
                if (!TryAddCanonicalEvents(route.HuntEvents, canonicalEvents, out reason))
                {
                    events = null;
                    return false;
                }
            foreach (EventData gameEvent in canonicalEvents.Values)
                if (!TryValidateEventReferences(gameEvent.chainedEvents, canonicalEvents, out reason))
                {
                    events = null;
                    return false;
                }
                else
                    foreach (EventOption option in gameEvent.options ?? new List<EventOption>())
                        if (!TryValidateEventReferences(option?.successChain, canonicalEvents, out reason) || !TryValidateEventReferences(option?.failChain, canonicalEvents, out reason))
                        {
                            events = null;
                            return false;
                        }
            events = new List<EventData>(canonicalEvents.Values);
            events.Sort((left, right) => string.Compare(left.ContentId, right.ContentId, StringComparison.Ordinal));
            reason = string.Empty;
            return true;
        }

        private static bool TryAddCanonicalEvents(IReadOnlyList<EventData> source, IDictionary<string, EventData> canonicalEvents, out string reason)
        {
            foreach (EventData gameEvent in source ?? Array.Empty<EventData>())
            {
                if (gameEvent == null || !gameEvent.HasExplicitContentId)
                {
                    reason = "Hunt Bundle 事件闭包包含空事件或缺少显式稳定 ID。";
                    return false;
                }
                if (canonicalEvents.TryGetValue(gameEvent.ContentId, out EventData canonicalEvent) && !ReferenceEquals(canonicalEvent, gameEvent))
                {
                    reason = $"Hunt Bundle 事件闭包包含同 ID 的不同内容对象：{gameEvent.ContentId}";
                    return false;
                }
                canonicalEvents[gameEvent.ContentId] = gameEvent;
            }
            reason = string.Empty;
            return true;
        }

        private static bool TryValidateEventReferences(IReadOnlyList<EventData> references, IReadOnlyDictionary<string, EventData> canonicalEvents, out string reason)
        {
            foreach (EventData gameEvent in references ?? Array.Empty<EventData>())
                if (gameEvent == null || !gameEvent.HasExplicitContentId || !canonicalEvents.TryGetValue(gameEvent.ContentId, out EventData canonicalEvent) || !ReferenceEquals(canonicalEvent, gameEvent))
                {
                    reason = $"事件链引用不属于 Hunt Bundle 的 canonical 事件闭包：{gameEvent?.ContentId}";
                    return false;
                }
            reason = string.Empty;
            return true;
        }

        private string BuildManifestId(IReadOnlyList<EventData> canonicalEvents)
        {
            var routes = new List<PlayableHuntRoutePlan> { DefaultRoute };
            routes.AddRange(routesById.Values);
            routes.Sort((left, right) => string.Compare(left.DestinationId, right.DestinationId, StringComparison.Ordinal));
            var manifest = new StringBuilder();
            AppendToken(manifest, "hunt-content-manifest-v2");
            var items = new List<ItemData>(RegistryBundle.Items);
            items.Sort((left, right) => string.Compare(left.ContentId, right.ContentId, StringComparison.Ordinal));
            AppendToken(manifest, "registry-items");
            AppendToken(manifest, items.Count);
            foreach (ItemData item in items) AppendItemManifest(manifest, item);
            var inventions = new List<InventionData>(RegistryBundle.Inventions);
            inventions.Sort((left, right) => string.Compare(left.ContentId, right.ContentId, StringComparison.Ordinal));
            AppendToken(manifest, "registry-inventions");
            AppendToken(manifest, inventions.Count);
            foreach (InventionData invention in inventions) AppendInventionManifest(manifest, invention);
            AppendToken(manifest, "canonical-events");
            AppendToken(manifest, canonicalEvents.Count);
            foreach (EventData gameEvent in canonicalEvents) AppendEventManifest(manifest, gameEvent);
            AppendToken(manifest, "routes");
            AppendToken(manifest, routes.Count);
            foreach (PlayableHuntRoutePlan route in routes) AppendRouteManifest(manifest, route);
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(manifest.ToString()));
            var result = new StringBuilder("hunt-v2:", 72);
            foreach (byte value in hash) result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            return result.ToString();
        }

        private static void AppendRouteManifest(StringBuilder manifest, PlayableHuntRoutePlan route)
        {
            AppendToken(manifest, route.DestinationId);
            AppendToken(manifest, route.MinimumYear);
            AppendTileManifest(manifest, route.StartingTile, "starting");
            AppendToken(manifest, route.TilePool.Count);
            foreach (HexTileData tile in route.TilePool) AppendTileManifest(manifest, tile, "pool");
            AppendToken(manifest, route.HuntEvents.Count);
            foreach (EventData gameEvent in route.HuntEvents) AppendToken(manifest, gameEvent.ContentId);
            AppendNoiseManifest(manifest, route.NoiseProfile);
        }

        private static void AppendTileManifest(StringBuilder manifest, HexTileData tile, string role)
        {
            AppendToken(manifest, role);
            AppendToken(manifest, tile.ContentId);
            AppendToken(manifest, tile.tileName);
            AppendToken(manifest, tile.tileType);
            AppendToken(manifest, tile.description);
            AppendToken(manifest, tile.tileRevealEvent?.ContentId);
            AppendToken(manifest, tile.tileRule);
            AppendToken(manifest, tile.spawnWeight);
            AppendToken(manifest, tile.spawnInGroup);
            AppendToken(manifest, tile.groupSize);
            AppendToken(manifest, tile.mustBeAdjacent);
            AppendToken(manifest, tile.maxResourcePoints);
            AppendToken(manifest, tile.bossEncounterWeight);
            AppendToken(manifest, tile.bossEncounterId);
            AppendToken(manifest, tile.resourcePoints?.Count ?? 0);
            foreach (ResourcePointConfig resourcePoint in tile.resourcePoints ?? new List<ResourcePointConfig>())
            {
                AppendItemManifest(manifest, resourcePoint?.resource);
                AppendToken(manifest, resourcePoint?.spawnWeight ?? 0);
                AppendToken(manifest, resourcePoint?.drawCount ?? 0);
                AppendToken(manifest, resourcePoint?.maxPerTile ?? 0);
            }
        }

        private static void AppendItemManifest(StringBuilder manifest, ItemData item)
        {
            AppendToken(manifest, item?.ContentId);
            if (item == null) return;
            AppendToken(manifest, item.itemName);
            AppendToken(manifest, item.itemType);
            AppendToken(manifest, item.description);
            AppendToken(manifest, item.tags?.Count ?? 0);
            foreach (ItemTag tag in item.tags ?? new List<ItemTag>()) AppendToken(manifest, tag);
            AppendToken(manifest, item.keywords?.Count ?? 0);
            foreach (string keyword in item.keywords ?? new List<string>()) AppendToken(manifest, keyword);
            AppendToken(manifest, item.weaponStats?.speed ?? 0);
            AppendToken(manifest, item.weaponStats?.power ?? 0);
            AppendToken(manifest, item.weaponStats?.accuracy ?? 0);
            AppendToken(manifest, item.weaponStats?.range ?? 0);
            AppendToken(manifest, item.weaponStats?.specialRule);
            AppendToken(manifest, item.armorStats?.armorHead ?? 0);
            AppendToken(manifest, item.armorStats?.armorBody ?? 0);
            AppendToken(manifest, item.armorStats?.armorArms ?? 0);
            AppendToken(manifest, item.armorStats?.armorLegs ?? 0);
            AppendToken(manifest, item.stackLimit);
            AppendToken(manifest, item.HuntNoise);
        }

        private static void AppendInventionManifest(StringBuilder manifest, InventionData invention)
        {
            AppendToken(manifest, invention.ContentId);
            AppendToken(manifest, invention.inventionName);
            AppendToken(manifest, invention.description);
            AppendToken(manifest, invention.category);
            AppendToken(manifest, invention.prerequisites?.Count ?? 0);
            foreach (InventionData prerequisite in invention.prerequisites ?? new List<InventionData>()) AppendToken(manifest, prerequisite?.ContentId);
            AppendToken(manifest, invention.costs?.Count ?? 0);
            foreach (InventionCost cost in invention.costs ?? new List<InventionCost>())
            {
                AppendToken(manifest, cost?.resource?.ContentId);
                AppendToken(manifest, cost?.count ?? 0);
            }
            AppendToken(manifest, invention.exclusiveWith?.Count ?? 0);
            foreach (InventionData exclusive in invention.exclusiveWith ?? new List<InventionData>()) AppendToken(manifest, exclusive?.ContentId);
            AppendToken(manifest, invention.effectDescription);
            AppendToken(manifest, invention.unlockEffects?.Count ?? 0);
            foreach (InventionPassiveEffect effect in invention.unlockEffects ?? new List<InventionPassiveEffect>())
            {
                AppendToken(manifest, effect?.lifetime ?? default);
                AppendToken(manifest, effect?.modifierId);
                AppendToken(manifest, effect?.kind ?? default);
                AppendToken(manifest, effect?.target ?? default);
                AppendToken(manifest, effect?.value ?? 0);
            }
            AppendToken(manifest, invention.actionEffects?.Count ?? 0);
            foreach (InventionActionEffect effect in invention.actionEffects ?? new List<InventionActionEffect>())
            {
                AppendToken(manifest, effect?.effectId);
                AppendToken(manifest, effect?.kind ?? default);
                AppendToken(manifest, effect?.targetKeyword);
                AppendToken(manifest, effect?.value ?? 0f);
            }
            AppendToken(manifest, invention.activeEffects?.Count ?? 0);
            foreach (InventionActiveEffect effect in invention.activeEffects ?? new List<InventionActiveEffect>())
            {
                AppendToken(manifest, effect?.effectId);
                AppendToken(manifest, effect?.effectName);
                AppendToken(manifest, effect?.description);
                AppendToken(manifest, effect?.eventId);
                AppendToken(manifest, effect?.maxUsesPerYear ?? 0);
            }
        }

        private static void AppendEventManifest(StringBuilder manifest, EventData gameEvent)
        {
            AppendToken(manifest, gameEvent.ContentId);
            AppendToken(manifest, gameEvent.eventName);
            AppendToken(manifest, gameEvent.eventType);
            AppendToken(manifest, gameEvent.displayText);
            AppendToken(manifest, gameEvent.hiddenText);
            AppendToken(manifest, gameEvent.combatEncounterId);
            AppendToken(manifest, gameEvent.minYear);
            AppendToken(manifest, gameEvent.maxYear);
            AppendToken(manifest, gameEvent.drawWeight);
            AppendToken(manifest, gameEvent.category);
            AppendEffectList(manifest, gameEvent.immediateEffects);
            AppendEventReferences(manifest, gameEvent.chainedEvents);
            AppendToken(manifest, gameEvent.options?.Count ?? 0);
            foreach (EventOption option in gameEvent.options ?? new List<EventOption>())
            {
                AppendToken(manifest, option?.optionText);
                AppendToken(manifest, option?.checkType ?? CheckType.None);
                AppendToken(manifest, option?.checkTarget ?? 0);
                AppendToken(manifest, option?.checkPresentation ?? EventCheckPresentationKind.PhysicalDice);
                AppendToken(manifest, option?.checkCount ?? 0);
                AppendToken(manifest, option?.checkSides ?? 0);
                AppendToken(manifest, option?.checkDeckId);
                AppendToken(manifest, option?.checkInstruction);
                AppendToken(manifest, option?.successText);
                AppendEffectList(manifest, option?.successEffects);
                AppendEventReferences(manifest, option?.successChain);
                AppendToken(manifest, option?.failText);
                AppendEffectList(manifest, option?.failEffects);
                AppendEventReferences(manifest, option?.failChain);
                AppendToken(manifest, option?.alwaysAvailable ?? false);
                AppendToken(manifest, option?.conditions?.Count ?? 0);
                foreach (EventOptionCondition condition in option?.conditions ?? new List<EventOptionCondition>())
                {
                    AppendToken(manifest, condition?.conditionKind ?? default);
                    AppendToken(manifest, condition?.key);
                    AppendToken(manifest, condition?.displayName);
                    AppendToken(manifest, condition?.value ?? 0);
                    AppendToken(manifest, condition?.inverted ?? false);
                }
            }
        }

        private static void AppendEffectList(StringBuilder manifest, IReadOnlyList<EventEffect> effects)
        {
            AppendToken(manifest, effects?.Count ?? 0);
            if (effects == null) return;
            foreach (EventEffect effect in effects)
            {
                AppendToken(manifest, effect?.effectType ?? default);
                AppendToken(manifest, effect?.targetName);
                AppendToken(manifest, effect?.value ?? 0);
                AppendToken(manifest, effect?.description);
            }
        }

        private static void AppendEventReferences(StringBuilder manifest, IReadOnlyList<EventData> events)
        {
            AppendToken(manifest, events?.Count ?? 0);
            if (events == null) return;
            foreach (EventData gameEvent in events) AppendToken(manifest, gameEvent?.ContentId);
        }

        private static void AppendNoiseManifest(StringBuilder manifest, PlayableHuntNoiseProfile profile)
        {
            AppendToken(manifest, profile?.ProfileId);
            AppendToken(manifest, profile?.DeckSize ?? 0);
            AppendToken(manifest, profile?.BaseNoisePerHunter ?? 0);
            AppendToken(manifest, profile?.MaxDangerCards ?? 0);
            IReadOnlyList<EventData> dangerEvents = profile?.GetDangerEvents();
            AppendToken(manifest, dangerEvents?.Count ?? 0);
            if (dangerEvents == null) return;
            foreach (EventData gameEvent in dangerEvents) AppendToken(manifest, gameEvent?.ContentId);
        }

        private static void AppendToken<T>(StringBuilder manifest, T value)
        {
            string text = value switch
            {
                null => string.Empty,
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
            manifest.Append(text.Length).Append(':').Append(text).Append(';');
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
