using System.Collections.Generic;
using System.Threading;
using Config;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.Card.CharacterActionCard;
using GameplayBase.CombatSystem;
using GameplayBase.Card.Effect;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using SO.Character;
using SO.Boss.ActionCard;
using UnityEditor;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableHunterCombatAdapterTests
    {
        private readonly List<Object> createdObjects = new();

        [SetUp]
        public void SetUp()
        {
            PlayableHunterCombatAdapter.Configure(null);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
                if (createdObject != null)
                    Object.DestroyImmediate(createdObject);
            createdObjects.Clear();
        }

        [Test]
        public void Apply_MapsPrimaryWeaponAndHunterStats()
        {
            var item = CreateItem("粗制石刃", 2, 3);
            var hunter = CreateHunter("猎人甲", strength: 4, evasion: 2, willpower: 1);
            hunter.Equipment.Add(new ItemInstance(item));
            hunter.EquippedItemNames.Add(item.itemName);
            var character = new CharacterRuntimeData { Id = 1 };
            var timeline = new TimelineManager();

            CombatRosterBindingResult result = PlayableHunterCombatAdapter.Apply(new[] { hunter }, new[] { character }, null, timeline);

            Assert.That(result.IsComplete, Is.True);
            Assert.That(character.Name, Is.EqualTo("猎人甲"));
            Assert.That(character.CombatStats.Strength, Is.EqualTo(4));
            Assert.That(character.CombatStats.Evasion, Is.EqualTo(2));
            Assert.That(character.CombatStats.Speed, Is.EqualTo(3));
            Assert.That(character.EquippedWeapon.weaponName, Is.EqualTo("粗制石刃"));
            Assert.That(character.EquippedWeapon.strengthBonus, Is.EqualTo(2));
            Assert.That(timeline.GetWillpower(1), Is.EqualTo(1));
        }

        [Test]
        public void Apply_MapsTwoWeaponsAndActivatesSelectedWeaponProfile()
        {
            var knife = CreateItem("粗制石刃", 2, 1, accuracy: 0, range: 1);
            var sling = CreateItem("筋腱投石索", 1, 2, accuracy: 1, range: 3);
            var hunter = CreateHunter("双武器猎人", strength: 2, evasion: 1, willpower: 2);
            hunter.Equipment.Add(new ItemInstance(knife));
            hunter.Equipment.Add(new ItemInstance(sling));
            var character = new CharacterRuntimeData { Id = 1 };

            PlayableHunterCombatAdapter.Apply(new[] { hunter }, new[] { character }, null, new TimelineManager());
            List<WeaponData> weapons = character.GetAvailableWeapons();
            PlayableHunterCombatAdapter.ActivateWeapon(character, weapons[1]);

            Assert.That(weapons, Has.Count.EqualTo(2));
            Assert.That(character.EquippedWeapon.weaponName, Is.EqualTo("筋腱投石索"));
            Assert.That(character.CombatStats.Speed, Is.EqualTo(2));
            Assert.That(PlayableHunterCombatAdapter.TryGetWeaponProfile(character.EquippedWeapon, out var profile), Is.True);
            Assert.That(profile.Accuracy, Is.EqualTo(1));
            Assert.That(profile.Range, Is.EqualTo(3));
            Assert.That(PlayableHunterCombatAdapter.IsWithinRange(weapons[0], 2), Is.False);
            Assert.That(PlayableHunterCombatAdapter.IsWithinRange(weapons[1], 2), Is.True);
        }

        [Test]
        public void Apply_FiltersUndeployedCharacterSlots()
        {
            var hunter = CreateHunter("唯一出发者", strength: 1, evasion: 1, willpower: 2);
            var first = new CharacterRuntimeData { Id = 1 };
            var second = new CharacterRuntimeData { Id = 2 };

            CombatRosterBindingResult result = PlayableHunterCombatAdapter.Apply(new[] { hunter }, new[] { first, second }, null, new TimelineManager());

            Assert.That(result.BoundHunterCount, Is.EqualTo(1));
            Assert.That(PlayableHunterCombatAdapter.FilterActiveCharacters(new[] { first, second }), Has.Count.EqualTo(1));
            Assert.That(PlayableHunterCombatAdapter.IsCharacterActive(first), Is.True);
            Assert.That(PlayableHunterCombatAdapter.IsCharacterActive(second), Is.False);
        }

        [Test]
        public void Apply_SecondRosterDoesNotOverwriteFirstRosterState()
        {
            var firstHunter = CreateHunter("第一场猎人", strength: 1, evasion: 0, willpower: 2);
            var firstActive = new CharacterRuntimeData { Id = 1 };
            var firstInactive = new CharacterRuntimeData { Id = 2 };
            PlayableHunterCombatAdapter.Apply(new[] { firstHunter }, new[] { firstActive, firstInactive }, null, new TimelineManager());

            var secondHunter = CreateHunter("第二场猎人", strength: 2, evasion: 1, willpower: 3);
            var secondActive = new CharacterRuntimeData { Id = 1 };
            PlayableHunterCombatAdapter.Apply(new[] { secondHunter }, new[] { secondActive }, null, new TimelineManager());

            Assert.That(PlayableHunterCombatAdapter.IsCharacterActive(firstActive), Is.True);
            Assert.That(PlayableHunterCombatAdapter.IsCharacterActive(firstInactive), Is.False);
            Assert.That(firstActive.Name, Is.EqualTo("第一场猎人"));
            Assert.That(secondActive.Name, Is.EqualTo("第二场猎人"));
        }

        [Test]
        public void Apply_UsesConfiguredUnarmedWeapon()
        {
            var fist = CreateObject<WeaponData>();
            fist.weaponName = "Fist";
            var catalog = CreateObject<PlayableCombatEquipmentCatalog>();
            var serializedCatalog = new SerializedObject(catalog);
            serializedCatalog.FindProperty("unarmedWeapon").objectReferenceValue = fist;
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            PlayableHunterCombatAdapter.Configure(catalog);
            var hunter = CreateHunter("徒手猎人", strength: 1, evasion: 0, willpower: 2);
            var character = new CharacterRuntimeData { Id = 1 };

            PlayableHunterCombatAdapter.Apply(new[] { hunter }, new[] { character }, null, new TimelineManager());

            Assert.That(character.EquippedWeapon, Is.SameAs(fist));
            Assert.That(character.CombatStats.Speed, Is.EqualTo(1));
        }

        [Test]
        public void Apply_WithoutHuntRosterPreservesConfiguredCombatants()
        {
            var first = new CharacterRuntimeData { Id = 1 };
            var second = new CharacterRuntimeData { Id = 2 };

            CombatRosterBindingResult result = PlayableHunterCombatAdapter.Apply(null, new[] { first, second }, null, new TimelineManager());

            Assert.That(result.IsComplete, Is.True);
            Assert.That(PlayableHunterCombatAdapter.FilterActiveCharacters(new[] { first, second }), Has.Count.EqualTo(2));
            Assert.That(PlayableHunterCombatAdapter.IsCharacterActive(second), Is.True);
        }

        [Test]
        public void PlayableCombatCards_ContainExecutableMoveAndAttackEffects()
        {
            var moveCard = AssetDatabase.LoadAssetAtPath<CharacterActionCardData>("Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Combat/PlayableCards/PlayableAdvance.asset");
            var attackCard = AssetDatabase.LoadAssetAtPath<CharacterActionCardData>("Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Combat/PlayableCards/PlayableStrike.asset");

            Assert.That(moveCard, Is.Not.Null);
            Assert.That(moveCard.faceUpEffects, Has.Count.EqualTo(1));
            Assert.That(moveCard.faceUpEffects[0].CreateRuntime(), Is.TypeOf<PlayablePreparedMoveEffect>());
            Assert.That(attackCard, Is.Not.Null);
            Assert.That(attackCard.faceUpEffects, Has.Count.EqualTo(1));
            Assert.That(attackCard.faceUpEffects[0].CreateRuntime(), Is.TypeOf<PlayablePreparedAttackEffect>());
            Assert.That(attackCard.IsDiscardable, Is.True);
        }

        [Test]
        public void InjuryAdapter_RestoresBodyStateArmorAndPersistentDeathDeck()
        {
            var hunter = CreateHunter("负伤猎人", strength: 1, evasion: 1, willpower: 2);
            hunter.MaxHP.body = 4;
            hunter.HP.body = 0;
            hunter.SurvivalCards = 1;
            hunter.DeathCards = 2;
            var armor = CreateObject<ItemData>();
            armor.itemName = "骨片胸甲";
            armor.itemType = ItemType.Armor;
            armor.armorStats = new ArmorStats { armorBody = 1 };
            hunter.Equipment.Add(new ItemInstance(armor));
            var stats = new CharacterCombatStats();

            PlayableHunterInjuryAdapter.Apply(hunter, stats);

            Assert.That(stats.InjuryState.GetPart(HunterBodyPart.Torso).CurrentHealth, Is.Zero);
            Assert.That(stats.InjuryState.GetPart(HunterBodyPart.Torso).Armor, Is.EqualTo(1));
            Assert.That(stats.InjuryState.DeathDeck.SurvivalCardCount, Is.EqualTo(1));
            Assert.That(stats.InjuryState.DeathDeck.DeathCardCount, Is.EqualTo(2));

            HunterDamageResult result = stats.ApplyDamage(HunterBodyPart.Torso, 2, new FirstCardRandom());
            PlayableHunterInjuryAdapter.Sync(hunter, stats);

            Assert.That(result.FatalInjuryTriggered, Is.True);
            Assert.That(result.IsDead, Is.False);
            Assert.That(hunter.DeathCards, Is.EqualTo(3));
            Assert.That(hunter.HP.body, Is.Zero);
        }

        [Test]
        public void CasualtyCoordinator_PermanentlyKillsReturnsEquipmentAndCompletesPartyDefeat()
        {
            var settlement = new SettlementInstance();
            var hunter = CreateHunter("阵亡猎人", strength: 1, evasion: 0, willpower: 1);
            var weapon = CreateItem("遗留石刃", 1, 1);
            hunter.Equipment.Add(new ItemInstance(weapon));
            hunter.EquippedItemNames.Add(weapon.itemName);
            settlement.Hunters.Add(hunter);
            var character = new CharacterRuntimeData { Id = 1, CombatStats = new CharacterCombatStats() };
            var timeline = new TimelineManager();
            PlayableHunterCombatAdapter.Apply(new[] { hunter }, new[] { character }, null, timeline);
            var hunterManagement = new HunterManagementSystem(settlement, new FirstCardRandom());
            int partyDefeatCount = 0;
            var coordinator = new PlayableCombatCasualtyCoordinator();
            coordinator.Bind(new[] { hunter }, new[] { character }, null, timeline, null, hunterManagement, () => partyDefeatCount++);

            try
            {
                EventBus.Publish(new CharacterDiedEvent { CharacterId = character.Id });

                Assert.That(hunter.IsAlive, Is.False);
                Assert.That(settlement.GetStoredEquipment(weapon.itemName), Is.EqualTo(1));
                Assert.That(hunter.Equipment, Is.Empty);
                Assert.That(PlayableHunterCombatAdapter.IsCharacterActive(character), Is.False);
                Assert.That(timeline.IsCharacterDone(character.Id), Is.True);
                Assert.That(partyDefeatCount, Is.EqualTo(1));
                EventBus.Publish(new CharacterDiedEvent { CharacterId = character.Id });
                Assert.That(partyDefeatCount, Is.EqualTo(1));
            }
            finally
            {
                coordinator.Dispose();
            }
        }

        [Test]
        public void SurvivalEventResolver_ShowsConfiguredNarrativeAndAppliesHunterEffect()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PlayableSurvivalEventCatalog>("Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Combat/SurvivalEvents/PlayableSurvivalEventCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.IsConfigured, Is.True);
            PlayableSurvivalEventRuntime.Configure(catalog);
            var settlement = new SettlementInstance();
            var hunter = CreateHunter("幸运猎人", strength: 1, evasion: 1, willpower: 1);
            settlement.Hunters.Add(hunter);
            var settlementEvents = new HuntingInDarkness.Settlement.EventSystem(settlement, new FirstCardRandom());
            var input = new RecordingInput();
            var resolver = new PlayableSurvivalEventResolver(_ => hunter, () => settlementEvents, new FirstCardRandom());
            var damage = new HunterDamageResult(HunterBodyPart.Torso, 1, 0, 0, 0, true, new DeathDrawResult(DeathCardType.Survive, true), null, false);

            resolver.ResolveAsync(1, damage, input).GetAwaiter().GetResult();

            Assert.That(input.Results, Has.Count.EqualTo(1));
            Assert.That(input.Results[0], Does.Contain("幸运儿"));
            Assert.That(hunter.Luck, Is.EqualTo(1));
        }

        [Test]
        public void BossWoundStep_WaitsForDamageResultBeforeSurvivalEvent()
        {
            var hunter = CreateHunter("幸存猎人", strength: 1, evasion: 0, willpower: 1);
            hunter.HP.body = 0;
            hunter.SurvivalCards = 1;
            hunter.DeathCards = 0;
            var stats = new CharacterCombatStats();
            PlayableHunterInjuryAdapter.Apply(hunter, stats);
            var trace = new List<string>();
            var input = new RecordingInput(trace);
            var resolver = new SurvivalResolverProbe(trace);
            var context = new AttackContext
            {
                DefenderId = 7,
                DefenderStats = stats,
                HitResult = HitResult.Success
            };
            var step = new BossAttackWoundStep(1, HunterBodyPart.Torso, new FirstCardRandom(), null, null, resolver);

            step.Execute(context, input).GetAwaiter().GetResult();

            Assert.That(trace, Is.EqualTo(new[] { "show", "survival" }));
        }

        [Test]
        public void BossWoundStep_OffersVisibleDeathDeckAndUsesSelectedBack()
        {
            var hunter = CreateHunter("抽牌猎人", strength: 1, evasion: 0, willpower: 1);
            hunter.HP.body = 0;
            hunter.SurvivalCards = 1;
            hunter.DeathCards = 1;
            var stats = new CharacterCombatStats();
            PlayableHunterInjuryAdapter.Apply(hunter, stats);
            var input = new DeathDeckRecordingInput(selectedPosition: 1);
            var context = new AttackContext
            {
                DefenderId = 8,
                DefenderStats = stats,
                HitResult = HitResult.Success
            };
            var step = new BossAttackWoundStep(1, HunterBodyPart.Torso, new FirstCardRandom());

            step.Execute(context, input).GetAwaiter().GetResult();

            Assert.That(input.Composition.SurvivalCards, Is.EqualTo(1));
            Assert.That(input.Composition.DeathCards, Is.EqualTo(1));
            Assert.That(input.Trace, Is.EqualTo(new[] { "preview", "draw", "reveal" }));
            Assert.That(stats.IsDead, Is.False);
            Assert.That(stats.InjuryState.DeathDeck.DeathCardCount, Is.EqualTo(2));
            Assert.That(input.Results[1], Does.Contain("翻开存活牌"));
        }

        [Test]
        public void PlayableBossContent_CreatesAttackAndPublishesVictoryAfterEveryPartIsDestroyed()
        {
            var boss = AssetDatabase.LoadAssetAtPath<BossConfigSO>("Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Combat/PlayableBoss/PlayableBossConfig.asset");
            Assert.That(boss, Is.Not.Null);
            Assert.That(boss.baseToughness, Is.EqualTo(2));
            Assert.That(boss.bossCardPool, Has.Count.EqualTo(1));
            Assert.That(boss.bossCardPool[0].effects, Has.Count.EqualTo(1));
            var attackData = boss.bossCardPool[0].effects[0] as PlayableBossAttackEffectData;
            Assert.That(attackData, Is.Not.Null);
            Assert.That((attackData.WoundCount, attackData.Accuracy, attackData.AttackCount), Is.EqualTo((1, 2, 1)));
            Assert.That(attackData.TargetPolicy, Is.EqualTo(BossTargetPolicy.PlayerChoice));
            Assert.That(attackData.CreateRuntime(), Is.TypeOf<PlayableDirectedBossAttackEffect>());
            Assert.That(boss.bossHitLocationPool, Has.Count.EqualTo(4));
            Assert.That(boss.bossHitLocationPool, Has.All.Matches<SO.Boss.HitLocation.HitLocationCardData>(location => !string.IsNullOrWhiteSpace(location.locationName)));
            Assert.That(boss.killLoot, Has.Count.EqualTo(2));

            var states = new List<HitLocationRuntimeState>();
            foreach (var location in boss.bossHitLocationPool)
            {
                var state = new HitLocationRuntimeState(location);
                while (!state.IsDestroyed)
                    state.ApplyDamage(1);
                states.Add(state);
            }

            int victoryCount = 0;
            System.Action<BossDefeatedEvent> handler = _ => victoryCount++;
            EventBus.Subscribe(handler);
            try
            {
                var step = new PlayableBossDefeatStep();
                Assert.That(step.TryPublish(states), Is.True);
                Assert.That(step.TryPublish(states), Is.False);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }

            Assert.That(victoryCount, Is.EqualTo(1));

            var controller = new BossController(new BossRuntimeData(), boss.bossCardPool, boss.bossHitLocationPool, null, null, boss.killLoot);
            try
            {
                controller.AccumulateDefeatLoot();
                Dictionary<string, int> loot = controller.GetAndClearLoot();
                Assert.That(loot["碎石"], Is.EqualTo(2));
                Assert.That(loot["柔软器官"], Is.EqualTo(1));
            }
            finally
            {
                controller.Dispose();
            }
        }

        private HunterInstance CreateHunter(string hunterName, int strength, int evasion, int willpower)
        {
            var hunter = new HunterInstance(null, 100)
            {
                Name = hunterName,
                Willpower = willpower
            };
            hunter.Stats.strength = strength;
            hunter.Stats.evasion = evasion;
            return hunter;
        }

        private ItemData CreateItem(string itemName, int power, int speed, int accuracy = 0, int range = 1)
        {
            var item = CreateObject<ItemData>();
            item.itemName = itemName;
            item.itemType = ItemType.Weapon;
            item.weaponStats ??= new WeaponStats();
            item.weaponStats.power = power;
            item.weaponStats.speed = speed;
            item.weaponStats.accuracy = accuracy;
            item.weaponStats.range = range;
            return item;
        }

        private T CreateObject<T>() where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>();
            createdObjects.Add(value);
            return value;
        }

        private sealed class FirstCardRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }

        private sealed class SurvivalResolverProbe : ISurvivalEventResolver
        {
            private readonly List<string> trace;

            public SurvivalResolverProbe(List<string> trace) => this.trace = trace;

            public UniTask ResolveAsync(int characterId, HunterDamageResult damage, IPlayerInputProvider input, CancellationToken cancellationToken = default)
            {
                trace.Add("survival");
                return UniTask.CompletedTask;
            }
        }

        private sealed class DeathDeckRecordingInput : IPlayerInputProvider, IDeathDeckInputProvider
        {
            private readonly int selectedPosition;
            public readonly List<string> Trace = new();
            public readonly List<string> Results = new();
            public DeathDeckComposition Composition { get; private set; }

            public DeathDeckRecordingInput(int selectedPosition) => this.selectedPosition = selectedPosition;

            public UniTask<int> RequestDrawDeathCard(string prompt, DeathDeckComposition composition, CancellationToken cancellationToken = default)
            {
                Composition = composition;
                Trace.Add("draw");
                return UniTask.FromResult(selectedPosition);
            }

            public UniTask<int> RequestRoll(string prompt, int maxExclusive, CancellationToken cancellationToken = default) => UniTask.FromResult(0);

            public UniTask ShowResult(string message, CancellationToken cancellationToken = default)
            {
                Results.Add(message);
                Trace.Add(Results.Count == 1 ? "preview" : "reveal");
                return UniTask.CompletedTask;
            }

            public UniTask<int> RequestSelectTarget(string prompt, List<int> validTargetIds, CancellationToken cancellationToken = default) => UniTask.FromResult(-1);
            public UniTask<Vector2Int?> RequestSelectTile(string prompt, List<Vector2Int> validTiles, CancellationToken cancellationToken = default) => UniTask.FromResult<Vector2Int?>(null);
            public UniTask<int> RequestSelectCard(string prompt, List<int> validCardIds, CancellationToken cancellationToken = default) => UniTask.FromResult(-1);
            public UniTask PlayShuffleAndReveal(List<HitLocationRuntimeState> allCards, List<HitLocationRuntimeState> toReveal, CancellationToken cancellationToken = default) => UniTask.CompletedTask;
            public UniTask<HitLocationRuntimeState> RequestSelectRevealedCard(string prompt, List<HitLocationRuntimeState> revealedCards, CancellationToken cancellationToken = default) => UniTask.FromResult<HitLocationRuntimeState>(null);
            public UniTask<WeaponData> RequestSelectWeapon(string prompt, List<WeaponData> candidates, CancellationToken cancellationToken = default) => UniTask.FromResult<WeaponData>(null);
        }

        private sealed class RecordingInput : IPlayerInputProvider
        {
            private readonly List<string> trace;
            public List<string> Results { get; } = new();

            public RecordingInput(List<string> trace = null) => this.trace = trace;

            public UniTask<int> RequestRoll(string prompt, int maxExclusive, CancellationToken cancellationToken = default) => UniTask.FromResult(0);

            public UniTask ShowResult(string message, CancellationToken cancellationToken = default)
            {
                Results.Add(message);
                trace?.Add("show");
                return UniTask.CompletedTask;
            }

            public UniTask<int> RequestSelectTarget(string prompt, List<int> validTargetIds, CancellationToken cancellationToken = default) => UniTask.FromResult(-1);
            public UniTask<Vector2Int?> RequestSelectTile(string prompt, List<Vector2Int> validTiles, CancellationToken cancellationToken = default) => UniTask.FromResult<Vector2Int?>(null);
            public UniTask<int> RequestSelectCard(string prompt, List<int> validCardIds, CancellationToken cancellationToken = default) => UniTask.FromResult(-1);
            public UniTask PlayShuffleAndReveal(List<HitLocationRuntimeState> allCards, List<HitLocationRuntimeState> toReveal, CancellationToken cancellationToken = default) => UniTask.CompletedTask;
            public UniTask<HitLocationRuntimeState> RequestSelectRevealedCard(string prompt, List<HitLocationRuntimeState> revealedCards, CancellationToken cancellationToken = default) => UniTask.FromResult<HitLocationRuntimeState>(null);
            public UniTask<WeaponData> RequestSelectWeapon(string prompt, List<WeaponData> candidates, CancellationToken cancellationToken = default) => UniTask.FromResult<WeaponData>(null);
        }
    }
}
