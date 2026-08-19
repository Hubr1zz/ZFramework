using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.ActionFlow.Campaign;
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

        [Test]
        public async Task Reveal_WaitsForEntireEventChainBeforeUnlockingNeighbors()
        {
            using var rig = new HuntRig();
            EventData chainedEvent = ScriptableObject.CreateInstance<EventData>();
            chainedEvent.name = "QueuedTileEventChild";
            chainedEvent.eventName = "后续事件";
            chainedEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.AddResource, targetName = "test-resource", value = 1 });
            rig.TileEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.AddResource, targetName = "test-resource", value = 1 });
            rig.TileEvent.chainedEvents.Add(chainedEvent);
            var input = new BlockingNarrativeInput();
            rig.Manager.EventInput = input;
            HexTileInstance target = rig.FirstInteractable;
            List<HexTileInstance> lockedNeighbors = HexMapGenerator.GetNeighbors(target.AxialCoord).Where(position => rig.Manager.Map.TryGetValue(position, out HexTileInstance tile) && tile.State == TileState.Locked).Select(position => rig.Manager.Map[position]).ToList();
            Assert.That(lockedNeighbors, Is.Not.Empty);

            UniTask<HuntTileCommandResult> interaction = rig.Session.InteractTileAsync(target.AxialCoord);
            Task started = await Task.WhenAny(input.Started.Task, Task.Delay(5000));
            Assert.That(started, Is.SameAs(input.Started.Task), "事件 Action 未在 5 秒内请求玩家输入");

            Assert.That(target.State, Is.EqualTo(TileState.Revealed));
            Assert.That(lockedNeighbors.All(tile => tile.State == TileState.Locked), Is.True);
            Assert.That(rig.Settlement.GetResource("test-resource"), Is.Zero);

            input.Continue.TrySetResult(true);
            HuntTileCommandResult result = await interaction;

            Assert.That(result.Succeeded, Is.True);
            Assert.That(input.PresentationCount, Is.EqualTo(2));
            Assert.That(lockedNeighbors.All(tile => tile.State == TileState.Interactable), Is.True);
            Assert.That(rig.Settlement.GetResource("test-resource"), Is.EqualTo(2));
            UnityEngine.Object.DestroyImmediate(chainedEvent);
        }

        [Test]
        public async Task CombatEvent_PublishesOneContextualRequestAfterRootAndStopsLaterChain()
        {
            using var rig = new HuntRig();
            EventData ignoredChild = ScriptableObject.CreateInstance<EventData>();
            ignoredChild.name = "AfterCombatChild";
            ignoredChild.immediateEffects.Add(new EventEffect { effectType = EventEffectType.AddResource, targetName = "should-not-apply", value = 1 });
            rig.TileEvent.eventType = GameEventType.Combat;
            rig.TileEvent.combatEncounterId = "event-boss";
            rig.TileEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.TriggerCombat, targetName = "ignored-effect-boss" });
            rig.TileEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.TriggerCombat, targetName = "ignored-second-boss" });
            rig.TileEvent.chainedEvents.Add(ignoredChild);
            HexTileInstance target = rig.FirstInteractable;
            int receivedCount = 0;
            int legacyRequestCount = 0;
            CampaignEncounterRequest received = default;
            bool neighborsUnlockedWhenPublished = false;
            Action<CampaignEncounterRequestedEvent> handler = evt =>
            {
                receivedCount++;
                received = evt.Request;
                neighborsUnlockedWhenPublished = HexMapGenerator.GetNeighbors(target.AxialCoord).Where(rig.Manager.Map.ContainsKey).All(position => rig.Manager.Map[position].State != TileState.Locked);
            };
            Action<PlayableEventEncounterRequestedEvent> legacyHandler = _ => legacyRequestCount++;
            EventBus.Subscribe(handler);
            EventBus.Subscribe(legacyHandler);
            try
            {
                HuntTileCommandResult result = await rig.Session.InteractTileAsync(target.AxialCoord);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(receivedCount, Is.EqualTo(1));
                Assert.That(legacyRequestCount, Is.Zero);
                Assert.That(received.SourceSessionId, Is.EqualTo(rig.Session.SessionId));
                Assert.That(received.EncounterId, Is.EqualTo("event-boss"));
                Assert.That(received.SourceKind, Is.EqualTo(CampaignEncounterSourceKind.HuntEvent));
                Assert.That(received.SourceCoordinate, Is.EqualTo(target.AxialCoord));
                Assert.That(received.SourceEventId, Is.EqualTo(rig.TileEvent.name));
                Assert.That(received.SourceContextId, Is.EqualTo("test-destination"));
                Assert.That(neighborsUnlockedWhenPublished, Is.True);
                Assert.That(rig.Settlement.GetResource("should-not-apply"), Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
                EventBus.Unsubscribe(legacyHandler);
                UnityEngine.Object.DestroyImmediate(ignoredChild);
            }
        }

        [Test]
        public async Task BossTile_UsesConfiguredEncounterIdAndCurrentSession()
        {
            using var rig = new HuntRig();
            HexTileInstance target = rig.FirstInteractable;
            target.HasBossEncounter = true;
            target.DomainState.HasBossEncounter = true;
            target.Config.bossEncounterId = "tile-boss";
            CampaignEncounterRequest received = default;
            Action<CampaignEncounterRequestedEvent> handler = evt => received = evt.Request;
            EventBus.Subscribe(handler);
            try
            {
                HuntTileCommandResult result = await rig.Session.InteractTileAsync(target.AxialCoord);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(received.EncounterId, Is.EqualTo("tile-boss"));
                Assert.That(received.SourceSessionId, Is.EqualTo(rig.Session.SessionId));
                Assert.That(received.SourceKind, Is.EqualTo(CampaignEncounterSourceKind.HuntBossTile));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
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
                startingTile.name = "QueuedStartingTile";
                startingTile.tileType = TileType.Starting;
                startingTile.tileName = "起点";
                plainTile = ScriptableObject.CreateInstance<HexTileData>();
                plainTile.name = "QueuedPlainTile";
                plainTile.tileType = TileType.Plains;
                plainTile.tileName = "测试地块";
                plainTile.tileRevealEvent = tileEvent;
                Settlement = new SettlementInstance();
                EventSystem = new EventSystem(Settlement, new FirstRandom());
                Manager = new HuntManager(EventSystem, seed: 17)
                {
                    StartingTileConfig = startingTile,
                    TilePool = { plainTile }
                };
                Manager.OnEnter(null);
                Session = new PlayableHuntActionSession(Manager, "default-boss", "test-destination");
            }

            public EventSystem EventSystem { get; }
            public SettlementInstance Settlement { get; }
            public EventData TileEvent => tileEvent;
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

        private sealed class BlockingNarrativeInput : IHuntEventInput
        {
            public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<bool> Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public int PresentationCount { get; private set; }

            public async UniTask ConfirmNarrativeAsync(EventData gameEvent, HunterInstance actor, CancellationToken cancellationToken)
            {
                PresentationCount++;
                if (PresentationCount != 1) return;
                Started.TrySetResult(true);
                await Continue.Task.AsUniTask().AttachExternalCancellation(cancellationToken);
            }

            public UniTask<HuntEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, CancellationToken cancellationToken) => UniTask.FromResult(new HuntEventChoiceSelection(-1, null));
            public UniTask<HuntEventCheckDecision> PresentCheckAsync(PlayableEventChoiceTransaction transaction, CancellationToken cancellationToken) => UniTask.FromResult(HuntEventCheckDecision.Accept);
            public UniTask ConfirmResultAsync(EventData gameEvent, EventResolutionResult result, CancellationToken cancellationToken) => UniTask.CompletedTask;
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
