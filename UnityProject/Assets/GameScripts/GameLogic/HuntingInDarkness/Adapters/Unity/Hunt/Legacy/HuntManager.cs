using System.Collections.Generic;
using Core;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.GameCore.Board;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunt;
using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    /// <summary>
    /// 狩猎阶段管理器（纯 C#）。
    /// 协调地图生成、移动、资源采集、事件触发、Boss遭遇。
    /// GameManager 在切入 Hunt 阶段时调用 OnEnter()。
    /// </summary>
    public class HuntManager
    {
        // ─── 子系统 ───────────────────────────────────────────────

        public HexMapGenerator   MapGen        { get; private set; }
        public ResourceSystem    Resources     { get; private set; }
        public HuntEventSystem   HuntEvents    { get; private set; }
        private readonly EventSystem _eventSystem;

        // ─── 地图状态 ─────────────────────────────────────────────

        public Dictionary<Vector2Int, HexTileInstance> Map { get; private set; } = new();
        private readonly HuntNavigationState _navigation = new();
        public Vector2Int SquadPosition => ToUnity(_navigation.SquadPosition);
        public float      CellSize      { get; } = 2.0f;

        // ─── 猎人 ─────────────────────────────────────────────────

        public List<HunterInstance>  ActiveHunters   { get; private set; } = new();
        public HunterInstance        SelectedHunter  { get; private set; }

        // ─── 配置（运行时注入）────────────────────────────────────

        public List<HexTileData> TilePool           { get; set; } = new();
        public HexTileData       StartingTileConfig  { get; set; }

        private readonly IRandomSource _rng;

        // ─── 回调（由 GameManager / HuntUIManager 注入）───────────

        /// <summary>地块状态改变时（翻开/可交互变更）</summary>
        public System.Action<Vector2Int, TileState> OnTileStateChanged;

        /// <summary>小队移动后</summary>
        public System.Action<Vector2Int> OnSquadMoved;

        /// <summary>需要显示资源采集 UI 时</summary>
        public System.Action<ResourcePointInstance, HunterInstance> OnResourcePointClicked;

        /// <summary>资源点采集状态改变后</summary>
        public System.Action<ResourcePointInstance> OnResourcePointHarvested;

        /// <summary>触发 Boss 战（切 BossFight 阶段）</summary>
        public System.Action OnBossEncounterTriggered;

        /// <summary>狩猎阶段结束（切回 Settlement）</summary>
        public System.Action<HuntRecord> OnHuntCompleted;

        // ─── 构造 ────────────────────────────────────────────────

        public HuntManager(EventSystem sharedEventSystem, int seed = 0)
        {
            _rng         = seed != 0 ? new SystemRandomSource(seed) : new SystemRandomSource();
            _eventSystem = sharedEventSystem;
            MapGen       = new HexMapGenerator(_rng, mapRadius: 3);
            Resources    = new ResourceSystem(_rng);
            HuntEvents   = new HuntEventSystem(_eventSystem, _rng);
            PlayableHuntContentRuntime.ApplyTo(this);
        }

        // ─── 生命周期 ────────────────────────────────────────────

        /// <summary>进入狩猎阶段（由 GameManager 调用）</summary>
        public void OnEnter(List<HunterInstance> hunters, int currentYear = 1)
        {
            ActiveHunters  = hunters ?? new List<HunterInstance>();
            SelectedHunter = ActiveHunters.Count > 0 ? ActiveHunters[0] : null;
            _navigation.Reset();
            HuntEvents.ResetSession(currentYear);

            Map = MapGen.GenerateMap(TilePool, StartingTileConfig);

            Debug.Log($"[HuntManager] 狩猎阶段开始，猎人数量: {ActiveHunters.Count}，地图格子: {Map.Count}");

            // 通知 UI 初始化
            foreach (var kv in Map)
                OnTileStateChanged?.Invoke(kv.Key, kv.Value.State);
        }

        /// <summary>离开狩猎阶段（Boss死亡或主动撤退）</summary>
        public void OnExit(SettlementInstance settlement)
        {
            // 将采集物转入营地存储
            Resources.TransferCollectibles(ActiveHunters, settlement);
            Debug.Log("[HuntManager] 狩猎阶段结束，采集物已转入营地");
        }

        // ─── 地图交互 ────────────────────────────────────────────

        /// <summary>
        /// 玩家点击一个地块。
        ///   - 若为「可交互」→ 翻开（消耗行动点/资源）
        ///   - 若为「已翻开」且相邻 → 移动小队
        ///   - 若为「已翻开」且有资源点 → 查询资源点交互
        /// </summary>
        public void OnTileClicked(Vector2Int coord)
        {
            if (PlayableHuntInputGuard.IsBlocked) return;
            if (!Map.TryGetValue(coord, out var tile)) return;

            if (tile.State == TileState.Interactable)
            {
                RevealTile(coord);
            }
            else if (tile.State == TileState.Revealed)
            {
                if (IsAdjacentToSquad(coord))
                    MoveSquad(coord);
            }
        }

        private void RevealTile(Vector2Int coord)
        {
            var newInteractable = MapGen.RevealTile(Map, coord, tile =>
                Resources.SpawnResourcePoints(tile));

            OnTileStateChanged?.Invoke(coord, TileState.Revealed);
            foreach (var n in newInteractable)
                OnTileStateChanged?.Invoke(n, TileState.Interactable);

            var tile = Map[coord];
            HuntEvents.OnTileRevealed(tile, SelectedHunter);

            // Boss遭遇：翻开 Boss 地块 → 直接切 Boss 战
            if (tile.HasBossEncounter)
            {
                Debug.Log("[HuntManager] 翻开了 Boss 遭遇地块！");
                OnBossEncounterTriggered?.Invoke();
            }

            EventBus.Publish(new GameEventTriggeredEvent
                { EventId = $"tile_reveal:{coord.x},{coord.y}" });
        }

        private void MoveSquad(Vector2Int target)
        {
            if (!Map.TryGetValue(target, out var tile)) return;
            if (tile.State != TileState.Revealed) return;

            _navigation.MoveTo(ToCore(target));
            OnSquadMoved?.Invoke(target);
            HuntEvents.OnSquadMoved(tile, SelectedHunter);

            // Boss遭遇：移动到 Boss 地块 → 触发战斗
            if (tile.HasBossEncounter)
                OnBossEncounterTriggered?.Invoke();

            Debug.Log($"[HuntManager] 小队移动到 {target}");
        }

        // ─── 资源采集 ─────────────────────────────────────────────

        /// <summary>玩家点击地块上的资源点</summary>
        public void OnResourcePointSelected(Vector2Int tileCoord, int pointIndex)
        {
            if (PlayableHuntInputGuard.IsBlocked) return;
            if (!Map.TryGetValue(tileCoord, out var tile)) return;
            if (pointIndex < 0 || pointIndex >= tile.ResourcePoints.Count) return;

            var point = tile.ResourcePoints[pointIndex];
            if (point.IsExhausted) return;

            OnResourcePointClicked?.Invoke(point, SelectedHunter);
        }

        /// <summary>执行采集（由 UI 确认后调用）</summary>
        public List<ItemInstance> ExecuteHarvest(ResourcePointInstance point)
        {
            var obtained = Resources.Harvest(point, SelectedHunter);
            OnResourcePointHarvested?.Invoke(point);
            return obtained;
        }

        public PlayableHarvestTransaction PrepareHarvest(ResourcePointInstance point) => Resources.PrepareHarvest(point, SelectedHunter);

        public IReadOnlyList<ItemInstance> CompleteHarvest(PlayableHarvestTransaction transaction)
        {
            IReadOnlyList<ItemInstance> obtained = Resources.CommitHarvest(transaction);
            if (transaction?.Point != null)
                OnResourcePointHarvested?.Invoke(transaction.Point);
            return obtained;
        }

        // ─── 狩猎结束 ─────────────────────────────────────────────

        /// <summary>
        /// 主动撤退或 Boss 战结算后调用。
        /// 创建狩猎记录并回调 OnHuntCompleted。
        /// </summary>
        public void CompleteHunt(bool bossDefeated, SettlementInstance settlement)
        {
            // 统计收集资源
            var resourceList = new List<string>();
            foreach (var h in ActiveHunters)
                foreach (var item in h.Collectibles)
                    resourceList.Add(item.Data.itemName);

            var record = new HuntRecord
            {
                Year             = settlement.CurrentYear,
                HuntersDeployed  = ActiveHunters.Count,
                HuntersLost      = ActiveHunters.FindAll(h => !h.IsAlive).Count,
                BossDefeated     = bossDefeated,
                CollectedResources = resourceList
            };

            OnExit(settlement);
            OnHuntCompleted?.Invoke(record);
        }

        // ─── 工具 ─────────────────────────────────────────────────

        public bool IsAdjacentToSquad(Vector2Int coord)
        {
            return _navigation.IsAdjacent(ToCore(coord));
        }

        public void SelectHunter(int hunterId)
        {
            SelectedHunter = ActiveHunters.Find(h => h.InstanceId == hunterId);
        }

        public HexTileInstance GetTile(Vector2Int coord)
        {
            Map.TryGetValue(coord, out var t);
            return t;
        }

        public Vector3 TileToWorld(Vector2Int coord)
            => HexMapGenerator.AxialToWorld(coord, CellSize);

        private static GridPosition ToCore(Vector2Int value) =>
            new GridPosition(value.x, value.y);

        private static Vector2Int ToUnity(GridPosition value) =>
            new Vector2Int(value.X, value.Y);
    }
}
