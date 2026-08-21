using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.Data;
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
        private readonly Func<Vector2Int, UniTask> requestHandler;
        private readonly Func<ResourcePointInstance, UniTask<PlayableHarvestTransaction>> prepareHarvestHandler;
        private readonly Func<PlayableHarvestTransaction, UniTask<PlayableHarvestStepResult>> advanceHarvestHandler;
        private readonly HashSet<PlayableHarvestTransaction> activeHarvests = new();
        private readonly Dictionary<ResourcePointInstance, ReactorEntityHandle> resourcePointHandles = new();
        private int nextResourcePointHandleId;

        public PlayableHuntActionSession(HuntManager manager, string defaultEncounterId = "default", string destinationId = "", ITabletopRandomInteractionPresenter randomInteractionPresenter = null, IHuntTileInteractionPresenter tileInteractionPresenter = null, IActionEnvironmentInstallerRegistry installerRegistry = null)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.defaultEncounterId = string.IsNullOrWhiteSpace(defaultEncounterId) ? "default" : defaultEncounterId.Trim();
            this.destinationId = destinationId ?? string.Empty;
            this.randomInteractionPresenter = randomInteractionPresenter;
            this.tileInteractionPresenter = tileInteractionPresenter;
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

        public async UniTask<HuntTileCommandResult> InteractTileAsync(Vector2Int coordinate)
        {
            if (!IsActive) return HuntTileCommandResult.Failed("狩猎会话已经结束");
            if (HasActiveHarvest) return HuntTileCommandResult.Failed("请先完成或离开当前资源采集");
            HuntTileInteractionKind intendedKind = GetIntendedKind(coordinate);
            var outbox = new ActionEventOutbox();
            ReactorEntityHandle squad = environment.EntityHandles.GetOrCreate("hunt-squad", "active", "狩猎小队");
            ReactorEntityHandle tile = environment.EntityHandles.GetOrCreate("hunt-tile", $"{coordinate.x},{coordinate.y}", $"地块 {coordinate.x},{coordinate.y}");
            IReactorEntity ResolveEventEntity(EventData gameEvent) => environment.EntityHandles.GetOrCreate("hunt-event", gameEvent != null ? gameEvent.name : "unknown", gameEvent != null ? gameEvent.eventName : "狩猎事件");
            var action = new InteractHuntTileAction(manager, coordinate, intendedKind, SessionId, defaultEncounterId, destinationId, outbox, squad, tile, ResolveEventEntity, randomInteractionPresenter, tileInteractionPresenter);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            if (!outcome.IsSuccess) return string.IsNullOrWhiteSpace(action.Result.Reason) ? HuntTileCommandResult.Failed(outcome.Reason) : action.Result;
            return action.Result;
        }

        public async UniTask<PlayableHarvestTransaction> PrepareHarvestAsync(ResourcePointInstance point)
        {
            if (!IsActive || point == null) return null;
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

        public async UniTask<PlayableHarvestStepResult> AdvanceHarvestAsync(PlayableHarvestTransaction transaction)
        {
            if (!IsActive) return PlayableHarvestStepResult.Failed("狩猎会话已经结束");
            if (transaction == null || !activeHarvests.Contains(transaction)) return PlayableHarvestStepResult.Failed("采集事务不属于当前狩猎会话");
            var outbox = new ActionEventOutbox();
            ReactorEntityHandle hunter = GetHunterHandle(transaction.HunterId, transaction.HunterName);
            ReactorEntityHandle resourcePoint = GetResourcePointHandle(transaction.Point);
            var action = new AdvanceHarvestAction(manager, transaction, outbox, hunter, resourcePoint);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            if (transaction.IsCommitted || transaction.IsCancelled)
                activeHarvests.Remove(transaction);
            if (outcome.IsSuccess) return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? PlayableHarvestStepResult.Failed(outcome.Reason) : action.Result;
        }

        public async UniTask<HuntRetreatCommandResult> PrepareRetreatAsync(int currentYear, CancellationToken cancellationToken = default)
        {
            if (!IsActive)
                return HuntRetreatCommandResult.Failed("狩猎会话已经结束。");
            if (HasActiveHarvest)
                return HuntRetreatCommandResult.Failed("请先完成或离开当前资源采集。");

            var outbox = new ActionEventOutbox();
            ReactorEntityHandle squad = environment.EntityHandles.GetOrCreate("hunt-squad", "active", "狩猎小队");
            ReactorEntityHandle settlement = environment.EntityHandles.GetOrCreate("settlement", "return-target", "营地");
            var action = new PrepareHuntRetreatAction(manager, currentYear, outbox, squad, settlement);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox, cancellationToken: cancellationToken);
            if (outcome.IsSuccess)
                return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? HuntRetreatCommandResult.Failed(outcome.Reason) : action.Result;
        }

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
