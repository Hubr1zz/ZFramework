using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.Hunt;
using UnityEngine;

namespace HuntingInDarkness.ActionFlow.Hunt
{
    /// <summary>单次狩猎独占的 ActionQueue 环境，负责串行化地图命令和管理狩猎 Reactor 生命周期。</summary>
    public sealed class PlayableHuntActionSession : IDisposable
    {
        private readonly HuntManager manager;
        private readonly ActionEnvironment environment;
        private readonly string defaultEncounterId;
        private readonly string destinationId;
        private readonly ITabletopRandomInteractionPresenter randomInteractionPresenter;
        private readonly IHuntTileInteractionPresenter tileInteractionPresenter;
        private readonly IPlayableEventFatalInjuryCommand fatalInjuryCommand;
        private readonly Func<Vector2Int, UniTask> requestHandler;
        private readonly Func<ResourcePointInstance, UniTask<PlayableHarvestTransaction>> prepareHarvestHandler;
        private readonly Func<PlayableHarvestTransaction, UniTask<PlayableHarvestStepResult>> advanceHarvestHandler;
        private readonly HashSet<PlayableHarvestTransaction> activeHarvests = new();
        private readonly Dictionary<ResourcePointInstance, ReactorEntityHandle> resourcePointHandles = new();
        private readonly PlayableHuntEventOccurrenceStore occurrenceStore;
        private readonly IHuntConsumableContent consumableContent;
        private readonly Action checkpointCommitted;
        private int nextResourcePointHandleId;
        private bool returnCheckpointLocked;
        private bool gameplayLocked;
        private bool resourceSelectionInFlight;

        public PlayableHuntActionSession(HuntManager manager, string defaultEncounterId = "default", string destinationId = "", ITabletopRandomInteractionPresenter randomInteractionPresenter = null, IHuntTileInteractionPresenter tileInteractionPresenter = null, IActionEnvironmentInstallerRegistry installerRegistry = null, PlayableHuntEventOccurrenceStore restoredOccurrenceStore = null, Action checkpointCommitted = null, IHuntConsumableContent consumableContent = null, IPlayableEventFatalInjuryCommand fatalInjuryCommand = null, string expeditionId = "")
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.defaultEncounterId = string.IsNullOrWhiteSpace(defaultEncounterId) ? "default" : defaultEncounterId.Trim();
            this.destinationId = destinationId ?? string.Empty;
            this.randomInteractionPresenter = randomInteractionPresenter;
            this.tileInteractionPresenter = tileInteractionPresenter;
            this.fatalInjuryCommand = fatalInjuryCommand ?? new PlayableHuntFatalInjuryCommand(manager.EventSystem.Settlement, new SystemRandomSource(), new SystemRandomSource(), manager.EventSystem.HunterDeathCommand);
            occurrenceStore = restoredOccurrenceStore ?? new PlayableHuntEventOccurrenceStore(expeditionId);
            this.consumableContent = consumableContent ?? new PlayableHuntConsumableContentAdapter(manager);
            this.checkpointCommitted = checkpointCommitted;
            SessionId = Guid.NewGuid();
            environment = new ActionEnvironment(new ActionEnvironmentConfiguration
            {
                Name = "Hunt",
                Kind = ActionEnvironmentKind.Hunt,
                MaxActionsPerChain = 256,
                TraceCapacity = 48
            }, installerRegistry);
            requestHandler = RequestTileInteractionAsync;
            prepareHarvestHandler = PrepareHarvestAsync;
            advanceHarvestHandler = AdvanceHarvestAsync;
            manager.RequestTileInteraction = requestHandler;
            manager.RequestPrepareHarvest = prepareHarvestHandler;
            manager.RequestAdvanceHarvest = advanceHarvestHandler;
        }

        public bool IsActive => !environment.IsDisposed;
        public bool IsRunning => environment.IsRunning;
        public bool IsReturnCheckpointLocked => returnCheckpointLocked;
        public HuntRetreatPreview GetRetreatPreview() => HuntRetreatPreview.Create(manager);
        public bool HasPendingEventOccurrences => occurrenceStore.HasPendingOccurrences;
        public bool HasActiveHarvest
        {
            get
            {
                RemoveFinishedHarvests();
                return activeHarvests.Count > 0;
            }
        }
        public Guid SessionId { get; }
        public ReactorRegistry Reactors => environment.Reactors;
        public ReactionGateRegistry ReactionGates => environment.ReactionGates;
        public PlayableHuntEventOccurrenceStoreState CaptureOccurrenceState() => occurrenceStore.CaptureState();

        public async UniTask<HuntTileCommandResult> InteractTileAsync(Vector2Int coordinate)
        {
            if (!IsActive) return HuntTileCommandResult.Failed("狩猎会话已经结束");
            if (PlayableHuntInputGuard.IsBlocked) return HuntTileCommandResult.Failed("狩猎桌面交互当前已锁定");
            if (gameplayLocked) return HuntTileCommandResult.Failed("遭遇事件正在等待交接，当前狩猎操作已暂停");
            if (!await ResumePendingEventsAsync()) return HuntTileCommandResult.Failed("请先完成待恢复的狩猎事件");
            if (returnCheckpointLocked) return HuntTileCommandResult.Failed("回营检查点已锁定，请直接重试回营");
            if (HasActiveHarvest) return HuntTileCommandResult.Failed("请先完成或离开当前资源采集");
            HuntTileInteractionKind intendedKind = GetIntendedKind(coordinate);
            var outbox = new ActionEventOutbox();
            ReactorEntityHandle squad = environment.EntityHandles.GetOrCreate("hunt-squad", "active", "狩猎小队");
            ReactorEntityHandle tile = environment.EntityHandles.GetOrCreate("hunt-tile", $"{coordinate.x},{coordinate.y}", $"地块 {coordinate.x},{coordinate.y}");
            IReactorEntity ResolveEventEntity(EventData gameEvent) => environment.EntityHandles.GetOrCreate("hunt-event", gameEvent != null ? gameEvent.ContentId : "unknown", gameEvent != null ? gameEvent.eventName : "狩猎事件");
            var action = new InteractHuntTileAction(manager, coordinate, intendedKind, SessionId, defaultEncounterId, destinationId, outbox, squad, tile, ResolveEventEntity, randomInteractionPresenter, tileInteractionPresenter, occurrenceStore, LockEncounterHandoff, fatalInjuryCommand);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            if (action.Result.Commit.IsCommitted)
                await NotifyCheckpointWhenIdleAsync();
            if (!outcome.IsSuccess) return string.IsNullOrWhiteSpace(action.Result.Reason) ? HuntTileCommandResult.Failed(outcome.Reason) : action.Result;
            return action.Result;
        }

        public async UniTask<bool> SelectResourcePointAsync(Vector2Int coordinate, int pointIndex)
        {
            if (!IsActive || IsRunning || resourceSelectionInFlight || PlayableHuntInputGuard.IsBlocked) return false;
            resourceSelectionInFlight = true;
            try
            {
                if (gameplayLocked || !await ResumePendingEventsAsync() || returnCheckpointLocked || HasActiveHarvest) return false;
                if (!manager.Map.TryGetValue(coordinate, out HexTileInstance tile) || tile.ResourcePoints == null || pointIndex < 0 || pointIndex >= tile.ResourcePoints.Count) return false;
                ResourcePointInstance point = tile.ResourcePoints[pointIndex];
                if (!manager.IsHarvestablePoint(point)) return false;
                manager.OnResourcePointSelected(coordinate, pointIndex);
                return true;
            }
            finally
            {
                resourceSelectionInFlight = false;
            }
        }

        public async UniTask<HuntActorSelectionResult> SelectActorAsync(int hunterId, CancellationToken cancellationToken = default)
        {
            if (!IsActive) return HuntActorSelectionResult.Failed("狩猎会话已经结束");
            if (IsRunning || resourceSelectionInFlight || PlayableHuntInputGuard.IsBlocked) return HuntActorSelectionResult.Failed("狩猎桌面正在处理其他交互");
            if (gameplayLocked) return HuntActorSelectionResult.Failed("遭遇事件正在等待交接，当前狩猎操作已暂停");
            if (returnCheckpointLocked) return HuntActorSelectionResult.Failed("回营检查点已锁定，请直接重试回营");
            if (HasActiveHarvest) return HuntActorSelectionResult.Failed("请先完成或离开当前资源采集");

            HunterInstance requested = manager.ActiveHunters.Find(hunter => hunter != null && hunter.InstanceId == hunterId);
            var outbox = new ActionEventOutbox();
            ReactorEntityHandle actor = GetHunterHandle(hunterId, requested?.Name);
            ReactorEntityHandle squad = environment.EntityHandles.GetOrCreate("hunt-squad", "active", "狩猎小队");
            var action = new SelectHuntActorAction(manager, hunterId, SessionId, outbox, actor, squad);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox, cancellationToken: cancellationToken);
            if (!outcome.IsSuccess)
                return string.IsNullOrWhiteSpace(action.Result.Reason) ? HuntActorSelectionResult.Failed(outcome.Reason) : action.Result;
            if (action.Result.Changed)
                await NotifyCheckpointWhenIdleAsync();
            return action.Result;
        }

        public async UniTask<PlayableHarvestTransaction> PrepareHarvestAsync(ResourcePointInstance point)
        {
            if (!IsActive || gameplayLocked || !await ResumePendingEventsAsync() || returnCheckpointLocked || point == null) return null;
            var outbox = new ActionEventOutbox();
            HunterInstance selectedHunter = manager.EnsureSelectedHunterAvailable();
            if (selectedHunter == null) return null;
            ReactorEntityHandle hunter = GetHunterHandle(selectedHunter.InstanceId, selectedHunter.Name);
            ReactorEntityHandle resourcePoint = GetResourcePointHandle(point);
            var action = new BeginHarvestAction(manager, point, selectedHunter, outbox, hunter, resourcePoint);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            if (!outcome.IsSuccess)
            {
                action.Transaction?.Abandon();
                return null;
            }
            if (action.Transaction != null)
                activeHarvests.Add(action.Transaction);
            return action.Transaction;
        }

        public async UniTask<PlayableHarvestTransaction> PrepareHarvestAsync(Vector2Int coordinate, int pointIndex)
        {
            if (!IsActive || !manager.Map.TryGetValue(coordinate, out HexTileInstance tile) || tile.ResourcePoints == null || pointIndex < 0 || pointIndex >= tile.ResourcePoints.Count) return null;
            ResourcePointInstance point = tile.ResourcePoints[pointIndex];
            return manager.IsHarvestablePoint(point) ? await PrepareHarvestAsync(point) : null;
        }

        public async UniTask<PlayableHarvestStepResult> AdvanceHarvestAsync(PlayableHarvestTransaction transaction)
            => await AdvanceHarvestAsync(transaction, -1);

        public async UniTask<PlayableHarvestStepResult> AdvanceHarvestAsync(PlayableHarvestTransaction transaction, int cardIndex)
        {
            if (!IsActive) return PlayableHarvestStepResult.Failed("狩猎会话已经结束");
            if (gameplayLocked) return PlayableHarvestStepResult.Failed("遭遇事件正在等待交接，当前狩猎操作已暂停");
            if (!await ResumePendingEventsAsync()) return PlayableHarvestStepResult.Failed("请先完成待恢复的狩猎事件");
            if (returnCheckpointLocked) return PlayableHarvestStepResult.Failed("回营检查点已锁定，请直接重试回营");
            if (transaction == null || !activeHarvests.Contains(transaction)) return PlayableHarvestStepResult.Failed("采集事务不属于当前狩猎会话");
            var outbox = new ActionEventOutbox();
            ReactorEntityHandle hunter = GetHunterHandle(transaction.HunterId, transaction.HunterName);
            ReactorEntityHandle resourcePoint = GetResourcePointHandle(transaction.Point);
            var action = new AdvanceHarvestAction(manager, transaction, outbox, hunter, resourcePoint, cardIndex);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            if (transaction.IsCommitted || transaction.IsCancelled)
                activeHarvests.Remove(transaction);
            if (transaction.IsCommitted)
                await NotifyCheckpointWhenIdleAsync();
            if (outcome.IsSuccess) return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? PlayableHarvestStepResult.Failed(outcome.Reason) : action.Result;
        }

        public async UniTask<HuntConsumableCommandResult> UseConsumableAsync(int ownerHunterId, string itemId, HunterBodyPart bodyPart, CancellationToken cancellationToken = default)
        {
            if (!IsActive) return HuntConsumableCommandResult.Failed("狩猎会话已经结束。");
            if (ownerHunterId <= 0 || string.IsNullOrWhiteSpace(itemId)) return HuntConsumableCommandResult.Failed("狩猎消耗品请求缺少猎人或物品 ID。");
            if (PlayableHuntInputGuard.IsBlocked) return HuntConsumableCommandResult.Failed("狩猎桌面交互当前已锁定。");
            if (gameplayLocked) return HuntConsumableCommandResult.Failed("遭遇事件正在等待交接，当前狩猎操作已暂停。");
            if (!await ResumePendingEventsAsync(cancellationToken)) return HuntConsumableCommandResult.Failed("请先完成待恢复的狩猎事件。");
            if (returnCheckpointLocked) return HuntConsumableCommandResult.Failed("回营检查点已锁定，请直接重试回营。");
            if (HasActiveHarvest) return HuntConsumableCommandResult.Failed("请先完成或离开当前资源采集。");

            HunterInstance hunter = manager.ActiveHunters?.Find(candidate => candidate != null && candidate.InstanceId == ownerHunterId);
            var outbox = new ActionEventOutbox();
            ReactorEntityHandle actor = GetHunterHandle(ownerHunterId, hunter?.Name);
            ReactorEntityHandle item = environment.EntityHandles.GetOrCreate("hunt-consumable", itemId?.Trim() ?? string.Empty, "狩猎消耗品");
            var action = new UseHuntConsumableAction(manager, SessionId, ownerHunterId, itemId, bodyPart, consumableContent, outbox, actor, item);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox, cancellationToken: cancellationToken);
            if (outcome.IsSuccess)
            {
                await NotifyCheckpointWhenIdleAsync();
                return action.Result;
            }
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? HuntConsumableCommandResult.Failed(outcome.Reason) : action.Result;
        }

        public UniTask<HuntRetreatCommandResult> PrepareRetreatAsync(int currentYear, CancellationToken cancellationToken = default)
            => PrepareRetreatAsync(currentYear, HuntRetreatDecision.None, cancellationToken);

        public async UniTask<HuntRetreatCommandResult> PrepareRetreatAsync(int currentYear, HuntRetreatDecision decision, CancellationToken cancellationToken = default)
        {
            if (!IsActive)
                return HuntRetreatCommandResult.Failed("狩猎会话已经结束。");
            if (gameplayLocked)
                return HuntRetreatCommandResult.Failed("遭遇事件正在等待交接，当前狩猎操作已暂停。");
            if (!await ResumePendingEventsAsync(cancellationToken))
                return HuntRetreatCommandResult.Failed("请先完成待恢复的狩猎事件。");
            if (returnCheckpointLocked)
                return HuntRetreatCommandResult.Failed("回营检查点已锁定，请复用原记录重试阶段切换。");
            if (HasActiveHarvest)
                return HuntRetreatCommandResult.Failed("请先完成或离开当前资源采集。");

            var outbox = new ActionEventOutbox();
            ReactorEntityHandle squad = environment.EntityHandles.GetOrCreate("hunt-squad", "active", "狩猎小队");
            ReactorEntityHandle settlement = environment.EntityHandles.GetOrCreate("settlement", "return-target", "营地");
            var action = new PrepareHuntRetreatAction(manager, currentYear, decision, outbox, squad, settlement, occurrenceStore.Memories, occurrenceStore.ExpeditionId);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox, cancellationToken: cancellationToken);
            if (outcome.IsSuccess)
                return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? HuntRetreatCommandResult.Failed(outcome.Reason) : action.Result;
        }

        public void SetReturnCheckpointLock(bool locked) => returnCheckpointLocked = locked;
        public void ReleaseEncounterHandoffLock() => gameplayLocked = false;

        public void Dispose()
        {
            if (manager.RequestTileInteraction == requestHandler)
                manager.RequestTileInteraction = null;
            if (manager.RequestPrepareHarvest == prepareHarvestHandler)
                manager.RequestPrepareHarvest = null;
            if (manager.RequestAdvanceHarvest == advanceHarvestHandler)
                manager.RequestAdvanceHarvest = null;
            foreach (PlayableHarvestTransaction transaction in activeHarvests)
                transaction.Abandon();
            activeHarvests.Clear();
            resourcePointHandles.Clear();
            environment.Dispose();
        }

        private async UniTask<bool> ResumePendingEventsAsync(CancellationToken cancellationToken = default)
        {
            while (occurrenceStore.HasPendingOccurrences)
            {
                if (!occurrenceStore.TryGetNextPending(out PlayableHuntEventOccurrence pending)) return false;
                var outbox = new ActionEventOutbox();
                ReactorEntityHandle squad = environment.EntityHandles.GetOrCreate("hunt-squad", "active", "狩猎小队");
                ReactorEntityHandle tile = environment.EntityHandles.GetOrCreate("hunt-tile", $"{pending.Coordinate.x},{pending.Coordinate.y}", $"地块 {pending.Coordinate.x},{pending.Coordinate.y}");
                IReactorEntity ResolveEventEntity(EventData gameEvent) => environment.EntityHandles.GetOrCreate("hunt-event", gameEvent != null ? gameEvent.ContentId : "unknown", gameEvent != null ? gameEvent.eventName : "狩猎事件");
                var encounterAccumulator = new HuntEncounterAccumulator(SessionId, defaultEncounterId, destinationId);
                if (!manager.Map.TryGetValue(pending.Coordinate, out HexTileInstance pendingTile) || pendingTile == null)
                    return false;
                var syntheticCommit = new HuntTileInteractionCommit(HuntTileInteractionKind.Move, pending.Coordinate, pendingTile, null);
                var action = new ResolveHuntTileEventAction(manager, syntheticCommit, default, outbox, encounterAccumulator, squad, tile, ResolveEventEntity, randomInteractionPresenter, occurrenceStore, pending, true, LockEncounterHandoff, fatalInjuryCommand);
                ActionOutcome outcome = await environment.ExecuteAsync(action, outbox, cancellationToken: cancellationToken);
                bool pendingConsumed = !occurrenceStore.ContainsPendingSequence(pending.Sequence);
                if (action.HasCommittedCheckpoint || pendingConsumed)
                    await NotifyCheckpointWhenIdleAsync();
                if (!outcome.IsSuccess || action.EncounterRequested) return false;
                if (!pendingConsumed) return false;
            }
            return true;
        }

        private async UniTask NotifyCheckpointWhenIdleAsync()
        {
            if (checkpointCommitted == null) return;
            while (IsActive && environment.IsRunning)
                await UniTask.Yield(PlayerLoopTiming.Update);
            if (IsActive)
                checkpointCommitted.Invoke();
        }

        private void LockEncounterHandoff() => gameplayLocked = true;

        private void RemoveFinishedHarvests()
        {
            activeHarvests.RemoveWhere(transaction => transaction == null || transaction.IsCancelled || transaction.IsCommitted);
        }

        private async UniTask RequestTileInteractionAsync(Vector2Int coordinate) => await InteractTileAsync(coordinate);

        private HuntTileInteractionKind GetIntendedKind(Vector2Int coordinate)
        {
            if (!manager.Map.TryGetValue(coordinate, out HexTileInstance tile)) return HuntTileInteractionKind.None;
            if (tile.State == TileState.Interactable) return HuntTileInteractionKind.Reveal;
            if (tile.State == TileState.Revealed && manager.IsAdjacentToSquad(coordinate)) return HuntTileInteractionKind.Move;
            return HuntTileInteractionKind.None;
        }

        private ReactorEntityHandle GetHunterHandle(int hunterId, string hunterName)
        {
            return environment.EntityHandles.GetOrCreate("hunter", hunterId.ToString(), string.IsNullOrWhiteSpace(hunterName) ? "狩猎者" : hunterName);
        }

        private ReactorEntityHandle GetResourcePointHandle(ResourcePointInstance point)
        {
            if (point != null && resourcePointHandles.TryGetValue(point, out ReactorEntityHandle handle)) return handle;
            string resourceName = string.IsNullOrWhiteSpace(point?.ResourceName) ? "resource" : point.ResourceName;
            handle = environment.EntityHandles.GetOrCreate("resource-point", (++nextResourcePointHandleId).ToString(), resourceName);
            if (point != null)
                resourcePointHandles[point] = handle;
            return handle;
        }
    }
}
