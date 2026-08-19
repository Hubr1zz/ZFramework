using System;
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
            manager.RequestTileInteraction = requestHandler;
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

        public void Dispose()
        {
            if (manager.RequestTileInteraction == requestHandler)
                manager.RequestTileInteraction = null;
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
    }
}
