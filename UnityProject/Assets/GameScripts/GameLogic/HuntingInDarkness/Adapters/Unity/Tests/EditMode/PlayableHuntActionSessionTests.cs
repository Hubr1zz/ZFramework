using System;
using System.Linq;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Core;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableHuntActionSessionTests
    {
        [Test]
        public async Task InteractTileAsync_RevealCommitsStateThenPublishesFact()
        {
            using var rig = new HuntRig();
            HexTileInstance target = rig.FirstInteractable;
            HuntTileInteractionCommittedEvent received = default;
            int receivedCount = 0;
            Action<HuntTileInteractionCommittedEvent> handler = evt =>
            {
                received = evt;
                receivedCount++;
            };
            EventBus.Subscribe(handler);
            try
            {
                HuntTileCommandResult result = await rig.Session.InteractTileAsync(target.AxialCoord);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.Commit.Kind, Is.EqualTo(HuntTileInteractionKind.Reveal));
                Assert.That(target.State, Is.EqualTo(TileState.Revealed));
                Assert.That(receivedCount, Is.EqualTo(1));
                Assert.That(received.Coordinate, Is.EqualTo(target.AxialCoord));
                Assert.That(received.Kind, Is.EqualTo(HuntTileInteractionKind.Reveal));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task CommitReactor_PreventionLeavesTileAndFactsUntouched()
        {
            using var rig = new HuntRig();
            HexTileInstance target = rig.FirstInteractable;
            int receivedCount = 0;
            Action<HuntTileInteractionCommittedEvent> handler = _ => receivedCount++;
            EventBus.Subscribe(handler);
            try
            {
                rig.Session.Reactors.RegisterGlobal(new PreventCommitReactor());

                HuntTileCommandResult result = await rig.Session.InteractTileAsync(target.AxialCoord);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Reason, Is.EqualTo("测试规则阻止地块提交"));
                Assert.That(target.State, Is.EqualTo(TileState.Interactable));
                Assert.That(receivedCount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task EventReactor_PreventionKeepsRevealAndSkipsTileEvent()
        {
            using var rig = new HuntRig();
            HexTileInstance target = rig.FirstInteractable;
            int presentedCount = 0;
            rig.EventSystem.OnEventTriggered = (_, _) => presentedCount++;
            rig.Session.Reactors.RegisterGlobal(new PreventTileEventReactor());

            HuntTileCommandResult result = await rig.Session.InteractTileAsync(target.AxialCoord);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(target.State, Is.EqualTo(TileState.Revealed));
            Assert.That(presentedCount, Is.Zero);
        }

        [Test]
        public async Task InteractTileAsync_RevealThenMoveUsesTwoCommittedCommands()
        {
            using var rig = new HuntRig();
            HexTileInstance target = rig.FirstInteractable;

            HuntTileCommandResult reveal = await rig.Session.InteractTileAsync(target.AxialCoord);
            HuntTileCommandResult move = await rig.Session.InteractTileAsync(target.AxialCoord);

            Assert.That(reveal.Succeeded, Is.True);
            Assert.That(reveal.Commit.Kind, Is.EqualTo(HuntTileInteractionKind.Reveal));
            Assert.That(move.Succeeded, Is.True);
            Assert.That(move.Commit.Kind, Is.EqualTo(HuntTileInteractionKind.Move));
            Assert.That(rig.Manager.SquadPosition, Is.EqualTo(target.AxialCoord));
        }

        [Test]
        public async Task InteractTileAsync_LockedTileIsRejectedWithoutMutation()
        {
            using var rig = new HuntRig();
            HexTileInstance target = rig.Manager.Map.Values.First(tile => tile.State == TileState.Locked);

            HuntTileCommandResult result = await rig.Session.InteractTileAsync(target.AxialCoord);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(target.State, Is.EqualTo(TileState.Locked));
            Assert.That(rig.Manager.SquadPosition, Is.EqualTo(Vector2Int.zero));
        }

        private sealed class HuntRig : IDisposable
        {
            private readonly EventData tileEvent;
            private readonly HexTileData startingTile;
            private readonly HexTileData plainTile;

            public HuntRig()
            {
                tileEvent = ScriptableObject.CreateInstance<EventData>();
                tileEvent.name = "QueuedTileEvent";
                tileEvent.eventName = "队列地块事件";
                tileEvent.category = EventCategory.Hunt;
                tileEvent.drawWeight = 1;
                startingTile = ScriptableObject.CreateInstance<HexTileData>();
                startingTile.tileType = TileType.Starting;
                startingTile.tileName = "起点";
                plainTile = ScriptableObject.CreateInstance<HexTileData>();
                plainTile.tileType = TileType.Plains;
                plainTile.tileName = "测试地块";
                plainTile.tileRevealEvent = tileEvent;
                EventSystem = new EventSystem(new SettlementInstance(), new FirstRandom());
                Manager = new HuntManager(EventSystem, seed: 17)
                {
                    StartingTileConfig = startingTile,
                    TilePool = { plainTile }
                };
                Manager.OnEnter(null);
                Session = new PlayableHuntActionSession(Manager);
            }

            public EventSystem EventSystem { get; }
            public HuntManager Manager { get; }
            public PlayableHuntActionSession Session { get; }
            public HexTileInstance FirstInteractable => Manager.Map.Values.First(tile => tile.State == TileState.Interactable);

            public void Dispose()
            {
                Session.Dispose();
                UnityEngine.Object.DestroyImmediate(plainTile);
                UnityEngine.Object.DestroyImmediate(startingTile);
                UnityEngine.Object.DestroyImmediate(tileEvent);
            }
        }

        private sealed class PreventCommitReactor : GameActionReactor<CommitHuntTileInteractionAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(CommitHuntTileInteractionAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试规则阻止地块提交");
        }

        private sealed class PreventTileEventReactor : GameActionReactor<ResolveHuntTileEventAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(ResolveHuntTileEventAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试规则覆盖地块事件");
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
