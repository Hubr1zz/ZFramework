using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.Data;
using UnityEngine;

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
    public class HuntMapVisualizer : MonoBehaviour, IHuntTileInteractionPresenter
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
        private readonly Dictionary<Vector2Int, GameObject> bossMarkers = new();
        private readonly Dictionary<Vector2Int, List<PlayableHuntResourceMarker3D>> resourceMarkers = new();
        private PlayableHuntSquadPawn3D squadPawn;
        private PlayableHuntMapIntroCamera3D mapIntroCamera;
        private IHuntExplorationPort explorationPort;
        private HuntTileScoutPanel3D scoutPanel;
        private bool tileRequestInFlight;
        private int presentationGeneration;

        public Transform TabletopInteractionAnchor => squadPawn != null ? squadPawn.transform : transform;

        public bool TryGetResourcePointPresentationPosition(ResourcePointInstance point, out Vector3 position)
        {
            position = default;
            if (point == null) return false;
            foreach (List<PlayableHuntResourceMarker3D> markers in resourceMarkers.Values)
                foreach (PlayableHuntResourceMarker3D marker in markers)
                    if (marker != null && ReferenceEquals(marker.Point, point))
                    {
                        position = marker.PresentationPosition;
                        return true;
                    }
            return false;
        }

        public bool TryGetResourcePointPresentationPosition(Vector2Int coordinate, int pointIndex, out Vector3 position)
        {
            position = default;
            if (!resourceMarkers.TryGetValue(coordinate, out List<PlayableHuntResourceMarker3D> markers)) return false;
            foreach (PlayableHuntResourceMarker3D marker in markers)
                if (marker != null && marker.PointIndex == pointIndex)
                {
                    position = marker.PresentationPosition;
                    return true;
                }
            return false;
        }

        public async UniTask PresentAsync(HuntTileInteractionPresentationRequest request, CancellationToken cancellationToken)
        {
            if (request.Kind == HuntTileInteractionKind.Reveal && _tileObjects.TryGetValue(request.Coordinate, out GameObject tileObject) && tileObject != null)
            {
                PlayableHexTileCard3D tileCard = tileObject.GetComponent<PlayableHexTileCard3D>();
                while (tileCard != null && tileCard.isActiveAndEnabled && tileCard.IsFlipping)
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                return;
            }
            if (request.Kind == HuntTileInteractionKind.Move)
                while (squadPawn != null && squadPawn.isActiveAndEnabled && squadPawn.IsMoving)
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        public void HandleTileClicked(Vector2Int coordinate)
        {
            if (PlayableHuntInputGuard.IsBlocked || tileRequestInFlight || _huntMgr == null || explorationPort == null) return;
            if (!_huntMgr.Map.TryGetValue(coordinate, out HexTileInstance tile)) return;
            if (tile.State == TileState.Interactable)
            {
                PresentScoutPanel(coordinate, tile);
                return;
            }
            if (tile.State != TileState.Revealed || !explorationPort.TryCreateSnapshot(coordinate, -1, out HuntExplorationSnapshot snapshot)) return;
            tileRequestInFlight = true;
            SubmitTileAsync(snapshot).Forget();
        }

        // ─── 初始化 ──────────────────────────────────────────────

        public void Init(HuntManager huntMgr)
        {
            Init(huntMgr, null);
        }

        public void Init(HuntManager huntMgr, IHuntExplorationPort port)
        {
            ClearVisuals();
            _huntMgr = huntMgr;
            explorationPort = port;
            huntMgr.OnTileStateChanged  = OnTileStateChanged;
            huntMgr.OnSquadMoved        = OnSquadMoved;
            huntMgr.OnResourcePointHarvested = OnResourcePointHarvested;
            huntMgr.OnResourcePointStateChanged = OnResourcePointStateChanged;

            BuildAllTiles();
            PlaceSquadToken(huntMgr.SquadPosition);
            PresentMapIntro();
        }

        private void PresentMapIntro()
        {
            mapIntroCamera ??= GetComponent<PlayableHuntMapIntroCamera3D>() ?? gameObject.AddComponent<PlayableHuntMapIntroCamera3D>();
            Camera presentationCamera = Camera.main;
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
            clicker.Visualizer = this;
        }

        private void PresentScoutPanel(Vector2Int coordinate, HexTileInstance tile, string status = null)
        {
            scoutPanel ??= HuntTileScoutPanel3D.Create(transform);
            Vector3 position = _tileObjects.TryGetValue(coordinate, out GameObject tileObject) && tileObject != null ? tileObject.transform.position + Vector3.up * 0.8f : transform.position;
            scoutPanel.Present(position, coordinate, tile?.Config?.tileName ?? "未知地块", ConfirmScout, null, status);
        }

        private void ConfirmScout(Vector2Int coordinate)
        {
            if (tileRequestInFlight || _huntMgr == null || explorationPort == null) return;
            if (!explorationPort.TryCreateSnapshot(coordinate, -1, out HuntExplorationSnapshot snapshot))
            {
                Debug.LogWarning($"[HuntMapVisualizer] 地块 {coordinate} 确认侦察时快照已失效。");
                return;
            }
            tileRequestInFlight = true;
            SubmitTileAsync(snapshot).Forget();
        }

        private async UniTaskVoid SubmitTileAsync(HuntExplorationSnapshot snapshot)
        {
            int generation = presentationGeneration;
            string failureReason = null;
            try
            {
                var result = await explorationPort.SubmitTileAsync(snapshot);
                if (!result.Succeeded)
                    failureReason = string.IsNullOrWhiteSpace(result.Reason) ? "侦察未能完成，请重试。" : result.Reason;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                failureReason = "侦察未能完成，请重试。";
            }
            finally
            {
                if (generation == presentationGeneration)
                    tileRequestInFlight = false;
            }
            if (generation != presentationGeneration || string.IsNullOrWhiteSpace(failureReason) || _huntMgr == null) return;
            if (!_huntMgr.Map.TryGetValue(snapshot.Coordinate, out HexTileInstance tile) || tile.State != TileState.Interactable) return;
            PresentScoutPanel(snapshot.Coordinate, tile, failureReason);
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

            UpdateBossMarker(coord, state == TileState.Revealed && tile.HasBossEncounter);

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
            foreach (List<PlayableHuntResourceMarker3D> markers in resourceMarkers.Values)
                foreach (PlayableHuntResourceMarker3D marker in markers)
                    marker?.RefreshAvailability();
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

        private void OnResourcePointStateChanged(Vector2Int coordinate)
        {
            if (_huntMgr.Map.TryGetValue(coordinate, out HexTileInstance tile))
                UpdateResourceMarkers(coordinate, tile);
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
            if (resourceMarkers.TryGetValue(coord, out List<PlayableHuntResourceMarker3D> oldMarkers))
            {
                foreach (PlayableHuntResourceMarker3D oldMarker in oldMarkers)
                    if (oldMarker != null)
                    {
                        oldMarker.gameObject.SetActive(false);
                        Destroy(oldMarker.gameObject);
                    }
                resourceMarkers.Remove(coord);
            }

            if (tile?.ResourcePoints == null || !_tileObjects.TryGetValue(coord, out GameObject parent) || parent == null) return;
            var activePointIndices = new List<int>();
            for (int pointIndex = 0; pointIndex < tile.ResourcePoints.Count; pointIndex++)
                if (tile.ResourcePoints[pointIndex]?.IsExhausted == false) activePointIndices.Add(pointIndex);
            if (activePointIndices.Count == 0) return;

            var markers = new List<PlayableHuntResourceMarker3D>(activePointIndices.Count);
            for (int visualIndex = 0; visualIndex < activePointIndices.Count; visualIndex++)
            {
                int pointIndex = activePointIndices[visualIndex];
                if (!PlayableHuntResourceMarkerLayout.TryGetLocalPosition(visualIndex, activePointIndices.Count, 0.48f, out Vector3 localPosition)) continue;
                markers.Add(PlayableHuntResourceMarker3D.Create(parent.transform, _huntMgr, explorationPort, coord, pointIndex, tile.ResourcePoints[pointIndex], localPosition));
            }
            resourceMarkers[coord] = markers;
        }

        private void UpdateBossMarker(Vector2Int coord, bool visible)
        {
            if (!visible)
            {
                if (!bossMarkers.Remove(coord, out GameObject existingMarker) || existingMarker == null) return;
                existingMarker.SetActive(false);
                Destroy(existingMarker);
                return;
            }
            if (bossMarkers.TryGetValue(coord, out GameObject marker) && marker != null) return;
            if (!_tileObjects.TryGetValue(coord, out GameObject parent) || parent == null) return;

            marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "BossMarker";
            marker.transform.SetParent(parent.transform);
            marker.transform.localPosition = new Vector3(0f, 0.32f, 0f);
            marker.transform.localScale    = Vector3.one * 0.48f;
            marker.GetComponent<Renderer>().material = new Material(Shader.Find("Standard"));
            marker.GetComponent<Renderer>().material.color = BossMarkerColor;
            Destroy(marker.GetComponent<Collider>());
            bossMarkers[coord] = marker;
        }

        // ─── 清理 ─────────────────────────────────────────────────

        private void OnDestroy()
        {
            ClearVisuals();
        }

        private void ClearVisuals()
        {
            ++presentationGeneration;
            tileRequestInFlight = false;
            scoutPanel?.Close();
            if (scoutPanel != null)
                Destroy(scoutPanel.gameObject);
            scoutPanel = null;
            mapIntroCamera?.Skip();
            if (_huntMgr != null && _huntMgr.OnTileStateChanged == OnTileStateChanged)
                _huntMgr.OnTileStateChanged = null;
            if (_huntMgr != null && _huntMgr.OnSquadMoved == OnSquadMoved)
                _huntMgr.OnSquadMoved = null;
            if (_huntMgr != null && _huntMgr.OnResourcePointHarvested == OnResourcePointHarvested)
                _huntMgr.OnResourcePointHarvested = null;
            if (_huntMgr != null && _huntMgr.OnResourcePointStateChanged == OnResourcePointStateChanged)
                _huntMgr.OnResourcePointStateChanged = null;
            foreach (GameObject tileObject in _tileObjects.Values)
                if (tileObject != null)
                {
                    tileObject.SetActive(false);
                    Destroy(tileObject);
                }
            if (squadPawn != null)
            {
                squadPawn.gameObject.SetActive(false);
                Destroy(squadPawn.gameObject);
            }
            _tileObjects.Clear();
            _tileRenderers.Clear();
            bossMarkers.Clear();
            resourceMarkers.Clear();
            squadPawn = null;
            explorationPort = null;
        }
    }
}
