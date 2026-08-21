using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
            int triggeredCount = 0;
            int committedCount = 0;
            Action<GameEventTriggeredEvent> triggeredHandler = evt =>
            {
                if (evt.EventId == rig.TileEvent.name)
                    triggeredCount++;
            };
            Action<HuntEventNodeCommittedEvent> committedHandler = _ => committedCount++;
            EventBus.Subscribe(triggeredHandler);
            EventBus.Subscribe(committedHandler);
            try
            {
                rig.Session.Reactors.RegisterGlobal(new PreventEventNodeReactor());

                HuntTileCommandResult result = await rig.Session.InteractTileAsync(target.AxialCoord);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(target.State, Is.EqualTo(TileState.Revealed));
                Assert.That(triggeredCount, Is.Zero);
                Assert.That(committedCount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(triggeredHandler);
                EventBus.Unsubscribe(committedHandler);
            }
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
        public async Task InteractTileAsync_AllHuntersLostRejectsFurtherExploration()
        {
            using var rig = new HuntRig();
            HexTileInstance target = rig.FirstInteractable;
            rig.Manager.ActiveHunters.Insert(0, null);
            rig.Hunter.IsAlive = false;

            HuntTileCommandResult result = await rig.Session.InteractTileAsync(target.AxialCoord);
            HuntRetreatCommandResult retreat = await rig.Session.PrepareRetreatAsync(1);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(target.State, Is.EqualTo(TileState.Interactable));
            Assert.That(rig.Manager.SelectedHunter, Is.Null);
            Assert.That(retreat.Succeeded, Is.True);
        }

        [Test]
        public async Task EventCommit_SelectedHunterLostPromotesLivingSquadMemberBeforeFact()
        {
            using var rig = new HuntRig(includeSurvivor: true, hunterDeathCommand: new DirectHunterDeathCommand());
            rig.TileEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.KillHunter, targetName = "test_event", description = "测试死亡" });
            HunterInstance selectedWhenPublished = null;
            Action<HuntEventNodeCommittedEvent> handler = _ => selectedWhenPublished = rig.Manager.SelectedHunter;
            EventBus.Subscribe(handler);
            try
            {
                HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.FirstInteractable.AxialCoord);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(rig.Hunter.IsAlive, Is.False);
                Assert.That(rig.Manager.SelectedHunter, Is.SameAs(rig.Survivor));
                Assert.That(selectedWhenPublished, Is.SameAs(rig.Survivor));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task Reveal_WaitsForEntireEventChainBeforeUnlockingNeighbors()
        {
            using var rig = new HuntRig();
            EventData chainedEvent = ScriptableObject.CreateInstance<EventData>();
            chainedEvent.name = "QueuedTileEventChild";
            chainedEvent.eventName = "后续事件";
            chainedEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.AddResource, targetName = rig.Resource.ContentId, value = 1 });
            rig.TileEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.AddResource, targetName = rig.Resource.ContentId, value = 1 });
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
            Assert.That(rig.Settlement.GetResource(rig.Resource), Is.Zero);

            input.Continue.TrySetResult(true);
            HuntTileCommandResult result = await interaction;

            Assert.That(result.Succeeded, Is.True);
            Assert.That(input.PresentationCount, Is.EqualTo(2));
            Assert.That(lockedNeighbors.All(tile => tile.State == TileState.Interactable), Is.True);
            Assert.That(rig.Settlement.GetResource(rig.Resource), Is.Zero);
            Assert.That(rig.Hunter.Collectibles.Sum(item => item.Count), Is.EqualTo(2));
            Assert.That(rig.Manager.CreateHuntRecord(false, 1).CollectedResources, Has.Count.EqualTo(2));

            rig.Manager.OnExit(rig.Settlement);

            Assert.That(rig.Hunter.Collectibles, Is.Empty);
            Assert.That(rig.Settlement.GetResource(rig.Resource), Is.EqualTo(2));
            UnityEngine.Object.DestroyImmediate(chainedEvent);
        }

        [Test]
        public async Task Reveal_WaitsForTilePresentationBeforeStartingEvent()
        {
            var presenter = new BlockingTilePresenter();
            using var rig = new HuntRig(presenter);
            var input = new BlockingNarrativeInput();
            rig.Manager.EventInput = input;
            HexTileInstance target = rig.FirstInteractable;

            UniTask<HuntTileCommandResult> interaction = rig.Session.InteractTileAsync(target.AxialCoord);
            Task presentationStarted = await Task.WhenAny(presenter.Started.Task, Task.Delay(5000));
            Assert.That(presentationStarted, Is.SameAs(presenter.Started.Task), "地块表现未在 5 秒内开始");

            Assert.That(target.State, Is.EqualTo(TileState.Revealed));
            Assert.That(input.Started.Task.IsCompleted, Is.False);

            presenter.Continue.TrySetResult(true);
            Task eventStarted = await Task.WhenAny(input.Started.Task, Task.Delay(5000));
            Assert.That(eventStarted, Is.SameAs(input.Started.Task), "地块表现结束后事件未在 5 秒内开始");
            input.Continue.TrySetResult(true);

            HuntTileCommandResult result = await interaction;
            Assert.That(result.Succeeded, Is.True);
            Assert.That(presenter.Request.Kind, Is.EqualTo(HuntTileInteractionKind.Reveal));
            Assert.That(presenter.Request.Coordinate, Is.EqualTo(target.AxialCoord));
        }

        [Test]
        public async Task Move_WaitsForSquadPresentationBeforeCompletingCommand()
        {
            var presenter = new BlockingTilePresenter(HuntTileInteractionKind.Move);
            using var rig = new HuntRig(presenter);
            rig.Manager.EventInput = null;
            HexTileInstance target = rig.FirstInteractable;
            await rig.Session.InteractTileAsync(target.AxialCoord);

            UniTask<HuntTileCommandResult> movement = rig.Session.InteractTileAsync(target.AxialCoord);
            Task presentationStarted = await Task.WhenAny(presenter.Started.Task, Task.Delay(5000));
            Assert.That(presentationStarted, Is.SameAs(presenter.Started.Task), "小队移动表现未在 5 秒内开始");

            Assert.That(rig.Manager.SquadPosition, Is.EqualTo(target.AxialCoord));
            Assert.That(movement.Status, Is.EqualTo(UniTaskStatus.Pending));

            presenter.Continue.TrySetResult(true);
            HuntTileCommandResult result = await movement;
            Assert.That(result.Succeeded, Is.True);
            Assert.That(presenter.Request.Kind, Is.EqualTo(HuntTileInteractionKind.Move));
        }

        [Test]
        public async Task Reveal_PresentationFailureDoesNotRollbackCommittedGameplay()
        {
            using var rig = new HuntRig(new FailingTilePresenter());
            rig.Manager.EventInput = null;
            HexTileInstance target = rig.FirstInteractable;
            LogAssert.Expect(LogType.Exception, new Regex("测试表现失败"));

            HuntTileCommandResult result = await rig.Session.InteractTileAsync(target.AxialCoord);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(target.State, Is.EqualTo(TileState.Revealed));
            Assert.That(HexMapGenerator.GetNeighbors(target.AxialCoord).Where(rig.Manager.Map.ContainsKey).All(position => rig.Manager.Map[position].State != TileState.Locked), Is.True);
        }

        [Test]
        public async Task Reveal_SelfReferencingEventCommitsOnceAndStillUnlocksNeighbors()
        {
            using var rig = new HuntRig();
            rig.TileEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.AddResource, targetName = rig.Resource.ContentId, value = 1 });
            rig.TileEvent.chainedEvents.Add(rig.TileEvent);
            HexTileInstance target = rig.FirstInteractable;
            int preventedCount = 0;
            Action<PlayableEventDuplicatePreventedEvent> handler = _ => preventedCount++;
            EventBus.Subscribe(handler);
            try
            {
                HuntTileCommandResult result = await rig.Session.InteractTileAsync(target.AxialCoord);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(rig.Settlement.GetResource(rig.Resource), Is.Zero);
                Assert.That(rig.Hunter.Collectibles.Sum(item => item.Count), Is.EqualTo(1));
                Assert.That(preventedCount, Is.EqualTo(1));
                Assert.That(HexMapGenerator.GetNeighbors(target.AxialCoord).Where(rig.Manager.Map.ContainsKey).All(position => rig.Manager.Map[position].State != TileState.Locked), Is.True);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task HuntEventResourceRemoval_IsAtomicAndNeverSpendsSettlementInventory()
        {
            using var rig = new HuntRig();
            rig.Settlement.AddResource(rig.Resource, 5);
            rig.Hunter.Collectibles.Add(new ItemInstance(rig.Resource));
            rig.TileEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.RemoveResource, targetName = rig.Resource.ContentId, value = 2 });

            HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.FirstInteractable.AxialCoord);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(rig.Hunter.Collectibles.Sum(item => item.Count), Is.EqualTo(1));
            Assert.That(rig.Settlement.GetResource(rig.Resource), Is.EqualTo(5));
        }

        [Test]
        public async Task HuntEventResourceReward_PublishesHuntScopedFactOnly()
        {
            using var rig = new HuntRig();
            rig.TileEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.AddResource, targetName = rig.Resource.ContentId, value = 2 });
            int huntFactCount = 0;
            int settlementFactCount = 0;
            PlayableEventResourceChangedEvent received = default;
            Action<PlayableEventResourceChangedEvent> huntHandler = evt =>
            {
                huntFactCount++;
                received = evt;
            };
            Action<ResourceChangedEvent> settlementHandler = _ => settlementFactCount++;
            EventBus.Subscribe(huntHandler);
            EventBus.Subscribe(settlementHandler);
            try
            {
                HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.FirstInteractable.AxialCoord);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(huntFactCount, Is.EqualTo(1));
                Assert.That(settlementFactCount, Is.Zero);
                Assert.That(received.Scope, Is.EqualTo(PlayableEventResourceScope.HuntCollectibles));
                Assert.That(received.ResourceId, Is.EqualTo(rig.Resource.ContentId));
                Assert.That(received.OldAmount, Is.Zero);
                Assert.That(received.NewAmount, Is.EqualTo(2));
            }
            finally
            {
                EventBus.Unsubscribe(huntHandler);
                EventBus.Unsubscribe(settlementHandler);
            }
        }

        [Test]
        public void HuntEventResourceReward_RejectsCountOverflowWithoutMutation()
        {
            using var rig = new HuntRig();
            rig.Hunter.Collectibles.Add(new ItemInstance(rig.Resource, int.MaxValue));
            var command = new HuntEventResourceCommand(rig.Manager);

            bool applied = command.TryApply(EventEffectType.AddResource, rig.Resource.ContentId, 1, rig.Hunter, out _, out string reason);

            Assert.That(applied, Is.False);
            Assert.That(reason, Does.Contain("数量范围"));
            Assert.That(rig.Hunter.Collectibles, Has.Count.EqualTo(1));
            Assert.That(rig.Hunter.Collectibles[0].Count, Is.EqualTo(int.MaxValue));
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
            private readonly HunterData hunterTemplate;
            private readonly ItemData resource;
            private readonly List<ItemData> previousItems;

            public HuntRig(IHuntTileInteractionPresenter tileInteractionPresenter = null, bool includeSurvivor = false, IHunterDeathCommand hunterDeathCommand = null)
            {
                previousItems = PlayableSettlementItemRegistry.Items.ToList();
                resource = ScriptableObject.CreateInstance<ItemData>();
                resource.name = "test_hunt_resource";
                resource.ConfigureContentId("test_hunt_resource");
                resource.itemName = "测试资源";
                resource.itemType = ItemType.Resource;
                var configuredItems = new List<ItemData>(previousItems) { resource };
                PlayableSettlementItemRegistry.Configure(configuredItems);
                hunterTemplate = ScriptableObject.CreateInstance<HunterData>();
                hunterTemplate.name = "TestHuntActor";
                hunterTemplate.hunterName = "测试猎人";
                Hunter = new HunterInstance(hunterTemplate);
                Survivor = includeSurvivor ? new HunterInstance(hunterTemplate) { Name = "后备猎人" } : null;
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
                if (includeSurvivor)
                {
                    Settlement.Hunters.Add(Hunter);
                    Settlement.Hunters.Add(Survivor);
                }
                EventSystem = new EventSystem(Settlement, new FirstRandom(), hunterDeathCommand: hunterDeathCommand);
                Manager = new HuntManager(EventSystem, seed: 17)
                {
                    StartingTileConfig = startingTile,
                    TilePool = { plainTile }
                };
                Manager.OnEnter(includeSurvivor ? new List<HunterInstance> { Hunter, Survivor } : new List<HunterInstance> { Hunter });
                Session = new PlayableHuntActionSession(Manager, "default-boss", "test-destination", tileInteractionPresenter: tileInteractionPresenter);
            }

            public EventSystem EventSystem { get; }
            public SettlementInstance Settlement { get; }
            public EventData TileEvent => tileEvent;
            public HunterInstance Hunter { get; }
            public HunterInstance Survivor { get; }
            public ItemData Resource => resource;
            public HuntManager Manager { get; }
            public PlayableHuntActionSession Session { get; }
            public HexTileInstance FirstInteractable => Manager.Map.Values.First(tile => tile.State == TileState.Interactable);

            public void Dispose()
            {
                Session.Dispose();
                PlayableSettlementItemRegistry.Configure(previousItems);
                UnityEngine.Object.DestroyImmediate(resource);
                UnityEngine.Object.DestroyImmediate(hunterTemplate);
                UnityEngine.Object.DestroyImmediate(plainTile);
                UnityEngine.Object.DestroyImmediate(startingTile);
                UnityEngine.Object.DestroyImmediate(tileEvent);
            }
        }

        private sealed class BlockingNarrativeInput : IPlayableEventInput
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

            public UniTask<PlayableEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, CancellationToken cancellationToken) => UniTask.FromResult(new PlayableEventChoiceSelection(-1, null));
            public UniTask<PlayableEventCheckDecision> PresentCheckAsync(PlayableEventChoiceTransaction transaction, CancellationToken cancellationToken) => UniTask.FromResult(PlayableEventCheckDecision.Accept);
            public UniTask ConfirmResultAsync(EventData gameEvent, EventResolutionResult result, CancellationToken cancellationToken) => UniTask.CompletedTask;
        }

        private sealed class BlockingTilePresenter : IHuntTileInteractionPresenter
        {
            private readonly HuntTileInteractionKind blockedKind;

            public BlockingTilePresenter(HuntTileInteractionKind blockedKind = HuntTileInteractionKind.Reveal)
            {
                this.blockedKind = blockedKind;
            }

            public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<bool> Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public HuntTileInteractionPresentationRequest Request { get; private set; }

            public async UniTask PresentAsync(HuntTileInteractionPresentationRequest request, CancellationToken cancellationToken)
            {
                if (request.Kind != blockedKind) return;
                Request = request;
                Started.TrySetResult(true);
                await Continue.Task.AsUniTask().AttachExternalCancellation(cancellationToken);
            }
        }

        private sealed class FailingTilePresenter : IHuntTileInteractionPresenter
        {
            public UniTask PresentAsync(HuntTileInteractionPresentationRequest request, CancellationToken cancellationToken) => UniTask.FromException(new InvalidOperationException("测试表现失败"));
        }

        private sealed class PreventCommitReactor : GameActionReactor<CommitHuntTileInteractionAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(CommitHuntTileInteractionAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试规则阻止地块提交");
        }

        private sealed class PreventEventNodeReactor : GameActionReactor<ResolvePlayableEventNodeAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(ResolvePlayableEventNodeAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试规则覆盖事件节点");
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }

        private sealed class DirectHunterDeathCommand : IHunterDeathCommand
        {
            public bool TryKill(HunterInstance hunter, string causeId, string causeText, out string reason)
            {
                reason = string.Empty;
                if (hunter == null || !hunter.IsAlive) return false;
                hunter.IsAlive = false;
                return true;
            }
        }
    }
}
