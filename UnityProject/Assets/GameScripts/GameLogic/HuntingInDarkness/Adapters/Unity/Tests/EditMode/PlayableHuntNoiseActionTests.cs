using System;
using System.Linq;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.Bootstrap;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableHuntNoiseActionTests
    {
        private const string SettingsPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Resources/HuntingInDarkness/PlayableBootstrapSettings.asset";

        [TearDown]
        public void TearDown() => PlayableHuntDestinationRuntime.Configure(null, null);

        [Test]
        public async System.Threading.Tasks.Task SafeCard_CommitsRevealAfterNoiseResult()
        {
            using var rig = new NoiseRig(10, false);

            HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.Target.AxialCoord);

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(result.NoiseResolution.IsResolved, Is.True);
            Assert.That(result.NoiseResolution.IsDanger, Is.False);
            Assert.That(rig.Target.State, Is.EqualTo(TileState.Revealed));
            Assert.That(rig.Presenter.RequestCount, Is.EqualTo(1));
        }

        [Test]
        public async System.Threading.Tasks.Task CancelledCard_LeavesTileUncommitted()
        {
            using var rig = new NoiseRig(1, true);

            HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.Target.AxialCoord);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Does.Contain("取消"));
            Assert.That(rig.Target.State, Is.EqualTo(TileState.Interactable));
            Assert.That(rig.Manager.LastNoiseResolution.IsResolved, Is.False);
        }

        [Test]
        public async System.Threading.Tasks.Task ReactorModifier_AdjustsFrozenPlanBeforePresentation()
        {
            using var rig = new NoiseRig(10, false);
            rig.Session.Reactors.RegisterGlobal(new QuietNoiseReactor());

            HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.Target.AxialCoord);

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(result.NoiseResolution.Plan.NoiseScore, Is.Zero);
            Assert.That(result.NoiseResolution.Plan.DangerCardCount, Is.Zero);
            Assert.That(rig.Presenter.LastRequest.Instruction, Does.Contain("没有危险牌"));
        }

        [Test]
        public async System.Threading.Tasks.Task MissingNoiseProfile_RejectsOrdinaryReveal()
        {
            using var rig = new NoiseRig(10, false);
            rig.Manager.NoiseProfile = null;

            HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.Target.AxialCoord);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Does.Contain("噪音风险牌堆"));
            Assert.That(rig.Target.State, Is.EqualTo(TileState.Interactable));
            Assert.That(rig.Presenter.RequestCount, Is.Zero);
        }

        [Test]
        public async System.Threading.Tasks.Task DangerCard_UsesOneStableEventIdAcrossCommittedFacts()
        {
            using var rig = new NoiseRig(1, false);
            string noiseEventId = string.Empty;
            string triggeredEventId = string.Empty;
            string committedEventId = string.Empty;
            Action<HuntNoiseResolvedEvent> noiseHandler = evt => noiseEventId = evt.EventId;
            Action<GameEventTriggeredEvent> triggeredHandler = evt =>
            {
                if (evt.EventId.StartsWith("hunt_", StringComparison.Ordinal)) triggeredEventId = evt.EventId;
            };
            Action<HuntEventNodeCommittedEvent> committedHandler = evt => committedEventId = evt.EventId;
            EventBus.Subscribe(noiseHandler);
            EventBus.Subscribe(triggeredHandler);
            EventBus.Subscribe(committedHandler);
            try
            {
                HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.Target.AxialCoord);

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(result.NoiseResolution.IsDanger, Is.True);
                Assert.That(result.NoiseResolution.EventId, Is.Not.Empty);
                Assert.That(noiseEventId, Is.EqualTo(result.NoiseResolution.EventId));
                Assert.That(triggeredEventId, Is.EqualTo(noiseEventId));
                Assert.That(committedEventId, Is.EqualTo(noiseEventId));
            }
            finally
            {
                EventBus.Unsubscribe(noiseHandler);
                EventBus.Unsubscribe(triggeredHandler);
                EventBus.Unsubscribe(committedHandler);
            }
        }

        [Test]
        public void MissingActionSession_RejectsDirectMapMutation()
        {
            using var rig = new NoiseRig(10, false);
            rig.Session.Dispose();
            LogAssert.Expect(LogType.Error, "[HuntManager] Hunt ActionSession 未安装，拒绝绕过 ActionQueue 的地图写入。");

            rig.Manager.OnTileClicked(rig.Target.AxialCoord);

            Assert.That(rig.Target.State, Is.EqualTo(TileState.Interactable));
            Assert.That(rig.Presenter.RequestCount, Is.Zero);
        }

        private sealed class NoiseRig : IDisposable
        {
            private readonly HunterData hunterTemplate;

            public NoiseRig(int cardValue, bool cancelled)
            {
                PlayableBootstrapSettings settings = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);
                PlayableHuntDestination destination = settings.HuntDestinations.GetAvailable(1)[0];
                PlayableHuntDestinationRuntime.Configure(settings.HuntDestinations, settings.HuntContent);
                Assert.That(PlayableHuntDestinationRuntime.TrySelect(destination, 1, out string reason), Is.True, reason);

                hunterTemplate = ScriptableObject.CreateInstance<HunterData>();
                hunterTemplate.hunterName = "噪音测试猎人";
                var hunter = new HunterInstance(hunterTemplate);
                var settlement = new SettlementInstance();
                settlement.Hunters.Add(hunter);
                var eventSystem = new EventSystem(settlement, new SystemRandomSource(27));
                Manager = new HuntManager(eventSystem, 27);
                Manager.OnEnter(new System.Collections.Generic.List<HunterInstance> { hunter }, 1);
                Target = Manager.Map.Values.First(tile => tile.State == TileState.Interactable && !tile.HasBossEncounter && tile.Config?.tileRevealEvent == null);
                Presenter = new FixedCardPresenter(cardValue, cancelled);
                Session = new PlayableHuntActionSession(Manager, destinationId: destination.DestinationId, randomInteractionPresenter: Presenter);
            }

            public HuntManager Manager { get; }
            public HexTileInstance Target { get; }
            public FixedCardPresenter Presenter { get; }
            public PlayableHuntActionSession Session { get; }

            public void Dispose()
            {
                Session.Dispose();
                UnityEngine.Object.DestroyImmediate(hunterTemplate);
            }
        }

        private sealed class FixedCardPresenter : ITabletopRandomInteractionPresenter
        {
            private readonly int cardValue;
            private readonly bool cancelled;

            public FixedCardPresenter(int cardValue, bool cancelled)
            {
                this.cardValue = cardValue;
                this.cancelled = cancelled;
            }

            public int RequestCount { get; private set; }
            public TabletopRandomInteractionRequest LastRequest { get; private set; }

            public UniTask<TabletopRandomInteractionResult> PresentAsync(TabletopRandomInteractionRequest request, CancellationToken cancellationToken)
            {
                RequestCount++;
                LastRequest = request;
                if (cancelled) return UniTask.FromResult(new TabletopRandomInteractionResult(request.InteractionId, Array.Empty<int>(), Array.Empty<string>(), true));
                return UniTask.FromResult(new TabletopRandomInteractionResult(request.InteractionId, new[] { cardValue }, new[] { $"{request.DeckId}:card-{cardValue}" }));
            }
        }

        private sealed class QuietNoiseReactor : GameActionReactor<PrepareHuntNoiseAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(PrepareHuntNoiseAction action, ReactionContext context, ReactionResponse response) => action.AddNoiseModifier(-1);
        }
    }
}
