using System.Collections.Generic;
using HuntingInDarkness.Data;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HuntingInDarkness.Hunt
{
    /// <summary>
    /// 狩猎地图 3D 可视化（MonoBehaviour）。
    /// 挂载在 HuntRoot 下的一个 GameObject 上。
    /// 为每个 HexTileInstance 创建平面六边形卡片 GameObject，
    /// 并根据 TileState 切换材质颜色。
    ///
    /// 颜色约定：
    ///   Locked      → 深灰（背面朝上效果）
    ///   Interactable→ 蓝白（可点击）
    ///   Revealed    → 按地块类型染色（绿/棕/蓝等）
    ///
    /// 点击检测：Raycast → 调用 HuntManager.OnTileClicked。
    /// </summary>
    public class HuntMapVisualizer : MonoBehaviour
    {
        // ─── 颜色配置 ─────────────────────────────────────────────

        private static readonly Color LockedColor       = new(0.20f, 0.20f, 0.22f, 1f);
        private static readonly Color InteractableColor = new(0.30f, 0.55f, 0.85f, 1f);
        private static readonly Color StartingColor     = new(0.40f, 0.75f, 0.40f, 1f);
        private static readonly Color PlainsColor       = new(0.55f, 0.72f, 0.35f, 1f);
        private static readonly Color ForestColor       = new(0.20f, 0.50f, 0.25f, 1f);
        private static readonly Color RuinsColor        = new(0.55f, 0.50f, 0.40f, 1f);
        private static readonly Color CaveColor         = new(0.30f, 0.28f, 0.32f, 1f);
        private static readonly Color SwampColor        = new(0.35f, 0.48f, 0.30f, 1f);
        private static readonly Color MountainColor     = new(0.52f, 0.50f, 0.50f, 1f);
        private static readonly Color BossMarkerColor   = new(0.80f, 0.20f, 0.20f, 1f);

        // ─── 引用 ─────────────────────────────────────────────────

        private HuntManager _huntMgr;
        private readonly Dictionary<Vector2Int, GameObject> _tileObjects = new();
        private readonly Dictionary<Vector2Int, Renderer>   _tileRenderers = new();
        private readonly Dictionary<Vector2Int, GameObject> _resourceMarkers = new();
        private PlayableHuntSquadPawn3D squadPawn;
        private PlayableHuntMapIntroCamera3D mapIntroCamera;

        public Transform TabletopInteractionAnchor => squadPawn != null ? squadPawn.transform : transform;

        public bool TryGetResourcePointPresentationPosition(ResourcePointInstance point, out Vector3 position)
        {
            position = default;
            if (point == null || _huntMgr == null) return false;
            foreach (KeyValuePair<Vector2Int, HexTileInstance> pair in _huntMgr.Map)
            {
                if (!pair.Value.ResourcePoints.Contains(point) || !_resourceMarkers.TryGetValue(pair.Key, out GameObject marker) || marker == null) continue;
                position = marker.transform.position + new Vector3(0f, 0.58f, -1.55f);
                return true;
            }
            return false;
        }

        // ─── 初始化 ──────────────────────────────────────────────

        public void Init(HuntManager huntMgr)
        {
            ClearVisuals();
            _huntMgr = huntMgr;
            huntMgr.OnTileStateChanged  = OnTileStateChanged;
            huntMgr.OnSquadMoved        = OnSquadMoved;
            huntMgr.OnResourcePointHarvested = OnResourcePointHarvested;

            BuildAllTiles();
            PlaceSquadToken(huntMgr.SquadPosition);
            PresentMapIntro();
        }

        private void PresentMapIntro()
        {
            Camera presentationCamera = Camera.main;
            if (presentationCamera == null || _tileObjects.Count == 0) return;
            mapIntroCamera ??= GetComponent<PlayableHuntMapIntroCamera3D>() ?? gameObject.AddComponent<PlayableHuntMapIntroCamera3D>();
            var tilePositions = new List<Vector3>(_tileObjects.Count);
            foreach (GameObject tileObject in _tileObjects.Values)
                if (tileObject != null) tilePositions.Add(tileObject.transform.position);
            mapIntroCamera.Present(presentationCamera, tilePositions);
        }

        private void BuildAllTiles()
        {
            foreach (var kv in _huntMgr.Map)
                CreateTileObject(kv.Key, kv.Value);
        }

        private void CreateTileObject(Vector2Int coord, HexTileInstance tile)
        {
            var worldPos = _huntMgr.TileToWorld(coord);
            var go = PlayableHexTileFactory.Create(_huntMgr.CellSize * 0.92f);
            go.name = $"Tile_{coord.x}_{coord.y}";
            go.transform.SetParent(transform);
            go.transform.position  = worldPos;

            var rend = go.GetComponent<Renderer>();
            rend.material = new Material(Shader.Find("Standard"));
            _tileObjects[coord]   = go;
            _tileRenderers[coord] = rend;

            ApplyTileColor(coord, tile.State, tile);

            // 点击组件
            var clicker = go.AddComponent<TileClickHandler>();
            clicker.Coord      = coord;
            clicker.HuntMgr    = _huntMgr;
            clicker.Visualizer = this;
        }

        // ─── 颜色更新 ─────────────────────────────────────────────

        private void ApplyTileColor(Vector2Int coord, TileState state, HexTileInstance tile)
        {
            if (!_tileRenderers.TryGetValue(coord, out var rend)) return;

            Color c;
            if (state == TileState.Locked)
                c = LockedColor;
            else if (state == TileState.Interactable)
                c = InteractableColor;
            else  // Revealed
                c = GetRevealedColor(tile);

            rend.material.color = c;
            _tileObjects[coord].GetComponent<PlayableHexTileCard3D>()?.Present(tile, state);

            // 浮动标记（已翻开且有 Boss 遭遇）
            if (state == TileState.Revealed && tile.HasBossEncounter)
                AddBossMarker(coord);

            // 资源点标记
            if (state == TileState.Revealed && tile.ResourcePoints.Count > 0)
                UpdateResourceMarkers(coord, tile);
        }

        private Color GetRevealedColor(HexTileInstance tile)
        {
            if (tile.Config == null) return PlainsColor;
            return tile.Config.tileType switch
            {
                TileType.Starting  => StartingColor,
                TileType.Plains    => PlainsColor,
                TileType.Forest    => ForestColor,
                TileType.Ruins     => RuinsColor,
                TileType.Cave      => CaveColor,
                TileType.Swamp     => SwampColor,
                TileType.Mountain  => MountainColor,
                _                  => PlainsColor
            };
        }

        // ─── 事件回调 ─────────────────────────────────────────────

        private void OnTileStateChanged(Vector2Int coord, TileState newState)
        {
            if (_huntMgr.Map.TryGetValue(coord, out var tile))
                ApplyTileColor(coord, newState, tile);
        }

        private void OnSquadMoved(Vector2Int newPos)
        {
            PlaceSquadToken(newPos);
        }

        private void OnResourcePointHarvested(ResourcePointInstance point)
        {
            foreach (var pair in _huntMgr.Map)
            {
                if (!pair.Value.ResourcePoints.Contains(point)) continue;
                UpdateResourceMarkers(pair.Key, pair.Value);
                return;
            }
        }

        // ─── 小队 Token ───────────────────────────────────────────

        private void PlaceSquadToken(Vector2Int coord)
        {
            var worldPos = _huntMgr.TileToWorld(coord) + Vector3.up * 0.3f;

            if (squadPawn == null)
            {
                var pawnObject = new GameObject("HuntSquadPawn3D");
                pawnObject.transform.SetParent(transform, false);
                squadPawn = pawnObject.AddComponent<PlayableHuntSquadPawn3D>();
                squadPawn.Initialize(_huntMgr.ActiveHunters.Count);
                squadPawn.Place(worldPos, true);
                return;
            }
            squadPawn.Place(worldPos, false);
        }

        // ─── 资源点标记 ───────────────────────────────────────────

        public void UpdateResourceMarkers(Vector2Int coord, HexTileInstance tile)
        {
            // 移除旧标记
            if (_resourceMarkers.TryGetValue(coord, out var old))
            {
                Destroy(old);
                _resourceMarkers.Remove(coord);
            }

            int activePoints = tile.ResourcePoints.FindAll(p => !p.IsExhausted).Count;
            if (activePoints <= 0) return;

            var parent = _tileObjects[coord];
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "ResourceMarker";
            marker.transform.SetParent(parent.transform);
            marker.transform.localPosition = new Vector3(0.5f, 0.28f, 0.35f);
            marker.transform.localScale    = Vector3.one * 0.35f;
            marker.GetComponent<Renderer>().material = new Material(Shader.Find("Standard"));
            marker.GetComponent<Renderer>().material.color = new Color(1f, 0.9f, 0.2f);
            var clickHandler = marker.AddComponent<ResourceMarkerClickHandler>();
            clickHandler.Initialize(_huntMgr, coord);

            _resourceMarkers[coord] = marker;
        }

        private void AddBossMarker(Vector2Int coord)
        {
            var parent = _tileObjects[coord];
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "BossMarker";
            marker.transform.SetParent(parent.transform);
            marker.transform.localPosition = new Vector3(0f, 0.32f, 0f);
            marker.transform.localScale    = Vector3.one * 0.48f;
            marker.GetComponent<Renderer>().material = new Material(Shader.Find("Standard"));
            marker.GetComponent<Renderer>().material.color = BossMarkerColor;
            Destroy(marker.GetComponent<Collider>());
        }

        // ─── 清理 ─────────────────────────────────────────────────

        private void OnDestroy()
        {
            ClearVisuals();
        }

        private void ClearVisuals()
        {
            if (_huntMgr != null && _huntMgr.OnTileStateChanged == OnTileStateChanged)
                _huntMgr.OnTileStateChanged = null;
            if (_huntMgr != null && _huntMgr.OnSquadMoved == OnSquadMoved)
                _huntMgr.OnSquadMoved = null;
            if (_huntMgr != null && _huntMgr.OnResourcePointHarvested == OnResourcePointHarvested)
                _huntMgr.OnResourcePointHarvested = null;
            foreach (var go in _tileObjects.Values) if (go != null) Destroy(go);
            if (squadPawn != null)
            {
                squadPawn.gameObject.SetActive(false);
                Destroy(squadPawn.gameObject);
            }
            _tileObjects.Clear();
            _tileRenderers.Clear();
            _resourceMarkers.Clear();
            squadPawn = null;
        }
    }
}
