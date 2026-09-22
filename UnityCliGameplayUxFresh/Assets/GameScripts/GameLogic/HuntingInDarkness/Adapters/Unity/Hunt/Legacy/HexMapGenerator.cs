using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Board;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunt;
using UnityEngine;
using CoreMapGenerator = HuntingInDarkness.GameCore.Hunt.HuntMapGenerator;

namespace HuntingInDarkness.Hunt
{
    /// <summary>
    /// Unity data adapter for the engine-independent hunt map generator.
    /// ScriptableObject templates and Vector coordinates are translated at this boundary.
    /// </summary>
    public class HexMapGenerator
    {
        private readonly CoreMapGenerator _core;

        public HexMapGenerator(IRandomSource random, int mapRadius = 3)
        {
            _core = new CoreMapGenerator(random, mapRadius);
        }

        public Dictionary<Vector2Int, HexTileInstance> GenerateMap(
            List<HexTileData> tilePool,
            HexTileData startingTileConfig = null)
        {
            var lookup = new Dictionary<string, HexTileData>();
            var definitions = new List<HuntTileDefinition>();
            if (tilePool != null)
            {
                foreach (HexTileData tile in tilePool)
                {
                    if (tile == null) continue;
                    HuntTileDefinition definition = ToDefinition(tile);
                    definitions.Add(definition);
                    lookup[definition.Id] = tile;
                }
            }

            HuntTileDefinition startingDefinition = startingTileConfig != null
                ? ToDefinition(startingTileConfig)
                : null;
            if (startingDefinition != null)
                lookup[startingDefinition.Id] = startingTileConfig;

            HuntMapState generated = _core.Generate(definitions, startingDefinition);
            var result = new Dictionary<Vector2Int, HexTileInstance>();
            bool hasBossEncounter = false;
            foreach (KeyValuePair<GridPosition, HuntTileState> pair in generated.Tiles)
            {
                HexTileData config = null;
                if (pair.Value.Definition != null)
                    lookup.TryGetValue(pair.Value.Definition.Id, out config);
                var instance = new HexTileInstance
                {
                    AxialCoord = ToUnity(pair.Key),
                    Config = config,
                    ConfigName = config != null ? config.name : "",
                    ConfigId = config != null ? config.ContentId : string.Empty
                };
                instance.AttachDomainState(pair.Value);
                result[instance.AxialCoord] = instance;
                hasBossEncounter |= pair.Value.HasBossEncounter;
            }
            if (hasBossEncounter)
                Debug.Log("[HexMapGenerator] Boss遭遇地块已由 GameCore 规则放置");
            return result;
        }

        public List<Vector2Int> RevealTile(
            Dictionary<Vector2Int, HexTileInstance> map,
            Vector2Int coord,
            System.Action<HexTileInstance> spawnResourcesCallback)
        {
            if (!RevealTileDeferred(map, coord, spawnResourcesCallback)) return new List<Vector2Int>();
            return UnlockNeighbors(map, coord);
        }

        public bool RevealTileDeferred(Dictionary<Vector2Int, HexTileInstance> map, Vector2Int coord, System.Action<HexTileInstance> spawnResourcesCallback)
        {
            if (!TryBuildDomainMap(map, coord, HuntTileVisibility.Interactable, out HuntMapState domainMap, out HexTileInstance selected)) return false;
            if (!CoreMapGenerator.RevealOnly(domainMap, ToCore(coord))) return false;
            selected.SyncFromDomain();
            spawnResourcesCallback?.Invoke(selected);
            return selected.State == TileState.Revealed;
        }

        public List<Vector2Int> UnlockNeighbors(Dictionary<Vector2Int, HexTileInstance> map, Vector2Int coord)
        {
            if (!TryBuildDomainMap(map, coord, HuntTileVisibility.Revealed, out HuntMapState domainMap, out _)) return new List<Vector2Int>();
            List<GridPosition> unlocked = CoreMapGenerator.UnlockNeighbors(domainMap, ToCore(coord));
            var result = new List<Vector2Int>(unlocked.Count);
            foreach (GridPosition position in unlocked)
            {
                Vector2Int unityPosition = ToUnity(position);
                if (map.TryGetValue(unityPosition, out HexTileInstance tile))
                    tile.SyncFromDomain();
                result.Add(unityPosition);
            }
            return result;
        }

        public static List<Vector2Int> GetNeighbors(Vector2Int coord)
        {
            List<GridPosition> positions = CoreMapGenerator.GetNeighbors(ToCore(coord));
            var result = new List<Vector2Int>(positions.Count);
            foreach (GridPosition position in positions) result.Add(ToUnity(position));
            return result;
        }

        public static int GetDistance(Vector2Int a, Vector2Int b) =>
            HuntNavigationState.HexDistance(ToCore(a), ToCore(b));

        public static Vector3 AxialToWorld(Vector2Int coord, float cellSize)
        {
            float x = cellSize * (Mathf.Sqrt(3f) * coord.x + Mathf.Sqrt(3f) / 2f * coord.y);
            float z = cellSize * (1.5f * coord.y);
            return new Vector3(x, 0f, z);
        }

        private static HuntTileDefinition ToDefinition(HexTileData data) =>
            new HuntTileDefinition(
                data.ContentId,
                data.spawnWeight,
                data.spawnInGroup,
                data.groupSize,
                data.bossEncounterWeight,
                data.mustBeAdjacent);

        private static GridPosition ToCore(Vector2Int value) =>
            new GridPosition(value.x, value.y);

        private static bool TryBuildDomainMap(Dictionary<Vector2Int, HexTileInstance> map, Vector2Int coord, HuntTileVisibility expectedVisibility, out HuntMapState domainMap, out HexTileInstance selected)
        {
            domainMap = new HuntMapState();
            selected = null;
            if (map == null || !map.TryGetValue(coord, out selected) || selected.DomainState == null || selected.DomainState.Visibility != expectedVisibility) return false;
            foreach (KeyValuePair<Vector2Int, HexTileInstance> pair in map)
                if (pair.Value.DomainState != null)
                    domainMap.Tiles[ToCore(pair.Key)] = pair.Value.DomainState;
            return true;
        }

        private static Vector2Int ToUnity(GridPosition value) =>
            new Vector2Int(value.X, value.Y);
    }
}
