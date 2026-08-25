using System;
using System.Collections.Generic;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Events;
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
        internal EventSystem EventSystem => _eventSystem;
        internal IPlayableEventResourceCommand EventResourceCommand { get; }
        public IPlayableEventInput EventInput { get; set; }

        // ─── 地图状态 ─────────────────────────────────────────────

        public Dictionary<Vector2Int, HexTileInstance> Map { get; private set; } = new();
        private readonly HuntNavigationState _navigation = new();
        public Vector2Int SquadPosition => ToUnity(_navigation.SquadPosition);
        public float      CellSize      { get; } = 2.0f;

        // ─── 猎人 ─────────────────────────────────────────────────

        public List<HunterInstance>  ActiveHunters   { get; private set; } = new();
        public HunterInstance        SelectedHunter  { get; private set; }
        public bool HasLivingHunter => PlayableHuntSquadAvailability.HasLivingHunter(ActiveHunters);

        // ─── 配置（运行时注入）────────────────────────────────────

        private List<HexTileData> tilePool = new();
        private HexTileData startingTileConfig;
        private PlayableHuntNoiseProfile noiseProfile;
        public List<HexTileData> TilePool
        {
            get => boundRoute != null ? new List<HexTileData>(tilePool) : tilePool;
            set
            {
                if (boundRoute != null) return;
                tilePool = value ?? new List<HexTileData>();
            }
        }
        public HexTileData StartingTileConfig
        {
            get => startingTileConfig;
            set
            {
                if (boundRoute != null) return;
                startingTileConfig = value;
            }
        }
        public PlayableHuntNoiseProfile NoiseProfile
        {
            get => noiseProfile;
            set
            {
                if (boundRoute != null) return;
                noiseProfile = value;
            }
        }
        private PlayableHuntRoutePlan boundRoute;
        private bool runtimeStarted;
        public PlayableHuntRoutePlan BoundRoute => boundRoute;
        public PlayableHuntContentBundle BoundContentBundle => boundRoute?.Owner;
        public bool HasBoundContent => boundRoute != null;
        public string ContentBundleId => boundRoute?.ContentBundleId ?? string.Empty;
        public PlayableHuntNoiseResolution LastNoiseResolution { get; private set; }
        public int CurrentYear { get; private set; } = 1;

        private readonly StatefulRandomSource _rng;

        // ─── 回调（由 GameManager / HuntUIManager 注入）───────────

        /// <summary>地块状态改变时（翻开/可交互变更）</summary>
        public System.Action<Vector2Int, TileState> OnTileStateChanged;

        /// <summary>小队移动后</summary>
        public System.Action<Vector2Int> OnSquadMoved;

        /// <summary>需要显示资源采集 UI 时</summary>
        public System.Action<ResourcePointInstance, HunterInstance> OnResourcePointClicked;

        /// <summary>正式世界空间采集桌的只读展示请求；不携带 ResourcePointInstance。</summary>
        public System.Action<HuntResourcePointPresentationRequest> OnResourcePointPresentationRequested;

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

        public HuntManager(EventSystem sharedEventSystem, int seed = 0, bool bindInitialContent = true)
        {
            _rng         = new StatefulRandomSource(seed != 0 ? seed : System.Environment.TickCount);
            _eventSystem = sharedEventSystem;
            MapGen       = new HexMapGenerator(_rng, mapRadius: 3);
            Resources    = new ResourceSystem(_rng);
            HuntEvents   = new HuntEventSystem(_rng);
            EventResourceCommand = new HuntEventResourceCommand(this);
            if (bindInitialContent) PlayableHuntContentRuntime.ApplyTo(this);
        }

        /// <summary>原子绑定一个冻结的狩猎内容世代；旧的公开配置属性仍保留给兼容入口。</summary>
        public bool TryBindContent(PlayableHuntRoutePlan plan, out string reason)
        {
            if (plan?.IsUsable != true || plan.Owner?.Owns(plan) != true)
            {
                reason = "狩猎路线内容计划未完整配置。";
                return false;
            }
            if (ReferenceEquals(boundRoute, plan))
            {
                reason = string.Empty;
                return true;
            }
            if (boundRoute != null || runtimeStarted)
            {
                reason = runtimeStarted ? "狩猎运行态已经建立，不能替换内容。" : "狩猎管理器已经绑定了另一个内容计划。";
                return false;
            }

            var tilePool = new List<HexTileData>(plan.TilePool);
            var huntEvents = new List<EventData>(plan.HuntEvents);
            if (tilePool.Count == 0 || plan.StartingTile == null || huntEvents.Exists(gameEvent => gameEvent == null))
            {
                reason = "狩猎路线缺少起始地块、地块池或包含空事件。";
                return false;
            }
            startingTileConfig = plan.StartingTile;
            this.tilePool = tilePool;
            HuntEvents.BindContent(huntEvents);
            noiseProfile = plan.NoiseProfile;
            boundRoute = plan;
            reason = string.Empty;
            return true;
        }

        public bool BindContent(PlayableHuntRoutePlan plan, out string reason) => TryBindContent(plan, out reason);

        // ─── 生命周期 ────────────────────────────────────────────

        /// <summary>进入狩猎阶段（由 GameManager 调用）</summary>
        public void OnEnter(List<HunterInstance> hunters, int currentYear = 1)
        {
            runtimeStarted = true;
            ActiveHunters  = hunters ?? new List<HunterInstance>();
            SelectedHunter = PlayableHuntSquadAvailability.ResolveSelectedHunter(ActiveHunters, null);
            CurrentYear = Math.Max(1, currentYear);
            LastNoiseResolution = default;
            _navigation.Reset();
            HuntEvents.ResetSession(CurrentYear);

            Map = MapGen.GenerateMap(tilePool, startingTileConfig);

            Debug.Log($"[HuntManager] 狩猎阶段开始，猎人数量: {ActiveHunters.Count}，地图格子: {Map.Count}");

            // 通知 UI 初始化
            foreach (var kv in Map)
                OnTileStateChanged?.Invoke(kv.Key, kv.Value.State);
        }

        public StatefulRandomState CaptureRandomState() => _rng.ExportState();

        public bool TryRestore(PlayableHuntRuntimeState state, out string reason)
        {
            if (state == null || state.Year <= 0 || state.Hunters == null || state.Hunters.Count == 0 || state.Map == null || state.Map.Count == 0)
            {
                reason = "活动狩猎恢复载荷不完整。";
                return false;
            }
            if (!state.Map.ContainsKey(state.SquadPosition))
            {
                reason = "活动狩猎的小队位置不在地图中。";
                return false;
            }

            runtimeStarted = true;
            ActiveHunters = state.Hunters;
            SelectedHunter = ActiveHunters.Find(hunter => hunter != null && hunter.InstanceId == state.SelectedHunterId && hunter.IsAlive);
            SelectedHunter = PlayableHuntSquadAvailability.ResolveSelectedHunter(ActiveHunters, SelectedHunter);
            CurrentYear = state.Year;
            LastNoiseResolution = default;
            Map = state.Map;
            _navigation.Reset();
            _navigation.MoveTo(ToCore(state.SquadPosition));
            _rng.RestoreState(state.RandomState);
            HuntEvents.ResetSession(CurrentYear);
            reason = string.Empty;
            return true;
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
            if (RequestTileInteraction == null)
            {
                Debug.LogError("[HuntManager] Hunt ActionSession 未安装，拒绝绕过 ActionQueue 的地图写入。");
                return;
            }
            RequestTileInteraction(coord).Forget();
        }

        public bool TryCommitTileInteraction(Vector2Int coordinate, HuntTileInteractionKind intendedKind, out HuntTileInteractionCommit commit)
        {
            commit = default;
            if (PlayableHuntInputGuard.IsBlocked || EnsureSelectedHunterAvailable() == null || !Map.TryGetValue(coordinate, out HexTileInstance tile)) return false;
            if (intendedKind == HuntTileInteractionKind.Reveal && tile.State == TileState.Interactable)
            {
                if (!MapGen.RevealTileDeferred(Map, coordinate, revealed => Resources.SpawnResourcePoints(revealed))) return false;
                if (tile.State != TileState.Revealed) return false;
                OnTileStateChanged?.Invoke(coordinate, TileState.Revealed);
                commit = new HuntTileInteractionCommit(HuntTileInteractionKind.Reveal, coordinate, tile, null);
                return true;
            }
            if (intendedKind != HuntTileInteractionKind.Move || tile.State != TileState.Revealed || !IsAdjacentToSquad(coordinate)) return false;

            _navigation.MoveTo(ToCore(coordinate));
            OnSquadMoved?.Invoke(coordinate);
            Debug.Log($"[HuntManager] 小队移动到 {coordinate}");
            commit = new HuntTileInteractionCommit(HuntTileInteractionKind.Move, coordinate, tile, null);
            return true;
        }

        internal EventData SelectTileInteractionEvent(HuntTileInteractionCommit commit, PlayableHuntNoiseResolution noiseResolution = default)
        {
            if (!commit.IsCommitted) return null;
            if (commit.Kind == HuntTileInteractionKind.Reveal)
            {
                EventData configuredEvent = HuntEvents.SelectTileRevealEvent(commit.Tile);
                return configuredEvent != null ? configuredEvent : noiseResolution.SelectedEvent;
            }
            return commit.Kind == HuntTileInteractionKind.Move ? HuntEvents.SelectSquadMoveEvent(commit.Tile) : null;
        }

        internal void CommitNoiseResolution(PlayableHuntNoiseResolution resolution)
        {
            if (resolution.IsResolved)
                LastNoiseResolution = resolution;
        }

        internal void FinalizeTileInteraction(HuntTileInteractionCommit commit)
        {
            if (!commit.IsCommitted || commit.Kind != HuntTileInteractionKind.Reveal) return;
            List<Vector2Int> newlyInteractable = MapGen.UnlockNeighbors(Map, commit.Coordinate);
            foreach (Vector2Int neighbor in newlyInteractable)
                OnTileStateChanged?.Invoke(neighbor, TileState.Interactable);
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
            if (EnsureSelectedHunterAvailable() == null) return;
            if (!Map.TryGetValue(tileCoord, out var tile)) return;
            if (pointIndex < 0 || pointIndex >= tile.ResourcePoints.Count) return;

            var point = tile.ResourcePoints[pointIndex];
            if (!IsHarvestablePoint(point)) return;

            OnResourcePointPresentationRequested?.Invoke(new HuntResourcePointPresentationRequest(tileCoord, pointIndex, point.ResourceName, point.DrawCount, point.MaterialPool?.Count ?? 0));
            OnResourcePointClicked?.Invoke(point, SelectedHunter);
        }

        public bool IsHarvestablePoint(ResourcePointInstance point)
        {
            if (!HasLivingHunter) return false;
            if (point == null || point.IsExhausted || !point.HasMaterialPool && point.Resource == null) return false;
            if (!Map.TryGetValue(SquadPosition, out HexTileInstance squadTile)) return false;
            return squadTile.State == TileState.Revealed && squadTile.ResourcePoints.Contains(point);
        }

        /// <summary>执行采集（由 UI 确认后调用）</summary>
        public List<ItemInstance> ExecuteHarvest(ResourcePointInstance point)
        {
            if (EnsureSelectedHunterAvailable() == null) return new List<ItemInstance>();
            var obtained = Resources.Harvest(point, SelectedHunter);
            OnResourcePointHarvested?.Invoke(point);
            return obtained;
        }

        public PlayableHarvestTransaction PrepareHarvest(ResourcePointInstance point)
        {
            return EnsureSelectedHunterAvailable() != null ? Resources.PrepareHarvest(point, SelectedHunter) : null;
        }

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
            if (!transaction.HunterIsAlive)
            {
                transaction.Abandon();
                return UniTask.FromResult(PlayableHarvestStepResult.Failed("执行采集的猎人已失去行动能力，资源点预约已释放"));
            }
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
            HuntRecord record = CreateHuntRecord(bossDefeated, settlement.CurrentYear);
            OnExit(settlement);
            // 旧 Boss/战败路径已经在 OnExit 完成资源转移，本阶段只交给 Settlement 提交历史/年份。
            record.ReturnSchemaVersion = 0;
            record.ParticipantHunterIds.Clear();
            OnHuntCompleted?.Invoke(record);
        }

        public HuntRecord CreateHuntRecord(bool bossDefeated, int currentYear)
        {
            var resourceList = new List<string>();
            int huntersDeployed = 0;
            int huntersLost = 0;
            var participantIds = new List<int>();
            foreach (var h in ActiveHunters)
            {
                if (h == null)
                    continue;
                huntersDeployed++;
                if (!participantIds.Contains(h.InstanceId))
                    participantIds.Add(h.InstanceId);
                if (h.IsDead)
                    huntersLost++;
                foreach (var item in h.Collectibles)
                    for (int count = 0; item?.Data != null && count < item.Count; count++)
                        resourceList.Add(item.Data.ContentId);
            }

            return new HuntRecord
            {
                RecordId         = Guid.NewGuid().ToString("N"),
                ReturnSchemaVersion = HuntRecord.CurrentReturnSchemaVersion,
                Year             = currentYear,
                HuntersDeployed  = huntersDeployed,
                HuntersLost      = huntersLost,
                BossDefeated     = bossDefeated,
                ParticipantHunterIds = participantIds,
                CollectedResources = resourceList
            };
        }

        // ─── 工具 ─────────────────────────────────────────────────

        public bool IsAdjacentToSquad(Vector2Int coord)
        {
            return _navigation.IsAdjacent(ToCore(coord));
        }

        public void SelectHunter(int hunterId)
        {
            HunterInstance selected = ActiveHunters.Find(hunter => hunter != null && hunter.InstanceId == hunterId && hunter.IsAlive);
            if (selected != null)
                SelectedHunter = selected;
        }

        internal HunterInstance EnsureSelectedHunterAvailable()
        {
            SelectedHunter = PlayableHuntSquadAvailability.ResolveSelectedHunter(ActiveHunters, SelectedHunter);
            return SelectedHunter;
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
