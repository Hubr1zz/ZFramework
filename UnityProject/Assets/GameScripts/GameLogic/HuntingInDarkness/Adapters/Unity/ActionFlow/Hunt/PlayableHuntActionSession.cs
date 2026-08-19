using System;
using System.Collections.Generic;
using CardGame.ActionQueue;
using Cysharp.Threading.Tasks;
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
        private readonly Func<Vector2Int, UniTask> requestHandler;
        private readonly Func<ResourcePointInstance, UniTask<PlayableHarvestTransaction>> prepareHarvestHandler;
        private readonly Func<PlayableHarvestTransaction, UniTask<PlayableHarvestStepResult>> advanceHarvestHandler;
        private readonly HashSet<PlayableHarvestTransaction> activeHarvests = new();
        private readonly Dictionary<ResourcePointInstance, ReactorEntityHandle> resourcePointHandles = new();
        private int nextResourcePointHandleId;

        public PlayableHuntActionSession(HuntManager manager)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            environment = new ActionEnvironment(new ActionEnvironmentConfiguration
            {
                Name = "Hunt",
                Kind = ActionEnvironmentKind.Hunt,
                MaxActionsPerChain = 256,
                TraceCapacity = 48
            });
            requestHandler = RequestTileInteractionAsync;
            prepareHarvestHandler = PrepareHarvestAsync;
            advanceHarvestHandler = AdvanceHarvestAsync;
            manager.RequestTileInteraction = requestHandler;
            manager.RequestPrepareHarvest = prepareHarvestHandler;
            manager.RequestAdvanceHarvest = advanceHarvestHandler;
        }

        public bool IsActive => !environment.IsDisposed;
        public ReactorRegistry Reactors => environment.Reactors;
        public ReactionGateRegistry ReactionGates => environment.ReactionGates;

        public async UniTask<HuntTileCommandResult> InteractTileAsync(Vector2Int coordinate)
        {
            if (!IsActive) return HuntTileCommandResult.Failed("狩猎会话已经结束");
            HuntTileInteractionKind intendedKind = GetIntendedKind(coordinate);
            var outbox = new ActionEventOutbox();
            ReactorEntityHandle squad = environment.EntityHandles.GetOrCreate("hunt-squad", "active", "狩猎小队");
            ReactorEntityHandle tile = environment.EntityHandles.GetOrCreate("hunt-tile", $"{coordinate.x},{coordinate.y}", $"地块 {coordinate.x},{coordinate.y}");
            var action = new InteractHuntTileAction(manager, coordinate, intendedKind, outbox, squad, tile);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            if (!outcome.IsSuccess) return string.IsNullOrWhiteSpace(action.Result.Reason) ? HuntTileCommandResult.Failed(outcome.Reason) : action.Result;
            HuntTileCommandResult result = action.Result;
            if (result.Commit.BossEncounter)
                manager.NotifyBossEncounter(result.Commit);
            return result;
        }

        public async UniTask<PlayableHarvestTransaction> PrepareHarvestAsync(ResourcePointInstance point)
        {
            if (!IsActive || point == null) return null;
            var outbox = new ActionEventOutbox();
            HunterInstance selectedHunter = manager.SelectedHunter;
            ReactorEntityHandle hunter = GetHunterHandle(selectedHunter?.InstanceId ?? -1, selectedHunter?.Name);
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
            if (transaction.IsCommitted)
                activeHarvests.Remove(transaction);
            if (outcome.IsSuccess) return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? PlayableHarvestStepResult.Failed(outcome.Reason) : action.Result;
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
