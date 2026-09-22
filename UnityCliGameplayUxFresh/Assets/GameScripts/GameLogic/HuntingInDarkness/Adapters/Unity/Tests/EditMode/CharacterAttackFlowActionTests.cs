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
using HuntingInDarkness.GameCore.Foundation;
using NUnit.Framework;
using SO.Boss.ActionCard;
using SO.Boss.HitLocation;
using SO.Character;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class CharacterAttackFlowActionTests
    {
        [Test]
        public async Task ExecuteAsync_ResolvesDamagePartAndVictoryAsChildActions()
        {
            using var rig = new AttackRig();
            int damageFacts = 0;
            int destroyedFacts = 0;
            int defeatedFacts = 0;
            int completedFacts = 0;
            Action<EffectiveWeaponDamageEvent> damageHandler = _ => damageFacts++;
            Action<HitLocationDestroyedEvent> destroyedHandler = _ => destroyedFacts++;
            Action<BossDefeatedEvent> defeatedHandler = _ => defeatedFacts++;
            Action<AttackCompletedEvent> completedHandler = evt => completedFacts += evt.Completed ? 1 : 100;
            EventBus.Subscribe(damageHandler);
            EventBus.Subscribe(destroyedHandler);
            EventBus.Subscribe(defeatedHandler);
            EventBus.Subscribe(completedHandler);
            try
            {
                var damageReactor = new ObserveDamageReactor();
                var effectReactor = new ObservePartEffectReactor();
                rig.Environment.Reactors.RegisterGlobal(damageReactor);
                rig.Environment.Reactors.RegisterGlobal(effectReactor);

                ActionOutcome outcome = await rig.ExecuteAsync();

                Assert.That(outcome.IsSuccess, Is.True);
                Assert.That(rig.Part.IsDestroyed, Is.True);
                Assert.That(rig.Boss.CurrentHealth, Is.Zero);
                Assert.That(damageReactor.InvocationCount, Is.EqualTo(1));
                Assert.That(effectReactor.InvocationCount, Is.EqualTo(1));
                Assert.That(damageFacts, Is.EqualTo(1));
                Assert.That(destroyedFacts, Is.EqualTo(1));
                Assert.That(defeatedFacts, Is.EqualTo(1));
                Assert.That(completedFacts, Is.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe(damageHandler);
                EventBus.Unsubscribe(destroyedHandler);
                EventBus.Unsubscribe(defeatedHandler);
                EventBus.Unsubscribe(completedHandler);
            }
        }

        [Test]
        public async Task DamageReactor_PreventionConvertsHitToFailureAndContinuesFlow()
        {
            using var rig = new AttackRig();
            var reactor = new PreventDamageReactor();
            rig.Environment.Reactors.RegisterGlobal(reactor);
            int completedFacts = 0;
            Action<AttackCompletedEvent> handler = evt => completedFacts += evt.Completed ? 1 : 100;
            EventBus.Subscribe(handler);
            try
            {
                ActionOutcome outcome = await rig.ExecuteAsync();

                Assert.That(outcome.IsSuccess, Is.True);
                Assert.That(reactor.InvocationCount, Is.EqualTo(1));
                Assert.That(rig.Part.CurrentHp, Is.EqualTo(1));
                Assert.That(rig.Boss.CurrentHealth, Is.EqualTo(1));
                Assert.That(completedFacts, Is.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task DisposeEnvironment_CancelsPendingPlayerInputAndDiscardsFacts()
        {
            using var rig = new AttackRig(blockAtReveal: true);
            int completedFacts = 0;
            Action<AttackCompletedEvent> handler = _ => completedFacts++;
            EventBus.Subscribe(handler);
            try
            {
                Task<ActionOutcome> execution = rig.ExecuteAsync().AsTask();
                await rig.Input.WaitUntilBlockedAsync();

                rig.Environment.Dispose();
                ActionOutcome outcome = await execution;

                Assert.That(outcome.Status, Is.EqualTo(ActionStatus.Cancelled), outcome.ToString());
                Assert.That(rig.Input.CancellationObserved, Is.True);
                Assert.That(rig.Part.CurrentHp, Is.EqualTo(1));
                Assert.That(rig.Boss.CurrentHealth, Is.EqualTo(1));
                Assert.That(completedFacts, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task DisposeAfterDamage_PreservesCommittedStateFactsButNotAttackCompletion()
        {
            using var rig = new AttackRig(blockAtResult: true);
            int damageFacts = 0;
            int destroyedFacts = 0;
            int completedFacts = 0;
            Action<EffectiveWeaponDamageEvent> damageHandler = _ => damageFacts++;
            Action<HitLocationDestroyedEvent> destroyedHandler = _ => destroyedFacts++;
            Action<AttackCompletedEvent> completedHandler = _ => completedFacts++;
            EventBus.Subscribe(damageHandler);
            EventBus.Subscribe(destroyedHandler);
            EventBus.Subscribe(completedHandler);
            try
            {
                Task<ActionOutcome> execution = rig.ExecuteAsync().AsTask();
                await rig.Input.WaitUntilBlockedAsync();

                rig.Environment.Dispose();
                ActionOutcome outcome = await execution;

                Assert.That(outcome.Status, Is.EqualTo(ActionStatus.Cancelled), outcome.ToString());
                Assert.That(rig.Part.IsDestroyed, Is.True);
                Assert.That(rig.Boss.CurrentHealth, Is.Zero);
                Assert.That(damageFacts, Is.EqualTo(1));
                Assert.That(destroyedFacts, Is.EqualTo(1));
                Assert.That(completedFacts, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(damageHandler);
                EventBus.Unsubscribe(destroyedHandler);
                EventBus.Unsubscribe(completedHandler);
            }
        }

        private sealed class AttackRig : IDisposable
        {
            private readonly HitLocationCardData partData;
            private readonly WeaponData weapon;
            private readonly ActionEventOutbox outbox = new();

            public AttackRig(bool blockAtReveal = false, bool blockAtResult = false)
            {
                partData = ScriptableObject.CreateInstance<HitLocationCardData>();
                partData.locationName = "测试部位";
                partData.maxHp = 1;
                partData.drawWeight = 1;
                partData.toughness = 0;
                Part = new HitLocationRuntimeState(partData);
                weapon = ScriptableObject.CreateInstance<WeaponData>();
                weapon.weaponName = "测试武器";
                weapon.strengthBonus = 1;
                Boss = new BossRuntimeData { Id = 99, Name = "测试Boss", MaxHealth = 1 };
                GameContext = new TestGameContext(Boss, Part);
                Input = new AttackInput(blockAtReveal, blockAtResult);
                Environment = new ActionEnvironment(new ActionEnvironmentConfiguration
                {
                    Name = "AttackTest",
                    Kind = ActionEnvironmentKind.Combat,
                    MaxActionsPerChain = 64,
                    TraceCapacity = 24
                });

                var context = new AttackContext
                {
                    AttackerId = 7,
                    DefenderId = Boss.Id,
                    AttackerStats = new CharacterCombatStats { Strength = 1, Speed = 1 },
                    Weapon = weapon,
                    DefenderToughness = 0,
                    AllHitLocationStates = new List<HitLocationRuntimeState> { Part },
                    GameContext = GameContext
                };
                ReactorEntityHandle source = Environment.EntityHandles.GetOrCreate("hunter", "7", "测试猎人");
                ReactorEntityHandle target = Environment.EntityHandles.GetOrCreate("boss", "99", "测试Boss");
                Action = new CharacterAttackFlowAction(context, Input, new DefaultHitLocationEffectResolver(GameContext, null), new FirstRandom(), outbox, source, target);
            }

            public ActionEnvironment Environment { get; }
            public CharacterAttackFlowAction Action { get; }
            public AttackInput Input { get; }
            public BossRuntimeData Boss { get; }
            public HitLocationRuntimeState Part { get; }
            public TestGameContext GameContext { get; }

            public UniTask<ActionOutcome> ExecuteAsync() => Environment.ExecuteAsync(Action, outbox);

            public void Dispose()
            {
                Environment.Dispose();
                UnityEngine.Object.DestroyImmediate(partData);
                UnityEngine.Object.DestroyImmediate(weapon);
            }
        }

        private sealed class TestGameContext : IGameContext
        {
            private readonly HitLocationRuntimeState part;

            public TestGameContext(BossRuntimeData boss, HitLocationRuntimeState part)
            {
                Boss = boss;
                this.part = part;
            }

            public TurnPhase CurrentPhase => TurnPhase.PlayerTurn;
            public int CurrentTurnNumber => 1;
            public IReadOnlyList<ICharacterState> PlayerCharacters => Array.Empty<ICharacterState>();
            public IBossState Boss { get; }
            public IReadOnlyList<HitLocationRuntimeState> BossHitLocationStates => new[] { part };
            public IReadOnlyList<BossActionCardData> BossRevealedCards => Array.Empty<BossActionCardData>();
            public Character GetCharacter(int characterId) => null;
            public IReadOnlyList<ICharacterActionCardInstanceState> GetCardsOf(int characterId) => Array.Empty<ICharacterActionCardInstanceState>();
            public ICharacterActionCardInstanceState GetCard(int cardInstanceId) => null;
            public Vector3 GetEntityWorldPosition(int entityId) => Vector3.zero;
        }

        private sealed class AttackInput : IPlayerInputProvider, IAttackResultBatchInputProvider
        {
            private readonly bool blockAtReveal;
            private readonly bool blockAtResult;
            private readonly UniTaskCompletionSource blocked = new();

            public AttackInput(bool blockAtReveal, bool blockAtResult)
            {
                this.blockAtReveal = blockAtReveal;
                this.blockAtResult = blockAtResult;
            }

            public bool CancellationObserved { get; private set; }
            public UniTask WaitUntilBlockedAsync() => blocked.Task;
            public UniTask<int> RequestRoll(string prompt, int maxExclusive, CancellationToken cancellationToken = default) => UniTask.FromResult(0);
            public async UniTask ShowResult(string message, CancellationToken cancellationToken = default)
            {
                if (!blockAtResult) return;
                blocked.TrySetResult();
                var pending = new UniTaskCompletionSource();
                try
                {
                    await pending.Task.AttachExternalCancellation(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    CancellationObserved = true;
                    throw;
                }
            }
            public UniTask<int> RequestSelectTarget(string prompt, List<int> validTargetIds, CancellationToken cancellationToken = default) => UniTask.FromResult(-1);
            public UniTask<Vector2Int?> RequestSelectTile(string prompt, List<Vector2Int> validTiles, CancellationToken cancellationToken = default) => UniTask.FromResult<Vector2Int?>(null);
            public UniTask<int> RequestSelectCard(string prompt, List<int> validCardIds, CancellationToken cancellationToken = default) => UniTask.FromResult(-1);
            public UniTask<HitLocationRuntimeState> RequestSelectRevealedCard(string prompt, List<HitLocationRuntimeState> revealedCards, CancellationToken cancellationToken = default) => UniTask.FromResult(revealedCards[0]);
            public UniTask<WeaponData> RequestSelectWeapon(string prompt, List<WeaponData> candidates, CancellationToken cancellationToken = default) => UniTask.FromResult(candidates[0]);
            public UniTask RequestRevealAttackResult(string prompt, CancellationToken cancellationToken = default) => UniTask.CompletedTask;

            public async UniTask PlayShuffleAndReveal(List<HitLocationRuntimeState> allCards, List<HitLocationRuntimeState> toReveal, CancellationToken cancellationToken = default)
            {
                if (blockAtReveal)
                {
                    blocked.TrySetResult();
                    var pending = new UniTaskCompletionSource();
                    try
                    {
                        await pending.Task.AttachExternalCancellation(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        CancellationObserved = true;
                        throw;
                    }
                }
                foreach (HitLocationRuntimeState state in toReveal)
                    state.Reveal();
            }
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }

        private sealed class ObserveDamageReactor : GameActionReactor<ApplyHitLocationDamageAction>
        {
            public int InvocationCount { get; private set; }
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(ApplyHitLocationDamageAction action, ReactionContext context, ReactionResponse response) => InvocationCount++;
        }

        private sealed class ObservePartEffectReactor : GameActionReactor<ResolveHitLocationEffectsAction>
        {
            public int InvocationCount { get; private set; }
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(ResolveHitLocationEffectsAction action, ReactionContext context, ReactionResponse response) => InvocationCount++;
        }

        private sealed class PreventDamageReactor : GameActionReactor<ApplyHitLocationDamageAction>
        {
            public int InvocationCount { get; private set; }
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;

            protected override void React(ApplyHitLocationDamageAction action, ReactionContext context, ReactionResponse response)
            {
                InvocationCount++;
                response.Prevent("测试护盾阻止伤害");
            }
        }
    }
}
