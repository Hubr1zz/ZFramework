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

        private static CharacterActionCardData CreateTemplate(bool oncePerTurn, int timePointCost = 0)
        {
            CharacterActionCardData template = ScriptableObject.CreateInstance<CharacterActionCardData>();
            template.cardId = Guid.NewGuid().ToString("N");
            template.cardName = "测试行动";
            template.oncePerTurn = oncePerTurn;
            template.timePointCost = timePointCost;
            return template;
        }

        private static TestRig CreateRig(CharacterActionCardInstance card, int initialInspiration = 0)
        {
            var context = new TestGameContext(card.OwnerCharacterId);
            var timeline = new TimelineManager();
            timeline.RegisterCharacter(card.OwnerCharacterId, 2);
            var resources = new ActionCardResourcePool();
            resources.Register(card.OwnerCharacterId, initialInspiration);
            var flipEvaluator = new FlipConditionEvaluator(context);
            flipEvaluator.RegisterCard(card);
            var costService = new ActionCardCostService(() => timeline, () => null, flipEvaluator, resources);
            var session = new PlayableCombatActionSession(context, null, null, costService, flipEvaluator, _ => true, (characterId, reward) => timeline.AccumulateTimePoints(characterId, -reward));
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
    }
}
