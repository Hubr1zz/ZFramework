using System;
using System.Collections.Generic;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Board;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunt;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace Core
{
    [Serializable]
    public sealed class CampaignSnapshot
    {
        public const int CurrentSchemaVersion = 2;
        public int CampaignSchemaVersion = CurrentSchemaVersion;
        public SettlementInstance Settlement;
        public bool HasActiveHuntState;
        public ActiveHuntSnapshot ActiveHunt;

        public bool HasActiveHunt => HasActiveHuntState && ActiveHunt != null;
    }

    [Serializable]
    public sealed class ActiveHuntSnapshot
    {
        public const int LegacySchemaVersion = 2;
        public const int CurrentSchemaVersion = 4;
        public const int CurrentPopulationSchemaVersion = 3;
        public const int CurrentEventMemorySchemaVersion = 4;
        public const string RandomAlgorithm = "xorshift32-v1";
        public int SchemaVersion = CurrentSchemaVersion;
        public string ExpeditionId;
        public string DestinationId;
        public string ContentBundleId;
        public bool EncounterHandoffPending;
        public string EncounterId;
        public int Year;
        public List<int> HunterIds = new();
        public int SelectedHunterId;
        public int SquadX;
        public int SquadY;
        public string RandomAlgorithmId = RandomAlgorithm;
        public uint RandomState;
        public List<ActiveHuntTileSnapshot> Tiles = new();
        public List<ActiveHuntCollectibleSnapshot> Collectibles = new();
        public ActiveHuntEventStoreSnapshot EventStore = new();
        public int RescuedPopulation;
        public int PopulationSchemaVersion = ActiveHuntSnapshot.CurrentPopulationSchemaVersion;
        public int EventMemorySchemaVersion = ActiveHuntSnapshot.CurrentEventMemorySchemaVersion;
    }

    [Serializable]
    public sealed class ActiveHuntTileSnapshot
    {
        public int X;
        public int Y;
        public string TileId;
        public TileState State;
        public bool HasBossEncounter;
        public List<ActiveHuntResourcePointSnapshot> ResourcePoints = new();
    }

    [Serializable]
    public sealed class ActiveHuntResourcePointSnapshot
    {
        public string ResourcePointId;
        public string DisplayName;
        public string ItemId;
        public List<string> MaterialItemIds = new();
        public int DrawCount;
        public bool IsExhausted;
    }

    [Serializable]
    public sealed class ActiveHuntCollectibleSnapshot
    {
        public int HunterId;
        public List<ActiveHuntItemStackSnapshot> Items = new();
    }

    [Serializable]
    public sealed class ActiveHuntItemStackSnapshot
    {
        public string ItemId;
        public int Count;
    }

    [Serializable]
    public sealed class ActiveHuntEventStoreSnapshot
    {
        public int NextSequence = 1;
        public int NextRootSequence = -1;
        public List<int> CommittedSequences = new();
        public List<ActiveHuntEventOccurrenceSnapshot> PendingOccurrences = new();
        public List<EventResolutionMemory> Memories = new();
        public int EventMemorySchemaVersion = ActiveHuntSnapshot.CurrentEventMemorySchemaVersion;
        public string Diagnostic;
    }

    [Serializable]
    public sealed class ActiveHuntEventOccurrenceSnapshot
    {
        public int Sequence;
        public string EventId;
        public string EventName;
        public int Year;
        public int ActorId;
        public int X;
        public int Y;
        public List<string> AncestorEventIds = new();
        public PlayableEventRerollCheckpoint RerollCheckpoint;
    }

    public static class ActiveHuntSnapshotAdapter
    {
        private sealed class ResourcePointPersistenceDefinition
        {
            public string Id;
            public ResourcePointConfig Config;
            public List<string> MaterialIds = new();
        }

        public static bool TryCapture(SettlementInstance settlement, HuntManager manager, PlayableHuntActionSession session, string expeditionId, out CampaignSnapshot campaign, out string reason, bool allowRunningSession = false)
        {
            campaign = null;
            if (settlement == null || manager == null || session?.IsActive != true || session.IsRunning && !allowRunningSession)
            {
                reason = "活动狩猎尚未到达可保存的权威检查点。";
                return false;
            }
            if (settlement.PendingHuntReturn != null)
            {
                reason = "活动狩猎与待结算回营记录不能同时存在。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(expeditionId))
            {
                reason = "活动狩猎缺少稳定远征 ID。";
                return false;
            }
            PlayableHuntRoutePlan boundRoute = manager.BoundRoute;
            if (boundRoute?.IsUsable != true || !ReferenceEquals(boundRoute.Owner, PlayableHuntContentRuntime.CurrentBundle))
            {
                reason = "活动狩猎未绑定当前可用的 Hunt 内容 Bundle。";
                return false;
            }

            var active = new ActiveHuntSnapshot
            {
                ExpeditionId = expeditionId.Trim(),
                DestinationId = boundRoute.DestinationId,
                ContentBundleId = boundRoute.ContentBundleId,
                Year = manager.CurrentYear,
                SelectedHunterId = manager.SelectedHunter?.InstanceId ?? 0,
                SquadX = manager.SquadPosition.x,
                SquadY = manager.SquadPosition.y,
                RandomState = manager.CaptureRandomState().Value,
                RescuedPopulation = manager.RescuedPopulation
            };
            var hunterIds = new HashSet<int>();
            foreach (HunterInstance hunter in manager.ActiveHunters)
            {
                if (hunter == null || hunter.InstanceId <= 0 || !hunterIds.Add(hunter.InstanceId) || !ReferenceEquals(settlement.GetHunter(hunter.InstanceId), hunter))
                {
                    reason = "活动狩猎包含空、重复、无效或非当前营地世代的猎人。";
                    return false;
                }
                active.HunterIds.Add(hunter.InstanceId);
                var collectible = new ActiveHuntCollectibleSnapshot { HunterId = hunter.InstanceId };
                foreach (ItemInstance item in hunter.Collectibles)
                {
                    if (item?.Data == null || string.IsNullOrWhiteSpace(item.Data.ContentId) || item.Count <= 0 || !boundRoute.TryResolveItem(item.Data.ContentId, out ItemData canonicalItem) || !ReferenceEquals(canonicalItem, item.Data))
                    {
                        reason = $"猎人 {hunter.InstanceId} 的采集物缺少同代稳定内容或数量无效。";
                        return false;
                    }
                    collectible.Items.Add(new ActiveHuntItemStackSnapshot { ItemId = item.Data.ContentId, Count = item.Count });
                }
                active.Collectibles.Add(collectible);
            }
            if (active.HunterIds.Count == 0 || active.HunterIds.Count > 4)
            {
                reason = "活动狩猎编队数量无效。";
                return false;
            }
            if (manager.Map == null || manager.Map.Count == 0 || !manager.Map.TryGetValue(manager.SquadPosition, out HexTileInstance squadTile) || squadTile?.State != TileState.Revealed)
            {
                reason = "活动狩猎地图为空，或小队不在已揭示地块。";
                return false;
            }

            var coordinates = new List<Vector2Int>(manager.Map.Keys);
            coordinates.Sort((left, right) => left.x != right.x ? left.x.CompareTo(right.x) : left.y.CompareTo(right.y));
            var canonicalTiles = new Dictionary<string, HexTileData>(StringComparer.Ordinal) { [boundRoute.StartingTile.ContentId] = boundRoute.StartingTile };
            foreach (HexTileData routeTile in boundRoute.TilePool) canonicalTiles.Add(routeTile.ContentId, routeTile);
            foreach (Vector2Int coordinate in coordinates)
            {
                HexTileInstance tile = manager.Map[coordinate];
                if (tile?.Config == null || !tile.Config.HasExplicitContentId || !canonicalTiles.TryGetValue(tile.Config.ContentId, out HexTileData canonicalTile) || !ReferenceEquals(canonicalTile, tile.Config))
                {
                    reason = $"地块 {coordinate} 缺少当前路线世代的稳定内容。";
                    return false;
                }
                var tileSnapshot = new ActiveHuntTileSnapshot
                {
                    X = coordinate.x,
                    Y = coordinate.y,
                    TileId = tile.Config.ContentId,
                    State = tile.State,
                    HasBossEncounter = tile.HasBossEncounter
                };
                foreach (ResourcePointInstance point in tile.ResourcePoints)
                {
                    var materialIds = new List<string>();
                    IReadOnlyList<ItemData> materials = point?.HasMaterialPool == true ? point.MaterialPool : point?.Resource != null ? new[] { point.Resource } : Array.Empty<ItemData>();
                    foreach (ItemData material in materials)
                    {
                        if (material == null || string.IsNullOrWhiteSpace(material.ContentId) || !boundRoute.TryResolveItem(material.ContentId, out ItemData canonicalItem) || !ReferenceEquals(canonicalItem, material))
                        {
                            reason = $"地块 {coordinate} 的资源点缺少同代稳定素材 ID。";
                            return false;
                        }
                        materialIds.Add(material.ContentId);
                    }
                    if (point == null || string.IsNullOrWhiteSpace(point.StableId) || materialIds.Count == 0 || point.DrawCount < 0 || point.DrawCount > materialIds.Count)
                    {
                        reason = $"地块 {coordinate} 的资源点身份、牌池或允许翻牌数无效。";
                        return false;
                    }
                    tileSnapshot.ResourcePoints.Add(new ActiveHuntResourcePointSnapshot { ResourcePointId = point.StableId, DisplayName = point.ResourceName, ItemId = materialIds[0], MaterialItemIds = materialIds, DrawCount = point.DrawCount, IsExhausted = point.IsExhausted });
                }
                active.Tiles.Add(tileSnapshot);
            }
            if (!CaptureEventStore(session.CaptureOccurrenceState(), boundRoute, active.EventStore, out reason)) return false;
            campaign = new CampaignSnapshot { Settlement = settlement, HasActiveHuntState = true, ActiveHunt = active };
            reason = string.Empty;
            return true;
        }

        public static CampaignSnapshot CaptureSettlement(SettlementInstance settlement)
        {
            return settlement == null ? null : new CampaignSnapshot { Settlement = settlement, HasActiveHuntState = false, ActiveHunt = null };
        }

        public static bool TryRestore(CampaignSnapshot campaign, HuntManager manager, out PlayableHuntRuntimeState runtimeState, out PlayableHuntEventOccurrenceStore occurrenceStore, out string reason)
        {
            runtimeState = null;
            occurrenceStore = null;
            ActiveHuntSnapshot active = campaign?.HasActiveHunt == true ? campaign.ActiveHunt : null;
            SettlementInstance settlement = campaign?.Settlement;
            if (campaign == null || campaign.CampaignSchemaVersion <= 0 || campaign.CampaignSchemaVersion > CampaignSnapshot.CurrentSchemaVersion || settlement == null || active == null)
            {
                reason = "战役或活动狩猎快照版本无效。";
                return false;
            }
            if (active.SchemaVersion < ActiveHuntSnapshot.LegacySchemaVersion || active.SchemaVersion > ActiveHuntSnapshot.CurrentSchemaVersion || active.Year != settlement.CurrentYear || string.IsNullOrWhiteSpace(active.ExpeditionId) || string.IsNullOrWhiteSpace(active.ContentBundleId) || active.RandomState == 0 || !string.Equals(active.RandomAlgorithmId, ActiveHuntSnapshot.RandomAlgorithm, StringComparison.Ordinal) || active.RescuedPopulation < 0 || active.SchemaVersion == ActiveHuntSnapshot.LegacySchemaVersion && active.RescuedPopulation != 0)
            {
                reason = "活动狩猎快照的年份、身份或随机算法无效。";
                return false;
            }
            if (active.PopulationSchemaVersion > ActiveHuntSnapshot.CurrentPopulationSchemaVersion || active.EventMemorySchemaVersion > ActiveHuntSnapshot.CurrentEventMemorySchemaVersion)
            {
                reason = "活动狩猎快照包含未来 schema。";
                return false;
            }
            if (active.SchemaVersion >= ActiveHuntSnapshot.CurrentSchemaVersion && (active.PopulationSchemaVersion != ActiveHuntSnapshot.CurrentPopulationSchemaVersion || active.EventMemorySchemaVersion != ActiveHuntSnapshot.CurrentEventMemorySchemaVersion))
            {
                reason = "活动狩猎快照的记忆或人口 schema 无效。";
                return false;
            }
            if (active.SchemaVersion < ActiveHuntSnapshot.CurrentSchemaVersion && (active.EventStore?.Memories?.Count ?? 0) > 0)
            {
                reason = "v2/v3 活动狩猎快照不得包含事件结果记忆。";
                return false;
            }
            if (active.SchemaVersion >= ActiveHuntSnapshot.CurrentSchemaVersion && (active.EventStore == null || active.EventStore.EventMemorySchemaVersion != ActiveHuntSnapshot.CurrentEventMemorySchemaVersion))
            {
                reason = "活动狩猎事件检查点 schema 无效。";
                return false;
            }
            PlayableHuntRoutePlan boundRoute = manager?.BoundRoute;
            if (boundRoute?.IsUsable != true || !ReferenceEquals(boundRoute.Owner, PlayableHuntContentRuntime.CurrentBundle) || !string.Equals(active.DestinationId, boundRoute.DestinationId, StringComparison.Ordinal) || !string.Equals(active.ContentBundleId, boundRoute.ContentBundleId, StringComparison.Ordinal))
            {
                reason = "活动狩猎快照的目的地或内容 Bundle 与当前运行态不一致。";
                return false;
            }
            if (settlement.PendingHuntReturn != null)
            {
                reason = "存档同时包含活动狩猎与待结算回营记录。";
                return false;
            }
            if (active.EncounterHandoffPending)
            {
                reason = $"活动狩猎停留在不可恢复的遭遇交接：{active.EncounterId}";
                return false;
            }

            var hunters = new List<HunterInstance>();
            var hunterIds = new HashSet<int>();
            if (active.HunterIds == null || active.HunterIds.Count == 0 || active.HunterIds.Count > 4)
            {
                reason = "活动狩猎编队数量无效。";
                return false;
            }
            foreach (int hunterId in active.HunterIds)
            {
                HunterInstance hunter = settlement.Hunters.Find(candidate => candidate != null && candidate.InstanceId == hunterId);
                if (hunter == null || !hunterIds.Add(hunterId))
                {
                    reason = $"无法恢复活动狩猎猎人：{hunterId}";
                    return false;
                }
                hunters.Add(hunter);
            }
            if (hunters.Count == 0 || hunters.Count > 4)
            {
                reason = "活动狩猎编队数量无效。";
                return false;
            }

            if (!TryBuildTileCatalog(manager, out Dictionary<string, HexTileData> tilesById, out reason)) return false;
            var map = new Dictionary<Vector2Int, HexTileInstance>();
            if (active.Tiles == null || active.Tiles.Count == 0)
            {
                reason = "活动狩猎地图快照为空。";
                return false;
            }
            foreach (ActiveHuntTileSnapshot savedTile in active.Tiles)
            {
                if (savedTile == null)
                {
                    reason = "活动狩猎包含空地块记录。";
                    return false;
                }
                var coordinate = new Vector2Int(savedTile.X, savedTile.Y);
                if (!tilesById.TryGetValue(savedTile.TileId ?? string.Empty, out HexTileData config) || map.ContainsKey(coordinate))
                {
                    reason = $"无法恢复活动狩猎地块：{savedTile.TileId}";
                    return false;
                }
                HuntTileDefinition tileDefinition = CreateDefinition(config);
                var domainState = new HuntTileState(new GridPosition(coordinate.x, coordinate.y), tileDefinition) { Visibility = (HuntTileVisibility)savedTile.State, HasBossEncounter = savedTile.HasBossEncounter };
                var tile = new HexTileInstance { AxialCoord = coordinate, Config = config, ConfigId = config.ContentId, ConfigName = config.name };
                tile.AttachDomainState(domainState);
                if (savedTile.ResourcePoints == null)
                {
                    reason = $"地块 {savedTile.TileId} 的资源点列表无效。";
                    return false;
                }
                if (savedTile.ResourcePoints.Count > config.maxResourcePoints)
                {
                    reason = $"地块 {coordinate} 的资源点总数超过当前配置上限。";
                    return false;
                }
                if (!TryBuildResourcePointDefinitions(config, boundRoute, out List<ResourcePointPersistenceDefinition> definitions, out Dictionary<string, ResourcePointPersistenceDefinition> definitionsById, out reason))
                    return false;
                var restoredCounts = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (ActiveHuntResourcePointSnapshot savedPoint in savedTile.ResourcePoints)
                {
                    if (savedPoint == null || savedPoint.DrawCount < 0)
                    {
                        reason = $"无法恢复地块资源：{savedPoint?.ItemId}";
                        return false;
                    }
                    bool allowLegacySingleMaterial = active.SchemaVersion >= ActiveHuntSnapshot.LegacySchemaVersion && active.SchemaVersion < ActiveHuntSnapshot.CurrentSchemaVersion;
                    if (!TryResolveResourcePointDefinition(savedPoint, definitions, definitionsById, boundRoute, allowLegacySingleMaterial, out ResourcePointPersistenceDefinition definition, out List<string> restoredMaterialIds, out reason))
                        return false;
                    restoredCounts.TryGetValue(definition.Id, out int restoredCount);
                    if (definition.Config.maxPerTile > 0 && restoredCount >= definition.Config.maxPerTile)
                    {
                        reason = $"地块 {coordinate} 的资源点超过 {definition.Id} 的同类上限。";
                        return false;
                    }
                    restoredCounts[definition.Id] = restoredCount + 1;
                    if (savedPoint.DrawCount != definition.Config.drawCount)
                    {
                        reason = $"地块 {coordinate} 的资源点 {definition.Id} 允许翻牌数与当前配置不一致。";
                        return false;
                    }
                    var materials = new List<ItemData>(restoredMaterialIds.Count);
                    foreach (string materialId in restoredMaterialIds)
                    {
                        if (!boundRoute.TryResolveItem(materialId, out ItemData material) || material == null || material.itemType != ItemType.Resource)
                        {
                            reason = $"无法恢复地块资源素材：{materialId}";
                            return false;
                        }
                        materials.Add(material);
                    }
                    ItemData primary = materials[0];
                    string displayName = string.IsNullOrWhiteSpace(definition.Config.displayName) ? primary.itemName : definition.Config.displayName.Trim();
                    tile.ResourcePoints.Add(new ResourcePointInstance { ResourcePointId = definition.Id, ResourceName = displayName, Resource = primary, MaterialPool = materials, DrawCount = definition.Config.drawCount, IsExhausted = savedPoint.IsExhausted });
                }
                map.Add(coordinate, tile);
            }

            var squadPosition = new Vector2Int(active.SquadX, active.SquadY);
            if (!map.TryGetValue(squadPosition, out HexTileInstance squadTile) || squadTile.State != TileState.Revealed)
            {
                reason = "活动狩猎小队位置不是已揭示地块。";
                return false;
            }
            if (!TryRestoreEventStore(active.EventStore, boundRoute, active.ExpeditionId, out occurrenceStore, out reason)) return false;
            if (!TryRestoreCollectibles(active, hunters, boundRoute, out reason)) return false;

            runtimeState = new PlayableHuntRuntimeState
            {
                Year = active.Year,
                Hunters = hunters,
                SelectedHunterId = active.SelectedHunterId,
                SquadPosition = squadPosition,
                Map = map,
                RandomState = new StatefulRandomState(active.RandomState),
                RescuedPopulation = active.SchemaVersion >= ActiveHuntSnapshot.CurrentPopulationSchemaVersion ? active.RescuedPopulation : 0
            };
            reason = string.Empty;
            return true;
        }

        private static bool TryBuildResourcePointDefinitions(HexTileData tile, PlayableHuntRoutePlan route, out List<ResourcePointPersistenceDefinition> definitions, out Dictionary<string, ResourcePointPersistenceDefinition> definitionsById, out string reason)
        {
            definitions = new List<ResourcePointPersistenceDefinition>();
            definitionsById = new Dictionary<string, ResourcePointPersistenceDefinition>(StringComparer.Ordinal);
            foreach (ResourcePointConfig config in tile?.resourcePoints ?? new List<ResourcePointConfig>())
            {
                if (config == null || config.drawCount <= 0 || config.drawCount > HarvestDrawPlan.MaximumCardCount || config.maxPerTile < 0 || !TryResolveResourcePointId(config, out string pointId))
                {
                    reason = $"地块 {tile?.ContentId} 的资源点配置无效。";
                    return false;
                }
                if (definitionsById.ContainsKey(pointId))
                {
                    reason = $"地块 {tile.ContentId} 的资源点稳定 ID 重复：{pointId}";
                    return false;
                }
                if (!TryBuildExpectedMaterialIds(config, route, out List<string> materialIds, out reason))
                    return false;
                var definition = new ResourcePointPersistenceDefinition { Id = pointId, Config = config, MaterialIds = materialIds };
                definitions.Add(definition);
                definitionsById.Add(pointId, definition);
            }
            reason = string.Empty;
            return true;
        }

        private static bool TryResolveResourcePointDefinition(ActiveHuntResourcePointSnapshot savedPoint, IReadOnlyList<ResourcePointPersistenceDefinition> definitions, IReadOnlyDictionary<string, ResourcePointPersistenceDefinition> definitionsById, PlayableHuntRoutePlan route, bool allowLegacySingleMaterial, out ResourcePointPersistenceDefinition definition, out List<string> restoredMaterialIds, out string reason)
        {
            definition = null;
            restoredMaterialIds = null;
            reason = string.Empty;
            string savedId = savedPoint?.ResourcePointId?.Trim() ?? string.Empty;
            if (savedId.Length > 0)
            {
                if (!definitionsById.TryGetValue(savedId, out definition))
                {
                    reason = $"存档资源点不属于当前地块：{savedId}";
                    return false;
                }
                if (allowLegacySingleMaterial && (savedPoint?.MaterialItemIds == null || savedPoint.MaterialItemIds.Count == 0))
                    return TryBuildLegacyMaterialIds(savedPoint, definition, route, out restoredMaterialIds, out reason);
            }
            else
            {
                if (savedPoint?.MaterialItemIds == null || savedPoint.MaterialItemIds.Count == 0)
                {
                    if (!allowLegacySingleMaterial || !TryResolveLegacyResourcePointByItem(savedPoint, definitions, route, out definition, out restoredMaterialIds, out reason))
                    {
                        reason = string.IsNullOrEmpty(reason) ? "历史资源点缺少稳定 ID，且没有完整素材池用于唯一迁移。" : reason;
                        return false;
                    }
                    return true;
                }
                int matchCount = 0;
                foreach (ResourcePointPersistenceDefinition candidate in definitions)
                    if (AreMaterialMultisetsEqual(savedPoint.MaterialItemIds, candidate.MaterialIds))
                    {
                        definition = candidate;
                        matchCount++;
                    }
                if (matchCount != 1)
                {
                    reason = "历史资源点缺少稳定 ID，素材池无法唯一匹配当前配置。";
                    definition = null;
                    return false;
                }
            }
            if (!AreMaterialMultisetsEqual(savedPoint.MaterialItemIds, definition.MaterialIds))
            {
                reason = $"存档资源点 {definition.Id} 的素材池与当前配置不一致。";
                definition = null;
                return false;
            }
            restoredMaterialIds = new List<string>(definition.MaterialIds);
            reason = string.Empty;
            return true;
        }

        private static bool TryResolveLegacyResourcePointByItem(ActiveHuntResourcePointSnapshot savedPoint, IReadOnlyList<ResourcePointPersistenceDefinition> definitions, PlayableHuntRoutePlan route, out ResourcePointPersistenceDefinition definition, out List<string> restoredMaterialIds, out string reason)
        {
            definition = null;
            restoredMaterialIds = null;
            if (savedPoint == null || string.IsNullOrWhiteSpace(savedPoint.ItemId) || !route.TryResolveItem(savedPoint.ItemId.Trim(), out ItemData item) || item == null || item.itemType != ItemType.Resource)
            {
                reason = "历史资源点缺少可解析的单素材 ID。";
                return false;
            }
            int matchCount = 0;
            foreach (ResourcePointPersistenceDefinition candidate in definitions)
                if (ContainsMaterialId(candidate.MaterialIds, item.ContentId))
                {
                    definition = candidate;
                    matchCount++;
                }
            if (matchCount != 1)
            {
                definition = null;
                reason = "历史资源点的单素材 ID 无法唯一匹配当前配置。";
                return false;
            }
            return TryBuildLegacyMaterialIds(savedPoint, definition, route, out restoredMaterialIds, out reason);
        }

        private static bool TryBuildLegacyMaterialIds(ActiveHuntResourcePointSnapshot savedPoint, ResourcePointPersistenceDefinition definition, PlayableHuntRoutePlan route, out List<string> restoredMaterialIds, out string reason)
        {
            restoredMaterialIds = null;
            if (savedPoint == null || definition == null || string.IsNullOrWhiteSpace(savedPoint.ItemId) || !route.TryResolveItem(savedPoint.ItemId.Trim(), out ItemData item) || item == null || item.itemType != ItemType.Resource || !ContainsMaterialId(definition.MaterialIds, item.ContentId))
            {
                reason = "历史资源点的单素材 ID 不属于当前资源点配置。";
                return false;
            }
            restoredMaterialIds = new List<string>();
            for (int index = 0; index < savedPoint.DrawCount; index++)
                restoredMaterialIds.Add(item.ContentId);
            reason = string.Empty;
            return true;
        }

        private static bool ContainsMaterialId(IReadOnlyList<string> materialIds, string itemId)
        {
            if (materialIds == null || string.IsNullOrWhiteSpace(itemId)) return false;
            foreach (string materialId in materialIds)
                if (string.Equals(materialId, itemId, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool TryResolveResourcePointId(ResourcePointConfig config, out string pointId)
        {
            pointId = config?.resourcePointId?.Trim() ?? string.Empty;
            if (pointId.Length > 0) return true;
            pointId = config?.resource?.ContentId?.Trim() ?? string.Empty;
            if (pointId.Length > 0) return true;
            foreach (ResourceMaterialConfig material in config?.materialPool ?? new List<ResourceMaterialConfig>())
            {
                pointId = material?.materialId?.Trim() ?? material?.material?.ContentId?.Trim() ?? string.Empty;
                if (pointId.Length > 0) return true;
            }
            return false;
        }

        private static bool TryBuildExpectedMaterialIds(ResourcePointConfig config, PlayableHuntRoutePlan route, out List<string> materialIds, out string reason)
        {
            materialIds = new List<string>();
            if (config.materialPool != null && config.materialPool.Count > 0)
            {
                foreach (ResourceMaterialConfig material in config.materialPool)
                {
                    string materialId = material?.materialId?.Trim() ?? material?.material?.ContentId?.Trim() ?? string.Empty;
                    if (material == null || material.copies <= 0 || material.copies > HarvestDrawPlan.MaximumCardCount - materialIds.Count || materialId.Length == 0 || !route.TryResolveItem(materialId, out ItemData resolved) || resolved == null || resolved.itemType != ItemType.Resource)
                    {
                        reason = "当前资源点配置包含无效素材。";
                        return false;
                    }
                    for (int index = 0; index < material.copies; index++)
                        materialIds.Add(resolved.ContentId);
                }
            }
            else if (config.resource != null && route.TryResolveItem(config.resource.ContentId, out ItemData resolved) && resolved != null && resolved.itemType == ItemType.Resource)
            {
                for (int index = 0; index < config.drawCount; index++)
                    materialIds.Add(resolved.ContentId);
            }
            if (materialIds.Count == 0 || config.drawCount > materialIds.Count)
            {
                reason = "当前资源点素材池小于允许翻牌数。";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private static bool AreMaterialMultisetsEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string value in left)
            {
                string id = value?.Trim() ?? string.Empty;
                if (id.Length == 0) return false;
                counts.TryGetValue(id, out int count);
                counts[id] = count + 1;
            }
            foreach (string value in right)
            {
                string id = value?.Trim() ?? string.Empty;
                if (id.Length == 0 || !counts.TryGetValue(id, out int count)) return false;
                if (count == 1) counts.Remove(id);
                else counts[id] = count - 1;
            }
            return counts.Count == 0;
        }

        private static bool CaptureEventStore(PlayableHuntEventOccurrenceStoreState state, PlayableHuntRoutePlan boundRoute, ActiveHuntEventStoreSnapshot target, out string reason)
        {
            if (state == null || boundRoute?.IsUsable != true || target == null)
            {
                reason = "活动狩猎事件检查点或绑定路线无效。";
                return false;
            }
            target.NextSequence = state.NextSequence;
            target.NextRootSequence = state.NextRootSequence;
            target.Diagnostic = state.Diagnostic;
            target.CommittedSequences.AddRange(state.CommittedSequences);
            foreach (EventResolutionMemory memory in state.Memories ?? Array.Empty<EventResolutionMemory>())
                if (memory != null) target.Memories.Add(EventResolutionMemoryRules.Clone(memory));
            target.EventMemorySchemaVersion = ActiveHuntSnapshot.CurrentEventMemorySchemaVersion;
            foreach (PlayableHuntEventOccurrenceRecord record in state.PendingOccurrences ?? Array.Empty<PlayableHuntEventOccurrenceRecord>())
            {
                PlayableEventChainOccurrence occurrence = record.Occurrence;
                if (occurrence.Sequence == 0 || !boundRoute.TryResolveEvent(occurrence.EventId, out _))
                {
                    reason = $"活动狩猎事件 occurrence 不属于当前路线：{occurrence.EventId}";
                    return false;
                }
                IReadOnlyList<string> ancestorIds = record.AncestorContentIds ?? Array.Empty<string>();
                foreach (string ancestorId in ancestorIds)
                    if (!boundRoute.TryResolveEvent(ancestorId, out _))
                    {
                        reason = $"活动狩猎事件 ancestor 不属于当前路线：{ancestorId}";
                        return false;
                    }
                target.PendingOccurrences.Add(new ActiveHuntEventOccurrenceSnapshot
                {
                    Sequence = occurrence.Sequence,
                    EventId = occurrence.EventId,
                    EventName = occurrence.EventName,
                    Year = occurrence.Year,
                    ActorId = occurrence.ActorId,
                    X = record.Coordinate.x,
                    Y = record.Coordinate.y,
                    AncestorEventIds = new List<string>(ancestorIds),
                    RerollCheckpoint = occurrence.RerollCheckpoint?.HasValue == true ? occurrence.RerollCheckpoint : null
                });
            }
            reason = string.Empty;
            return true;
        }

        private static bool TryBuildTileCatalog(HuntManager manager, out Dictionary<string, HexTileData> result, out string reason)
        {
            result = new Dictionary<string, HexTileData>(StringComparer.Ordinal);
            var tiles = new List<HexTileData>(manager.TilePool ?? new List<HexTileData>());
            if (manager.StartingTileConfig != null) tiles.Add(manager.StartingTileConfig);
            foreach (HexTileData tile in tiles)
            {
                if (tile == null || !tile.HasExplicitContentId || result.ContainsKey(tile.ContentId))
                {
                    reason = $"狩猎地块目录存在空、兼容名称或重复 ID：{tile?.ContentId}";
                    result.Clear();
                    return false;
                }
                result.Add(tile.ContentId, tile);
            }
            reason = string.Empty;
            if (result.Count > 0) return true;
            reason = "狩猎地块目录为空。";
            return false;
        }

        private static bool TryRestoreCollectibles(ActiveHuntSnapshot active, IReadOnlyList<HunterInstance> hunters, PlayableHuntRoutePlan boundRoute, out string reason)
        {
            if (active.Collectibles == null)
            {
                reason = "活动狩猎采集物列表无效。";
                return false;
            }
            var huntersById = new Dictionary<int, HunterInstance>();
            var restoredItems = new Dictionary<int, List<ItemInstance>>();
            foreach (HunterInstance hunter in hunters)
            {
                huntersById.Add(hunter.InstanceId, hunter);
                restoredItems.Add(hunter.InstanceId, new List<ItemInstance>());
            }
            var restoredHunters = new HashSet<int>();
            foreach (ActiveHuntCollectibleSnapshot collectible in active.Collectibles)
            {
                if (collectible == null || !huntersById.TryGetValue(collectible.HunterId, out HunterInstance hunter) || !restoredHunters.Add(collectible.HunterId))
                {
                    reason = $"活动狩猎采集物引用了无效猎人：{collectible?.HunterId}";
                    return false;
                }
                if (collectible.Items == null)
                {
                    reason = $"猎人 {collectible.HunterId} 的采集物列表无效。";
                    return false;
                }
                foreach (ActiveHuntItemStackSnapshot savedItem in collectible.Items)
                {
                    if (savedItem == null || savedItem.Count <= 0 || !boundRoute.TryResolveItem(savedItem.ItemId, out ItemData item) || item == null)
                    {
                        reason = $"无法恢复狩猎采集物：{savedItem?.ItemId}";
                        return false;
                    }
                    restoredItems[hunter.InstanceId].Add(new ItemInstance(item, savedItem.Count));
                }
            }
            if (restoredHunters.Count != hunters.Count)
            {
                reason = "活动狩猎采集物未覆盖完整编队。";
                return false;
            }
            foreach (HunterInstance hunter in hunters)
            {
                hunter.Collectibles ??= new List<ItemInstance>();
                hunter.Collectibles.Clear();
                hunter.Collectibles.AddRange(restoredItems[hunter.InstanceId]);
            }
            reason = string.Empty;
            return true;
        }

        private static bool TryRestoreEventStore(ActiveHuntEventStoreSnapshot saved, PlayableHuntRoutePlan boundRoute, string expeditionId, out PlayableHuntEventOccurrenceStore store, out string reason)
        {
            saved ??= new ActiveHuntEventStoreSnapshot();
            if (boundRoute?.IsUsable != true)
            {
                store = null;
                reason = "活动狩猎事件恢复缺少当前可用路线。";
                return false;
            }
            var pending = new List<PlayableHuntEventOccurrenceRecord>();
            if (saved.PendingOccurrences == null || saved.CommittedSequences == null)
            {
                store = null;
                reason = "活动狩猎事件检查点列表无效。";
                return false;
            }
            if (saved.EventMemorySchemaVersion > ActiveHuntSnapshot.CurrentEventMemorySchemaVersion || saved.EventMemorySchemaVersion < ActiveHuntSnapshot.CurrentEventMemorySchemaVersion && (saved.Memories?.Count ?? 0) > 0)
            {
                store = null;
                reason = "活动狩猎事件结果记忆 schema 无效。";
                return false;
            }
            foreach (ActiveHuntEventOccurrenceSnapshot occurrence in saved.PendingOccurrences)
            {
                if (occurrence == null || occurrence.Sequence == 0 || string.IsNullOrWhiteSpace(occurrence.EventId))
                {
                    store = null;
                    reason = "活动狩猎事件 occurrence 无效。";
                    return false;
                }
                if (!boundRoute.TryResolveEvent(occurrence.EventId, out _))
                {
                    store = null;
                    reason = $"无法解析待恢复狩猎事件：{occurrence.EventId}";
                    return false;
                }
                foreach (string ancestorId in occurrence.AncestorEventIds ?? new List<string>())
                    if (!boundRoute.TryResolveEvent(ancestorId, out _))
                    {
                        store = null;
                        reason = $"无法解析待恢复狩猎事件 ancestor：{ancestorId}";
                        return false;
                    }
                PlayableEventRerollCheckpoint checkpoint = occurrence.RerollCheckpoint?.HasValue == true ? occurrence.RerollCheckpoint : null;
                var core = new PlayableEventChainOccurrence(occurrence.Sequence, occurrence.EventId, occurrence.EventName, occurrence.Year, occurrence.ActorId, occurrence.AncestorEventIds, checkpoint);
                pending.Add(new PlayableHuntEventOccurrenceRecord(core, new Vector2Int(occurrence.X, occurrence.Y), occurrence.AncestorEventIds));
            }
            var state = new PlayableHuntEventOccurrenceStoreState { NextSequence = saved.NextSequence, NextRootSequence = saved.NextRootSequence, CommittedSequences = saved.CommittedSequences, PendingOccurrences = pending, Memories = saved.Memories, Diagnostic = saved.Diagnostic };
            return PlayableHuntEventOccurrenceStore.TryRestore(state, eventId => boundRoute.TryResolveEvent(eventId, out EventData gameEvent) ? gameEvent : null, out store, out reason, expeditionId);
        }


        private static HuntTileDefinition CreateDefinition(HexTileData data) => new(data.ContentId, data.spawnWeight, data.spawnInGroup, data.groupSize, data.bossEncounterWeight, data.mustBeAdjacent);
    }
}
