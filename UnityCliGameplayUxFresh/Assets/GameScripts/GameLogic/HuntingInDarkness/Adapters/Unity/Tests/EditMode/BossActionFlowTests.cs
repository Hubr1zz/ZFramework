using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.CombatSystem;
using HuntingInDarkness.ActionFlow;
using HuntingInDarkness.ActionFlow.Combat;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunters;
using NUnit.Framework;
using SO.Boss.ActionCard;
using SO.Boss.HitLocation;
using SO.Character;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class BossActionFlowTests
    {
        [Test]
        public async Task BossTurn_ExecutesCardsInOrderAndPublishesEachCheckpoint()
        {
            BossActionCardData first = ScriptableObject.CreateInstance<BossActionCardData>();
            BossActionCardData second = ScriptableObject.CreateInstance<BossActionCardData>();
            var received = new List<int>();
            Action<BossActionExecutedEvent> executed = evt => received.Add(evt.ActionCardId);
            EventBus.Subscribe(executed);
            try
            {
                using var environment = CreateEnvironment();
                var outbox = new ActionEventOutbox();
                ReactorEntityHandle boss = environment.EntityHandles.GetOrCreate("boss", "99", "Boss");
                ReactorEntityHandle combat = environment.EntityHandles.GetOrCreate("combat", "active", "Combat");
                var requests = new[] { new BossActionRequest(11, first), new BossActionRequest(12, second) };
                var action = new ExecuteBossTurnAction(requests, new TestGameContext(), null, outbox, boss, combat, id => environment.EntityHandles.GetOrCreate("hunter", id.ToString(), id.ToString()));

                ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);

                Assert.That(outcome.IsSuccess, Is.True);
                Assert.That(action.ExecutedCardCount, Is.EqualTo(2));
                Assert.That(received, Is.EqualTo(new[] { 11, 12 }));
            }
            finally
            {
                EventBus.Unsubscribe(executed);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public async Task MultiHitAttack_CommitsEachWoundAndCompletesOnce()
        {
            CharacterCombatStats stats = CreateStats();
            var received = new List<string>();
            Action<CharacterWoundedEvent> wounded = _ => received.Add("wounded");
            Action<AttackCompletedEvent> completed = _ => received.Add("completed");
            EventBus.Subscribe(wounded);
            EventBus.Subscribe(completed);
            try
            {
                using var environment = CreateEnvironment();
                var hitProbe = new HitProbeReactor();
                environment.Reactors.RegisterGlobal(hitProbe);

                ActionOutcome outcome = await ExecuteAttack(environment, stats, attackCount: 2);

                Assert.That(outcome.IsSuccess, Is.True);
                Assert.That(stats.InjuryState.GetPart(HunterBodyPart.Torso).CurrentHealth, Is.EqualTo(2));
                Assert.That(hitProbe.Count, Is.EqualTo(2));
                Assert.That(received, Is.EqualTo(new[] { "wounded", "wounded", "completed" }));
            }
            finally
            {
                EventBus.Unsubscribe(wounded);
                EventBus.Unsubscribe(completed);
            }
        }

        [Test]
        public async Task DeathOnFirstAttempt_TruncatesRemainingAttacks()
        {
            CharacterCombatStats stats = CreateStats(new DeathDeck(new[] { DeathCardType.Death }));
            stats.ApplyDamage(HunterBodyPart.Torso, 4, new FirstRandom());
            int deathCount = 0;
            int completedCount = 0;
            Action<CharacterDiedEvent> died = _ => deathCount++;
            Action<AttackCompletedEvent> completed = _ => completedCount++;
            EventBus.Subscribe(died);
            EventBus.Subscribe(completed);
            try
            {
                using var environment = CreateEnvironment();
                var hitProbe = new HitProbeReactor();
                environment.Reactors.RegisterGlobal(hitProbe);

                ActionOutcome outcome = await ExecuteAttack(environment, stats, attackCount: 3, input: new AttackInput());

                Assert.That(outcome.IsSuccess, Is.True);
                Assert.That(stats.IsDead, Is.True);
                Assert.That(hitProbe.Count, Is.EqualTo(1));
                Assert.That(deathCount, Is.EqualTo(1));
                Assert.That(completedCount, Is.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe(died);
                EventBus.Unsubscribe(completed);
            }
        }

        [Test]
        public async Task WoundReactor_PreventsMutationButAttackStillCompletes()
        {
            CharacterCombatStats stats = CreateStats();
            int woundedCount = 0;
            int completedCount = 0;
            Action<CharacterWoundedEvent> wounded = _ => woundedCount++;
            Action<AttackCompletedEvent> completed = _ => completedCount++;
            EventBus.Subscribe(wounded);
            EventBus.Subscribe(completed);
            try
            {
                using var environment = CreateEnvironment();
                environment.Reactors.RegisterGlobal(new PreventWoundReactor());

                ActionOutcome outcome = await ExecuteAttack(environment, stats);

                Assert.That(outcome.IsSuccess, Is.True);
                Assert.That(stats.InjuryState.GetPart(HunterBodyPart.Torso).CurrentHealth, Is.EqualTo(4));
                Assert.That(woundedCount, Is.Zero);
                Assert.That(completedCount, Is.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe(wounded);
                EventBus.Unsubscribe(completed);
            }
        }

        private static CharacterCombatStats CreateStats(DeathDeck deathDeck = null)
        {
            var stats = new CharacterCombatStats { Evasion = 0 };
            stats.InitializeInjuryState(HunterInjuryProfile.CreateDefault(), deathDeck);
            return stats;
        }

        private static ActionEnvironment CreateEnvironment()
        {
            return new ActionEnvironment(new ActionEnvironmentConfiguration
            {
                Name = "BossFlowTest",
                Kind = ActionEnvironmentKind.Combat,
                MaxActionsPerChain = 64,
                TraceCapacity = 16,
                SkipPresentationWaits = true
            });
        }

        private static async UniTask<ActionOutcome> ExecuteAttack(ActionEnvironment environment, CharacterCombatStats stats, int attackCount = 1, IPlayerInputProvider input = null)
        {
            var context = new AttackContext
            {
                AttackerId = 99,
                DefenderId = 7,
                AttackerIsBoss = true,
                DefenderStats = stats
            };
            var outbox = new ActionEventOutbox();
            ReactorEntityHandle source = environment.EntityHandles.GetOrCreate("boss", "99", "Boss");
            ReactorEntityHandle target = environment.EntityHandles.GetOrCreate("hunter", "7", "Hunter");
            var action = new BossAttackFlowAction(context, input ?? new AttackInput(), 1, HunterBodyPart.Torso, 1, attackCount, new FirstRandom(), null, null, null, outbox, source, target);
            return await environment.ExecuteAsync(action, outbox);
        }

        private sealed class HitProbeReactor : GameActionReactor<BossHitCheckAction>
        {
            public int Count { get; private set; }
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(BossHitCheckAction action, ReactionContext context, ReactionResponse response) => Count++;
        }

        private sealed class PreventWoundReactor : GameActionReactor<ApplyHunterWoundAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(ApplyHunterWoundAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试护盾抵消伤害");
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }

        private sealed class AttackInput : IPlayerInputProvider, IDeathDeckInputProvider
        {
            public UniTask<int> RequestDrawDeathCard(string prompt, DeathDeckComposition composition, CancellationToken cancellationToken = default) => UniTask.FromResult(0);
            public UniTask<int> RequestRoll(string prompt, int maxExclusive, CancellationToken cancellationToken = default) => UniTask.FromResult(0);
            public UniTask ShowResult(string message, CancellationToken cancellationToken = default) => UniTask.CompletedTask;
            public UniTask<int> RequestSelectTarget(string prompt, List<int> validTargetIds, CancellationToken cancellationToken = default) => UniTask.FromResult(validTargetIds.Count > 0 ? validTargetIds[0] : -1);
            public UniTask<Vector2Int?> RequestSelectTile(string prompt, List<Vector2Int> validTiles, CancellationToken cancellationToken = default) => UniTask.FromResult<Vector2Int?>(null);
            public UniTask<int> RequestSelectCard(string prompt, List<int> validCardIds, CancellationToken cancellationToken = default) => UniTask.FromResult(-1);
            public UniTask PlayShuffleAndReveal(List<HitLocationRuntimeState> allCards, List<HitLocationRuntimeState> toReveal, CancellationToken cancellationToken = default) => UniTask.CompletedTask;
            public UniTask<HitLocationRuntimeState> RequestSelectRevealedCard(string prompt, List<HitLocationRuntimeState> revealedCards, CancellationToken cancellationToken = default) => UniTask.FromResult<HitLocationRuntimeState>(null);
            public UniTask<WeaponData> RequestSelectWeapon(string prompt, List<WeaponData> candidates, CancellationToken cancellationToken = default) => UniTask.FromResult<WeaponData>(null);
        }

        private sealed class TestGameContext : IGameContext
        {
            private readonly TestBoss boss = new();
            public TurnPhase CurrentPhase => TurnPhase.BossTurn;
            public int CurrentTurnNumber => 1;
            public IReadOnlyList<ICharacterState> PlayerCharacters => Array.Empty<ICharacterState>();
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
            public string Name => "Boss";
            public int CurrentTimePoints => 0;
            public IReadOnlyList<int> PendingActionCardIds => Array.Empty<int>();
            public IReadOnlyList<int> RevealedNextCardIds => Array.Empty<int>();
        }
    }
}
