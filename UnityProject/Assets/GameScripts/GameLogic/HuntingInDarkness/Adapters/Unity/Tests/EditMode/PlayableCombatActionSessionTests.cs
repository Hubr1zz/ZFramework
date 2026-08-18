using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.Card.CharacterActionCard;
using GameplayBase.CombatSystem;
using GameplayBase.CombatSystem.Cards.FlipConditions;
using HuntingInDarkness.ActionFlow;
using HuntingInDarkness.ActionFlow.Combat;
using HuntingInDarkness.Combat;
using HuntingInDarkness.GameCore.Cards;
using NUnit.Framework;
using SO.Boss.ActionCard;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableCombatActionSessionTests
    {
        [Test]
        public async Task PlayCardAsync_CommitsCardAndPublishesFactsInOrder()
        {
            CharacterActionCardData template = CreateTemplate(oncePerTurn: true);
            var card = new CharacterActionCardInstance(template, 7);
            card.FlipConditions.Add(new AlwaysOnPlayCondition());
            var received = new List<string>();
            Action<CardPlayedEvent> playedHandler = evt => received.Add($"played:{evt.CardInstanceId}");
            Action<CardFlippedEvent> flippedHandler = evt => received.Add($"flipped:{evt.CardInstanceId}");
            Action<CombatActionCommittedEvent> committedHandler = evt => received.Add($"committed:{evt.CardInstanceId}");
            EventBus.Subscribe(playedHandler);
            EventBus.Subscribe(flippedHandler);
            EventBus.Subscribe(committedHandler);
            try
            {
                using TestRig rig = CreateRig(card);

                CombatCardCommandResult result = await rig.Session.PlayCardAsync(card, -1);

                Assert.That(result.Success, Is.True);
                Assert.That(card.IsAvailableThisTurn, Is.False);
                Assert.That(card.CurrentFace, Is.EqualTo(CardFace.FaceDown));
                Assert.That(received, Is.EqualTo(new[] { $"played:{card.InstanceId}", $"flipped:{card.InstanceId}", $"committed:{card.InstanceId}" }));
            }
            finally
            {
                EventBus.Unsubscribe(playedHandler);
                EventBus.Unsubscribe(flippedHandler);
                EventBus.Unsubscribe(committedHandler);
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        [Test]
        public async Task BeforeReactor_PreventionLeavesCardAndEventsUntouched()
        {
            CharacterActionCardData template = CreateTemplate(oncePerTurn: true);
            var card = new CharacterActionCardInstance(template, 7);
            int committedCount = 0;
            Action<CombatActionCommittedEvent> handler = _ => committedCount++;
            EventBus.Subscribe(handler);
            try
            {
                using TestRig rig = CreateRig(card);
                rig.Session.Reactors.RegisterGlobal(new PreventCardReactor());

                CombatCardCommandResult result = await rig.Session.PlayCardAsync(card, -1);

                Assert.That(result.Success, Is.False);
                Assert.That(result.Reason, Is.EqualTo("测试规则阻止行动"));
                Assert.That(card.IsAvailableThisTurn, Is.True);
                Assert.That(card.CurrentFace, Is.EqualTo(CardFace.FaceUp));
                Assert.That(committedCount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        [Test]
        public async Task PreparedEffectCancellation_StopsBeforeCostAndCardCommit()
        {
            CharacterActionCardData template = CreateTemplate(oncePerTurn: true, timePointCost: 2);
            var card = new CharacterActionCardInstance(template, 7);
            card.FaceUpEffects.Add(new CancelledPreparedEffect());
            using TestRig rig = CreateRig(card);

            CombatCardCommandResult result = await rig.Session.PlayCardAsync(card, -1);

            Assert.That(result.Success, Is.False);
            Assert.That(rig.Timeline.GetTimePoints(7), Is.Zero);
            Assert.That(card.IsAvailableThisTurn, Is.True);
        }

        [Test]
        public async Task ConcurrentRequests_OnlyCommitOncePerTurnCard()
        {
            CharacterActionCardData template = CreateTemplate(oncePerTurn: true);
            var card = new CharacterActionCardInstance(template, 7);
            int committedCount = 0;
            Action<CombatActionCommittedEvent> handler = _ => committedCount++;
            EventBus.Subscribe(handler);
            try
            {
                using TestRig rig = CreateRig(card);

                Task<CombatCardCommandResult> first = rig.Session.PlayCardAsync(card, -1).AsTask();
                Task<CombatCardCommandResult> second = rig.Session.PlayCardAsync(card, -1).AsTask();
                CombatCardCommandResult[] results = await Task.WhenAll(first, second);

                Assert.That(Array.FindAll(results, result => result.Success), Has.Length.EqualTo(1));
                Assert.That(committedCount, Is.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        [Test]
        public async Task DisposedSession_RejectsFurtherCommands()
        {
            CharacterActionCardData template = CreateTemplate(oncePerTurn: true);
            var card = new CharacterActionCardInstance(template, 7);
            TestRig rig = CreateRig(card);
            rig.Dispose();

            CombatCardCommandResult result = await rig.Session.PlayCardAsync(card, -1);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Reason, Is.EqualTo("战斗会话已经结束"));
            Assert.That(card.IsAvailableThisTurn, Is.True);
            UnityEngine.Object.DestroyImmediate(template);
        }

        [Test]
        public async Task EffectReactor_PreventionSkipsEffectButStillCommitsPaidCard()
        {
            CharacterActionCardData template = CreateTemplate(oncePerTurn: true, timePointCost: 1);
            var card = new CharacterActionCardInstance(template, 7);
            var effect = new CountingEffect();
            card.FaceUpEffects.Add(effect);
            using TestRig rig = CreateRig(card);
            rig.Session.Reactors.RegisterGlobal(new PreventEffectReactor());

            CombatCardCommandResult result = await rig.Session.PlayCardAsync(card, -1);

            Assert.That(result.Success, Is.True);
            Assert.That(effect.ExecutionCount, Is.Zero);
            Assert.That(card.IsAvailableThisTurn, Is.False);
            Assert.That(rig.Timeline.GetTimePoints(7), Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(template);
        }

        [Test]
        public async Task RestoreCardAsync_SpendsPreparedInspirationThenPublishesRestore()
        {
            CharacterActionCardData template = CreateTemplate(oncePerTurn: false);
            var card = new CharacterActionCardInstance(template, 7);
            card.RestoreConditions.Add(new CombatInspirationRestoreCondition(1, InspirationRequirement.Any));
            card.SetFace(CardFace.FaceDown);
            var received = new List<string>();
            Action<CombatInspirationChangedEvent> inspiration = _ => received.Add("inspiration");
            Action<CardRestoredEvent> restored = _ => received.Add("restored");
            EventBus.Subscribe(inspiration);
            EventBus.Subscribe(restored);
            try
            {
                using TestRig rig = CreateRig(card, initialInspiration: 1);

                CardRestoreCommandResult result = await rig.Session.RestoreCardAsync(card);

                Assert.That(result.Success, Is.True);
                Assert.That(card.CurrentFace, Is.EqualTo(CardFace.FaceUp));
                Assert.That(rig.Resources.GetCombatInspiration(7), Is.Zero);
                Assert.That(received, Is.EqualTo(new[] { "inspiration", "restored" }));
            }
            finally
            {
                EventBus.Unsubscribe(inspiration);
                EventBus.Unsubscribe(restored);
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        [Test]
        public async Task RestoreReactor_PreventionLeavesCardAndCostUntouched()
        {
            CharacterActionCardData template = CreateTemplate(oncePerTurn: false);
            var card = new CharacterActionCardInstance(template, 7);
            card.RestoreConditions.Add(new CombatInspirationRestoreCondition(1, InspirationRequirement.Any));
            card.SetFace(CardFace.FaceDown);
            using TestRig rig = CreateRig(card, initialInspiration: 1);
            rig.Session.Reactors.RegisterGlobal(new PreventRestoreReactor());

            CardRestoreCommandResult result = await rig.Session.RestoreCardAsync(card);

            Assert.That(result.Success, Is.False);
            Assert.That(card.CurrentFace, Is.EqualTo(CardFace.FaceDown));
            Assert.That(rig.Resources.GetCombatInspiration(7), Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(template);
        }

        [Test]
        public async Task ConcurrentRestoreRequests_OnlyOneCanCommit()
        {
            CharacterActionCardData template = CreateTemplate(oncePerTurn: false);
            var card = new CharacterActionCardInstance(template, 7);
            card.RestoreConditions.Add(new CombatInspirationRestoreCondition(1, InspirationRequirement.Any));
            card.SetFace(CardFace.FaceDown);
            using TestRig rig = CreateRig(card, initialInspiration: 1);

            Task<CardRestoreCommandResult> first = rig.Session.RestoreCardAsync(card).AsTask();
            Task<CardRestoreCommandResult> second = rig.Session.RestoreCardAsync(card).AsTask();
            CardRestoreCommandResult[] results = await Task.WhenAll(first, second);

            Assert.That(Array.FindAll(results, result => result.Success), Has.Length.EqualTo(1));
            Assert.That(card.CurrentFace, Is.EqualTo(CardFace.FaceUp));
            Assert.That(rig.Resources.GetCombatInspiration(7), Is.Zero);
            UnityEngine.Object.DestroyImmediate(template);
        }

        [Test]
        public async Task BurstCardAsync_FlipsCardBeforeApplyingTimePointReward()
        {
            CharacterActionCardData template = CreateTemplate(oncePerTurn: false);
            template.burstReward = new BurstRewardData { enabled = true, timePointReward = 1 };
            var card = new CharacterActionCardInstance(template, 7);
            using TestRig rig = CreateRig(card);
            rig.Timeline.AccumulateTimePoints(7, 2);
            var received = new List<string>();
            Action<CardFlippedEvent> flipped = _ => received.Add("flipped");
            Action<CardDiscardedEvent> discarded = _ => received.Add("discarded");
            Action<TimePointChangedEvent> time = _ => received.Add("time");
            EventBus.Subscribe(flipped);
            EventBus.Subscribe(discarded);
            EventBus.Subscribe(time);
            try
            {
                DiscardResult result = await rig.Session.BurstCardAsync(card);

                Assert.That(result.Success, Is.True);
                Assert.That(card.CurrentFace, Is.EqualTo(CardFace.FaceDown));
                Assert.That(rig.Timeline.GetTimePoints(7), Is.EqualTo(1));
                Assert.That(received, Is.EqualTo(new[] { "flipped", "discarded", "time" }));
            }
            finally
            {
                EventBus.Unsubscribe(flipped);
                EventBus.Unsubscribe(discarded);
                EventBus.Unsubscribe(time);
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        [Test]
        public async Task BurstReactor_PreventionLeavesCardAndTimelineUntouched()
        {
            CharacterActionCardData template = CreateTemplate(oncePerTurn: false);
            template.burstReward = new BurstRewardData { enabled = true, timePointReward = 1 };
            var card = new CharacterActionCardInstance(template, 7);
            using TestRig rig = CreateRig(card);
            rig.Timeline.AccumulateTimePoints(7, 2);
            rig.Session.Reactors.RegisterGlobal(new PreventBurstReactor());

            DiscardResult result = await rig.Session.BurstCardAsync(card);

            Assert.That(result.Success, Is.False);
            Assert.That(card.CurrentFace, Is.EqualTo(CardFace.FaceUp));
            Assert.That(rig.Timeline.GetTimePoints(7), Is.EqualTo(2));
            UnityEngine.Object.DestroyImmediate(template);
        }

        [Test]
        public async Task BurstEffectFailure_StillCommitsStartedBurstOnce()
        {
            CharacterActionCardData template = CreateTemplate(oncePerTurn: false);
            template.burstReward = new BurstRewardData { enabled = true, timePointReward = 1, bonusEffects = new List<CharacterActionCardEffectData> { new FailingBurstEffectData() } };
            var card = new CharacterActionCardInstance(template, 7);
            using TestRig rig = CreateRig(card);
            rig.Timeline.AccumulateTimePoints(7, 2);

            DiscardResult result = await rig.Session.BurstCardAsync(card);

            Assert.That(result.Success, Is.True);
            Assert.That(card.CurrentFace, Is.EqualTo(CardFace.FaceDown));
            Assert.That(rig.Timeline.GetTimePoints(7), Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(template);
        }

        [Test]
        public async Task BurstEffectReactor_PreventionSkipsEffectAndCommitsBurst()
        {
            CharacterActionCardData template = CreateTemplate(oncePerTurn: false);
            template.burstReward = new BurstRewardData { enabled = true, timePointReward = 1, bonusEffects = new List<CharacterActionCardEffectData> { new FailingBurstEffectData() } };
            var card = new CharacterActionCardInstance(template, 7);
            using TestRig rig = CreateRig(card);
            rig.Timeline.AccumulateTimePoints(7, 2);
            rig.Session.Reactors.RegisterGlobal(new PreventFailingBurstEffectReactor());

            DiscardResult result = await rig.Session.BurstCardAsync(card);

            Assert.That(result.Success, Is.True);
            Assert.That(card.CurrentFace, Is.EqualTo(CardFace.FaceDown));
            Assert.That(rig.Timeline.GetTimePoints(7), Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(template);
        }

        [Test]
        public async Task BeginPlayerTurnAsync_AppliesCardTransitionsInStableIdOrder()
        {
            CharacterActionCardData firstTemplate = CreateTemplate(oncePerTurn: false);
            CharacterActionCardData secondTemplate = CreateTemplate(oncePerTurn: false);
            var first = new CharacterActionCardInstance(firstTemplate, 7);
            var second = new CharacterActionCardInstance(secondTemplate, 7);
            first.RestoreConditions.Add(new RestoreOnTurnEndCondition());
            first.SetFace(CardFace.FaceDown);
            second.FlipConditions.Add(new TrackingTurnEndCondition());
            var received = new List<string>();
            Action<CardRestoredEvent> restored = evt => received.Add($"restore:{evt.CardInstanceId}");
            Action<CardFlippedEvent> flipped = evt => received.Add($"flip:{evt.CardInstanceId}");
            EventBus.Subscribe(restored);
            EventBus.Subscribe(flipped);
            try
            {
                using TestRig rig = CreateRig(first);
                rig.FlipEvaluator.RegisterCard(second);

                bool success = await rig.Session.BeginPlayerTurnAsync(new[] { second, first });

                Assert.That(success, Is.True);
                Assert.That(first.CurrentFace, Is.EqualTo(CardFace.FaceUp));
                Assert.That(second.CurrentFace, Is.EqualTo(CardFace.FaceDown));
                Assert.That(received, Is.EqualTo(new[] { $"restore:{first.InstanceId}", $"flip:{second.InstanceId}" }));
            }
            finally
            {
                EventBus.Unsubscribe(restored);
                EventBus.Unsubscribe(flipped);
                UnityEngine.Object.DestroyImmediate(firstTemplate);
                UnityEngine.Object.DestroyImmediate(secondTemplate);
            }
        }

        [Test]
        public async Task TurnStartCardReactor_PreventsOnlyMatchingAutomaticTransition()
        {
            CharacterActionCardData firstTemplate = CreateTemplate(oncePerTurn: false);
            CharacterActionCardData secondTemplate = CreateTemplate(oncePerTurn: false);
            var first = new CharacterActionCardInstance(firstTemplate, 7);
            var second = new CharacterActionCardInstance(secondTemplate, 7);
            first.RestoreConditions.Add(new RestoreOnTurnEndCondition());
            second.RestoreConditions.Add(new RestoreOnTurnEndCondition());
            first.SetFace(CardFace.FaceDown);
            second.SetFace(CardFace.FaceDown);
            using TestRig rig = CreateRig(first);
            rig.FlipEvaluator.RegisterCard(second);
            rig.Session.Reactors.RegisterGlobal(new PreventTurnStartCardReactor(first.InstanceId));

            bool success = await rig.Session.BeginPlayerTurnAsync(new[] { first, second });

            Assert.That(success, Is.True);
            Assert.That(first.CurrentFace, Is.EqualTo(CardFace.FaceDown));
            Assert.That(second.CurrentFace, Is.EqualTo(CardFace.FaceUp));
            UnityEngine.Object.DestroyImmediate(firstTemplate);
            UnityEngine.Object.DestroyImmediate(secondTemplate);
        }

        [Test]
        public void TurnEndEvent_IsCommittedFactAndDoesNotMutateCards()
        {
            CharacterActionCardData template = CreateTemplate(oncePerTurn: false);
            var card = new CharacterActionCardInstance(template, 7);
            card.RestoreConditions.Add(new RestoreOnTurnEndCondition());
            card.SetFace(CardFace.FaceDown);
            using TestRig rig = CreateRig(card);

            EventBus.Publish(new TurnEndEvent { EndingPhase = TurnPhase.PlayerTurn, TurnNumber = 1 });
            EventBus.Publish(new TurnEndEvent { EndingPhase = TurnPhase.BossTurn, TurnNumber = 1 });

            Assert.That(card.CurrentFace, Is.EqualTo(CardFace.FaceDown));
            UnityEngine.Object.DestroyImmediate(template);
        }

        [Test]
        public async Task BeginPlayerTurnAsync_ConsumesTurnConditionOnlyOnce()
        {
            CharacterActionCardData template = CreateTemplate(oncePerTurn: false);
            var card = new CharacterActionCardInstance(template, 7);
            var condition = new TrackingTurnEndCondition();
            card.RestoreConditions.Add(condition);
            card.SetFace(CardFace.FaceDown);
            using TestRig rig = CreateRig(card);

            Assert.That(await rig.Session.BeginPlayerTurnAsync(new[] { card }), Is.True);
            Assert.That(await rig.Session.BeginPlayerTurnAsync(new[] { card }), Is.True);

            Assert.That(condition.EvaluationCount, Is.EqualTo(1));
            Assert.That(condition.ConsumeCount, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(template);
        }

        [Test]
        public async Task BeginPlayerTurnAsync_ResetsTimelineBeforeEvaluatingCards()
        {
            CharacterActionCardData template = CreateTemplate(oncePerTurn: false);
            var card = new CharacterActionCardInstance(template, 7);
            bool resetCompleted = false;
            card.RestoreConditions.Add(new TrackingTurnEndCondition(() => resetCompleted));
            card.SetFace(CardFace.FaceDown);
            using TestRig rig = CreateRig(card, resetPlayerTurn: () => resetCompleted = true);
            var reactor = new PreventResetReactor();
            rig.Session.Reactors.RegisterGlobal(reactor);

            bool success = await rig.Session.BeginPlayerTurnAsync(new[] { card });

            Assert.That(success, Is.True);
            Assert.That(resetCompleted, Is.True);
            Assert.That(card.CurrentFace, Is.EqualTo(CardFace.FaceUp));
            Assert.That(reactor.InvocationCount, Is.Zero);
            UnityEngine.Object.DestroyImmediate(template);
        }

        private static CharacterActionCardData CreateTemplate(bool oncePerTurn, int timePointCost = 0)
        {
            CharacterActionCardData template = ScriptableObject.CreateInstance<CharacterActionCardData>();
            template.cardId = Guid.NewGuid().ToString("N");
            template.cardName = "测试行动";
            template.oncePerTurn = oncePerTurn;
            template.timePointCost = timePointCost;
            return template;
        }

        private static TestRig CreateRig(CharacterActionCardInstance card, int initialInspiration = 0, Action resetPlayerTurn = null)
        {
            var context = new TestGameContext(card.OwnerCharacterId);
            var timeline = new TimelineManager();
            timeline.RegisterCharacter(card.OwnerCharacterId, 2);
            var resources = new ActionCardResourcePool();
            resources.Register(card.OwnerCharacterId, initialInspiration);
            var flipEvaluator = new FlipConditionEvaluator(context);
            flipEvaluator.RegisterCard(card);
            var costService = new ActionCardCostService(() => timeline, () => null, flipEvaluator, resources);
            var session = new PlayableCombatActionSession(context, null, null, costService, flipEvaluator, _ => true, (characterId, reward) => timeline.AccumulateTimePoints(characterId, -reward), resetPlayerTurn ?? (() => { }));
            return new TestRig(session, flipEvaluator, timeline, resources);
        }

        private sealed class TestRig : IDisposable
        {
            private readonly FlipConditionEvaluator flipEvaluator;

            public TestRig(PlayableCombatActionSession session, FlipConditionEvaluator flipEvaluator, TimelineManager timeline, ActionCardResourcePool resources)
            {
                Session = session;
                this.flipEvaluator = flipEvaluator;
                Timeline = timeline;
                Resources = resources;
            }

            public PlayableCombatActionSession Session { get; }
            public TimelineManager Timeline { get; }
            public ActionCardResourcePool Resources { get; }
            public FlipConditionEvaluator FlipEvaluator => flipEvaluator;

            public void Dispose()
            {
                Session.Dispose();
                flipEvaluator.Dispose();
            }
        }

        private sealed class TestGameContext : IGameContext
        {
            private readonly CharacterRuntimeData character;
            private readonly TestBoss boss = new();

            public TestGameContext(int ownerId)
            {
                character = new CharacterRuntimeData { Id = ownerId, Name = "测试猎人" };
            }

            public TurnPhase CurrentPhase => TurnPhase.PlayerTurn;
            public int CurrentTurnNumber => 1;
            public IReadOnlyList<ICharacterState> PlayerCharacters => new ICharacterState[] { character };
            public IBossState Boss => boss;
            public IReadOnlyList<HitLocationRuntimeState> BossHitLocationStates => Array.Empty<HitLocationRuntimeState>();
            public IReadOnlyList<BossActionCardData> BossRevealedCards => Array.Empty<BossActionCardData>();

            public Character GetCharacter(int characterId) => null;
            public IReadOnlyList<ICharacterActionCardInstanceState> GetCardsOf(int characterId) => Array.Empty<ICharacterActionCardInstanceState>();
            public ICharacterActionCardInstanceState GetCard(int cardInstanceId) => null;
            public Vector3 GetEntityWorldPosition(int entityId) => Vector3.zero;
        }

        private sealed class TestBoss : IBossState
        {
            public int Id => 99;
            public string Name => "测试Boss";
            public int CurrentTimePoints => 0;
            public IReadOnlyList<int> PendingActionCardIds => Array.Empty<int>();
            public IReadOnlyList<int> RevealedNextCardIds => Array.Empty<int>();
        }

        private sealed class AlwaysOnPlayCondition : IFlipCondition
        {
            public FlipTriggerTiming Timing => FlipTriggerTiming.OnPlay;
            public string Description => "测试翻面";
            public bool Evaluate(FlipConditionContext context) => true;
            public void Consume(FlipConditionContext context) { }
        }

        private sealed class CancelledPreparedEffect : CharacterActionCardEffect, IPlayablePreparedActionEffect
        {
            public override string Description => "取消准备";
            public override TargetType TargetType => TargetType.Self;
            public bool IsPrepared => false;
            public override bool CanExecute(ActionCardContext context) => true;
            public override void Execute(ActionCardContext context) { }
            public Cysharp.Threading.Tasks.UniTask<bool> PrepareAsync(ActionCardContext context, CancellationToken cancellationToken = default) => Cysharp.Threading.Tasks.UniTask.FromResult(false);
            public Cysharp.Threading.Tasks.UniTask ExecutePreparedAsync(ActionCardContext context, CancellationToken cancellationToken = default) => Cysharp.Threading.Tasks.UniTask.CompletedTask;
            public void ResetPreparation() { }
        }

        private sealed class CountingEffect : CharacterActionCardEffect
        {
            public int ExecutionCount { get; private set; }
            public override string Description => "计数效果";
            public override TargetType TargetType => TargetType.Self;
            public override bool CanExecute(ActionCardContext context) => true;
            public override void Execute(ActionCardContext context) => ExecutionCount++;
        }

        private sealed class PreventCardReactor : GameActionReactor<PlayCharacterCardAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;

            protected override void React(PlayCharacterCardAction action, ReactionContext context, ReactionResponse response)
            {
                response.Prevent("测试规则阻止行动");
            }
        }

        private sealed class PreventEffectReactor : GameActionReactor<ExecuteCharacterCardEffectAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;

            protected override void React(ExecuteCharacterCardEffectAction action, ReactionContext context, ReactionResponse response)
            {
                response.Prevent("测试效果被覆盖");
            }
        }

        private sealed class PreventRestoreReactor : GameActionReactor<PrepareCardRestoreAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(PrepareCardRestoreAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试阻止恢复");
        }

        private sealed class PreventBurstReactor : GameActionReactor<BurstCharacterCardAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(BurstCharacterCardAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试阻止爆发");
        }

        [Serializable]
        private sealed class FailingBurstEffectData : CharacterActionCardEffectData
        {
            public override CharacterActionCardEffect CreateRuntime() => new FailingBurstEffect();
        }

        private sealed class FailingBurstEffect : CharacterActionCardEffect, IPlayableQueuedActionEffect
        {
            public override string Description => "失败的测试爆发效果";
            public override TargetType TargetType => TargetType.Self;
            public override bool CanExecute(ActionCardContext context) => true;
            public override void Execute(ActionCardContext context) { }
            public GameAction CreateAction(ActionCardContext context, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target) => new FailingBurstEffectAction(source, target);
        }

        private sealed class FailingBurstEffectAction : CommandAction, ISourceAction, ITargetAction
        {
            public FailingBurstEffectAction(IReactorEntity source, IReactorEntity target)
            {
                Source = source;
                Target = target;
            }

            public IReactorEntity Source { get; }
            public IReactorEntity Target { get; }
            protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken) => UniTask.FromResult(ActionOutcome.Failure("测试效果失败"));
        }

        private sealed class PreventFailingBurstEffectReactor : GameActionReactor<FailingBurstEffectAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(FailingBurstEffectAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试跳过爆发效果");
        }

        private sealed class TrackingTurnEndCondition : IFlipCondition
        {
            private readonly Func<bool> canEvaluate;

            public TrackingTurnEndCondition(Func<bool> canEvaluate = null) => this.canEvaluate = canEvaluate;

            public FlipTriggerTiming Timing => FlipTriggerTiming.OnTurnEnd;
            public string Description => "测试回合条件";
            public int EvaluationCount { get; private set; }
            public int ConsumeCount { get; private set; }

            public bool Evaluate(FlipConditionContext context)
            {
                EvaluationCount++;
                return canEvaluate?.Invoke() ?? true;
            }

            public void Consume(FlipConditionContext context) => ConsumeCount++;
        }

        private sealed class PreventTurnStartCardReactor : GameActionReactor<ResolveCardTurnStartAction>
        {
            private readonly int cardInstanceId;

            public PreventTurnStartCardReactor(int cardInstanceId) => this.cardInstanceId = cardInstanceId;

            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;

            protected override void React(ResolveCardTurnStartAction action, ReactionContext context, ReactionResponse response)
            {
                if (action.CardInstanceId == cardInstanceId)
                    response.Prevent("测试阻止自动恢复");
            }
        }

        private sealed class PreventResetReactor : GameActionReactor<ResetPlayerTurnStateAction>
        {
            public int InvocationCount { get; private set; }
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;

            protected override void React(ResetPlayerTurnStateAction action, ReactionContext context, ReactionResponse response)
            {
                InvocationCount++;
                response.Prevent("测试不应阻止核心轮次重置");
            }
        }
    }
}
