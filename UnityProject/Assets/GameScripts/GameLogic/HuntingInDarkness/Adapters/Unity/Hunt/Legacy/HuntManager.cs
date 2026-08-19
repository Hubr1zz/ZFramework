using System.Collections.Generic;
using Core;
using Cysharp.Threading.Tasks;
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

        /// <summary>正式入口由 Hunt ActionSession 注入；为空时保留旧同步兼容路径。</summary>
        public System.Func<Vector2Int, UniTask> RequestTileInteraction;
        public System.Func<ResourcePointInstance, UniTask<PlayableHarvestTransaction>> RequestPrepareHarvest;
        public System.Func<PlayableHarvestTransaction, UniTask<PlayableHarvestStepResult>> RequestAdvanceHarvest;

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
            if (RequestTileInteraction != null)
            {
                RequestTileInteraction(coord).Forget();
                return;
            }
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
            if (!TryCommitTileInteraction(coord, HuntTileInteractionKind.Reveal, out HuntTileInteractionCommit commit)) return;
            ResolveTileInteractionEvent(commit);
            if (commit.BossEncounter)
                NotifyBossEncounter(commit);
            EventBus.Publish(new GameEventTriggeredEvent { EventId = $"tile_reveal:{coord.x},{coord.y}" });
        }

        private void MoveSquad(Vector2Int target)
        {
            if (!TryCommitTileInteraction(target, HuntTileInteractionKind.Move, out HuntTileInteractionCommit commit)) return;
            ResolveTileInteractionEvent(commit);
            if (commit.BossEncounter)
                NotifyBossEncounter(commit);
        }

        public bool TryCommitTileInteraction(Vector2Int coordinate, HuntTileInteractionKind intendedKind, out HuntTileInteractionCommit commit)
        {
            commit = default;
            if (PlayableHuntInputGuard.IsBlocked || !Map.TryGetValue(coordinate, out HexTileInstance tile)) return false;
            if (intendedKind == HuntTileInteractionKind.Reveal && tile.State == TileState.Interactable)
            {
                List<Vector2Int> newlyInteractable = MapGen.RevealTile(Map, coordinate, revealed => Resources.SpawnResourcePoints(revealed));
                if (tile.State != TileState.Revealed) return false;
                OnTileStateChanged?.Invoke(coordinate, TileState.Revealed);
                foreach (Vector2Int neighbor in newlyInteractable)
                    OnTileStateChanged?.Invoke(neighbor, TileState.Interactable);
                commit = new HuntTileInteractionCommit(HuntTileInteractionKind.Reveal, coordinate, tile, newlyInteractable);
                return true;
            }
            if (intendedKind != HuntTileInteractionKind.Move || tile.State != TileState.Revealed || !IsAdjacentToSquad(coordinate)) return false;

            _navigation.MoveTo(ToCore(coordinate));
            OnSquadMoved?.Invoke(coordinate);
            Debug.Log($"[HuntManager] 小队移动到 {coordinate}");
            commit = new HuntTileInteractionCommit(HuntTileInteractionKind.Move, coordinate, tile, null);
            return true;
        }

        internal void ResolveTileInteractionEvent(HuntTileInteractionCommit commit)
        {
            if (!commit.IsCommitted) return;
            if (commit.Kind == HuntTileInteractionKind.Reveal)
                HuntEvents.OnTileRevealed(commit.Tile, SelectedHunter);
            else if (commit.Kind == HuntTileInteractionKind.Move)
                HuntEvents.OnSquadMoved(commit.Tile, SelectedHunter);
        }

        internal void NotifyBossEncounter(HuntTileInteractionCommit commit)
        {
            if (!commit.IsCommitted || !commit.BossEncounter) return;
            Debug.Log(commit.Kind == HuntTileInteractionKind.Reveal ? "[HuntManager] 翻开了 Boss 遭遇地块！" : $"[HuntManager] 移动到 Boss 遭遇地块 {commit.Coordinate}");
            OnBossEncounterTriggered?.Invoke();
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

        public bool IsHarvestablePoint(ResourcePointInstance point)
        {
            if (point == null || point.IsExhausted || point.Resource == null) return false;
            foreach (HexTileInstance tile in Map.Values)
                if (tile.State == TileState.Revealed && tile.ResourcePoints.Contains(point))
                    return true;
            return false;
        }

        /// <summary>执行采集（由 UI 确认后调用）</summary>
        public List<ItemInstance> ExecuteHarvest(ResourcePointInstance point)
        {
            var obtained = Resources.Harvest(point, SelectedHunter);
            OnResourcePointHarvested?.Invoke(point);
            return obtained;
        }

        public PlayableHarvestTransaction PrepareHarvest(ResourcePointInstance point) => Resources.PrepareHarvest(point, SelectedHunter);

        public UniTask<PlayableHarvestTransaction> PrepareHarvestAsync(ResourcePointInstance point)
        {
            if (RequestPrepareHarvest != null) return RequestPrepareHarvest(point);
            return UniTask.FromResult(PrepareHarvest(point));
        }

        public UniTask<PlayableHarvestStepResult> AdvanceHarvestAsync(PlayableHarvestTransaction transaction)
        {
            if (RequestAdvanceHarvest != null) return RequestAdvanceHarvest(transaction);
            if (transaction == null) return UniTask.FromResult(PlayableHarvestStepResult.Failed("采集事务不存在"));
            if (transaction.IsCancelled) return UniTask.FromResult(PlayableHarvestStepResult.Failed("采集事务已经取消"));
            if (!transaction.CanReveal && !transaction.IsComplete) return UniTask.FromResult(PlayableHarvestStepResult.Failed("采集事务不可推进"));
            HarvestCardResult? revealedCard = transaction.CanReveal ? transaction.RevealNext() : null;
            if (transaction.CanReveal) return UniTask.FromResult(PlayableHarvestStepResult.Revealed(revealedCard.Value));
            IReadOnlyList<ItemInstance> obtained = CompleteHarvest(transaction);
            return UniTask.FromResult(PlayableHarvestStepResult.Completed(revealedCard, obtained));
        }

        public IReadOnlyList<ItemInstance> CompleteHarvest(PlayableHarvestTransaction transaction)
        {
            IReadOnlyList<ItemInstance> obtained = Resources.CommitHarvest(transaction);
            if (transaction?.Point != null)
                NotifyResourcePointHarvested(transaction.Point);
            return obtained;
        }

        internal void NotifyResourcePointHarvested(ResourcePointInstance point)
        {
            try
            {
                OnResourcePointHarvested?.Invoke(point);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
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
