using System.Linq;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEditor;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableConditionalEventOptionTests
    {
        private const string SymptomCatalogPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/Symptoms/PlayableSymptomCatalog.asset";

        [SetUp]
        public void SetUp()
        {
            PlayableSymptomRuntime.Configure(AssetDatabase.LoadAssetAtPath<PlayableSymptomCatalog>(SymptomCatalogPath));
            PlayableEventTableRuntime.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            PlayableEventTableRuntime.ClearCache();
            PlayableSymptomRuntime.Configure(null);
        }

        [Test]
        public void TableContent_ProvidesGuardedWatcherOption()
        {
            EventData gameEvent = PlayableEventTableRuntime.GetEvents().First(item => item.name == "main_giant_face");
            EventOption option = gameEvent.options.First(item => !item.alwaysAvailable);

            Assert.That(option.conditions, Has.Count.EqualTo(1));
            Assert.That(option.conditions[0].conditionKind, Is.EqualTo(EventOptionConditionKind.HasTrait));
            Assert.That(option.conditions[0].key, Is.EqualTo("trait_watcher"));
            Assert.That(option.conditions[0].displayName, Is.EqualTo("守望者"));
            Assert.That(option.successChain.Select(item => item.name), Is.EqualTo(new[] { "triggered_face_safe_path" }));
            Assert.That(option.successChain.Single().category, Is.EqualTo(EventCategory.Triggered));
        }

        [Test]
        public void TableContent_ResolvesEventLevelChainByStableId()
        {
            EventData gameEvent = PlayableEventTableRuntime.GetEvents().First(item => item.name == "main_face_echo");

            Assert.That(gameEvent.chainedEvents.Select(item => item.name), Is.EqualTo(new[] { "triggered_face_memory" }));
            Assert.That(gameEvent.chainedEvents.Single().category, Is.EqualTo(EventCategory.Triggered));
        }

        [Test]
        public void PrepareChoice_RejectsBypassAndAcceptsEligibleHunter()
        {
            var settlement = new SettlementInstance();
            var watcher = new HunterInstance(null, 9101) { Name = "守望者" };
            watcher.Traits.Add("trait_watcher");
            var stranger = new HunterInstance(null, 9102) { Name = "陌生人" };
            settlement.Hunters.Add(watcher);
            settlement.Hunters.Add(stranger);
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            EventData gameEvent = PlayableEventTableRuntime.GetEvents().First(item => item.name == "main_giant_face");
            int optionIndex = gameEvent.options.FindIndex(item => !item.alwaysAvailable);

            Assert.That(eventSystem.PrepareChoice(gameEvent, optionIndex, stranger), Is.Null);
            EventResolutionResult legacyResult = eventSystem.ResolveChoice(gameEvent, optionIndex, stranger);
            Assert.That(legacyResult.Success, Is.False);
            Assert.That(legacyResult.ResultText, Does.Contain("守望者"));
            Assert.That(stranger.Understanding, Is.Zero);
            Assert.That(eventSystem.PrepareChoice(gameEvent, optionIndex, watcher), Is.Not.Null);
        }

        [Test]
        public void StoneEquipment_UnlocksKeywordEventOption()
        {
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 9103) { Name = "持石者" };
            var stoneItem = UnityEngine.ScriptableObject.CreateInstance<ItemData>();
            stoneItem.itemName = "测试石器";
            stoneItem.itemType = ItemType.Weapon;
            stoneItem.tags.Add(ItemTag.Stone);
            hunter.EquippedItemNames.Add(stoneItem.itemName);
            settlement.Hunters.Add(hunter);

            try
            {
                PlayableSettlementItemRegistry.Configure(new[] { stoneItem });
                EventData gameEvent = PlayableEventTableRuntime.GetEvents().First(item => item.name == "random_stone_vigil");
                EventOption option = gameEvent.options.First(item => !item.alwaysAvailable);

                Assert.That(option.conditions[0].conditionKind, Is.EqualTo(EventOptionConditionKind.HasKeyword));
                Assert.That(PlayableEventOptionAvailability.CanUse(option, hunter, settlement, out string reason), Is.True, reason);
                PlayableEventChoiceTransaction transaction = new EventSystem(settlement, new FirstRandom()).PrepareChoice(gameEvent, gameEvent.options.IndexOf(option), hunter);
                Assert.That(transaction, Is.Not.Null);
                Assert.That(transaction.CommitStandalone().Result.Success, Is.True);
                Assert.That(settlement.GetResource("black_salt"), Is.EqualTo(1));
                Assert.That(hunter.Understanding, Is.EqualTo(1));
            }
            finally
            {
                PlayableSettlementItemRegistry.Configure(null);
                UnityEngine.Object.DestroyImmediate(stoneItem);
            }
        }

        [Test]
        public void EchoContent_ConnectsEquipmentKeywordsToSettlementAndHuntRewards()
        {
            ItemData echoWeapon = PlayableItemTableRuntime.GetItems().First(item => item.ContentId == "echo_hook_spear");
            ItemData quietArmor = PlayableItemTableRuntime.GetItems().First(item => item.ContentId == "stonewatch_mantle");
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 9112) { Name = "回声猎人" };
            hunter.Equipment.Add(new ItemInstance(echoWeapon));
            hunter.Equipment.Add(new ItemInstance(quietArmor));
            hunter.EquippedItemIds.Add(echoWeapon.ContentId);
            hunter.EquippedItemIds.Add(quietArmor.ContentId);
            settlement.Hunters.Add(hunter);

            try
            {
                PlayableSettlementItemRegistry.Configure(PlayableItemTableRuntime.GetItems());
                EventData settlementEvent = PlayableEventTableRuntime.GetEvents().First(item => item.name == "random_echo_knot");
                EventOption echoOption = settlementEvent.options.First(option => !option.alwaysAvailable);
                EventData huntEvent = PlayableEventTableRuntime.GetEvents().First(item => item.name == "hunt_singing_sinew");
                EventOption quietOption = huntEvent.options.First(option => !option.alwaysAvailable);
                var eventSystem = new EventSystem(settlement, new FirstRandom());

                Assert.That(PlayableEventOptionAvailability.CanUse(echoOption, hunter, settlement, out string echoReason), Is.True, echoReason);
                Assert.That(PlayableEventOptionAvailability.CanUse(quietOption, hunter, settlement, out string quietReason), Is.True, quietReason);
                Assert.That(eventSystem.PrepareChoice(settlementEvent, settlementEvent.options.IndexOf(echoOption), hunter).CommitStandalone().Result.Success, Is.True);
                Assert.That(eventSystem.PrepareChoice(huntEvent, huntEvent.options.IndexOf(quietOption), hunter).CommitStandalone().Result.Success, Is.True);
                Assert.That(hunter.Understanding, Is.EqualTo(1));
                Assert.That(settlement.GetResource("echo_sinew"), Is.EqualTo(3));
            }
            finally
            {
                PlayableSettlementItemRegistry.Configure(null);
            }
        }

        [Test]
        public void FateKnotEvent_OffersSafeAndRiskyBranchesAtConfiguredFateThresholds()
        {
            EventData gameEvent = PlayableEventTableRuntime.GetEvents().First(item => item.name == "random_fate_knots");
            EventOption safeOption = gameEvent.options.First(option => option.conditions.Any(condition => condition.conditionKind == EventOptionConditionKind.MaximumLuck));
            EventOption riskyOption = gameEvent.options.First(option => option.conditions.Any(condition => condition.conditionKind == EventOptionConditionKind.MinimumLuck));
            var settlement = new SettlementInstance();
            var lowFateHunter = new HunterInstance(null, 9113) { Name = "未缠命者", Luck = 1 };
            var highFateHunter = new HunterInstance(null, 9114) { Name = "缠命者", Luck = 2 };
            settlement.Hunters.Add(lowFateHunter);
            settlement.Hunters.Add(highFateHunter);
            var eventSystem = new EventSystem(settlement, new FirstRandom());

            Assert.That(PlayableEventOptionAvailability.CanUse(safeOption, lowFateHunter, settlement, out string reason), Is.True, reason);
            Assert.That(PlayableEventOptionAvailability.CanUse(safeOption, highFateHunter, settlement, out reason), Is.False);
            Assert.That(PlayableEventOptionAvailability.CanUse(riskyOption, lowFateHunter, settlement, out reason), Is.False);
            Assert.That(PlayableEventOptionAvailability.CanUse(riskyOption, highFateHunter, settlement, out reason), Is.True, reason);
            Assert.That(riskyOption.checkPresentation, Is.EqualTo(EventCheckPresentationKind.OldMaid));

            PlayableEventChoiceTransaction transaction = eventSystem.PrepareChoice(gameEvent, gameEvent.options.IndexOf(riskyOption), highFateHunter, riskyOption.checkSides);

            Assert.That(transaction, Is.Not.Null);
            Assert.That(transaction.CommitStandalone().Result.Success, Is.True);
            Assert.That(settlement.GetResource("black_salt"), Is.EqualTo(3));
        }

        [Test]
        public void BloodlineEvent_ActivatesOnlyMatchingInactiveHunter()
        {
            var settlement = new SettlementInstance();
            var listener = new HunterInstance(null, 9104) { Name = "听石者", BloodlineId = "stone-listener", BloodlineName = "听石之血" };
            var dreamer = new HunterInstance(null, 9105) { Name = "梦行者", BloodlineId = "deep-dreamer", BloodlineName = "深梦之血" };
            settlement.Hunters.Add(listener);
            settlement.Hunters.Add(dreamer);
            EventData gameEvent = PlayableEventTableRuntime.GetEvents().First(item => item.name == "random_bloodline_awakening");
            EventOption option = gameEvent.options.First(item => item.successEffects.Any(effect => effect.effectType == EventEffectType.ActivateBloodline && effect.targetName == "stone-listener"));
            int optionIndex = gameEvent.options.IndexOf(option);
            var eventSystem = new EventSystem(settlement, new FirstRandom());

            Assert.That(PlayableEventOptionAvailability.GetRequirements(option), Does.Contain("听石之血"));
            Assert.That(PlayableEventOptionAvailability.GetRequirements(option), Does.Not.Contain("stone-listener"));
            Assert.That(eventSystem.PrepareChoice(gameEvent, optionIndex, dreamer), Is.Null);
            PlayableEventChoiceTransaction transaction = eventSystem.PrepareChoice(gameEvent, optionIndex, listener);
            Assert.That(transaction, Is.Not.Null);
            Assert.That(listener.IsBloodlineActivated, Is.False);

            EventResolutionResult result = transaction.CommitStandalone().Result;

            Assert.That(result.Success, Is.True);
            Assert.That(listener.IsBloodlineActivated, Is.True);
            Assert.That(listener.Traits, Contains.Item("trait_stone_speaker"));
            Assert.That(eventSystem.PrepareChoice(gameEvent, optionIndex, listener), Is.Null);
        }

        [Test]
        public void CardInteractionEvent_MapsStableDeckAndPresentationFromTable()
        {
            EventData gameEvent = PlayableEventTableRuntime.GetEvents().First(item => item.name == "random_bone_omens");
            EventOption option = gameEvent.options.First(item => item.checkType != CheckType.None);

            Assert.That(option.checkPresentation, Is.EqualTo(EventCheckPresentationKind.FlipCards));
            Assert.That(option.checkCount, Is.EqualTo(1));
            Assert.That(option.checkSides, Is.EqualTo(10));
            Assert.That(option.checkDeckId, Is.EqualTo("bone-omens"));
            Assert.That(option.checkInstruction, Does.Contain("骨兆"));
        }

        [Test]
        public void OldMaidEvent_IsReachableFromRandomPoolWithStableDeckRules()
        {
            EventData gameEvent = PlayableEventTableRuntime.GetEvents().First(item => item.name == "random_faceless_hand");
            EventOption option = gameEvent.options.First(item => item.checkType != CheckType.None);

            Assert.That(gameEvent.category, Is.EqualTo(EventCategory.Random));
            Assert.That(gameEvent.maxYear, Is.LessThanOrEqualTo(0));
            Assert.That(option.checkPresentation, Is.EqualTo(EventCheckPresentationKind.OldMaid));
            Assert.That(option.checkCount, Is.EqualTo(1));
            Assert.That(option.checkSides, Is.EqualTo(10));
            Assert.That(option.checkDeckId, Is.EqualTo("faceless-hand"));
            Assert.That(option.successEffects.Any(effect => effect.effectType == EventEffectType.AddResource), Is.True);
            Assert.That(option.failEffects.Any(effect => effect.effectType == EventEffectType.AddRecoverableWound), Is.True);
        }

        [Test]
        public void OldMaidCheck_IgnoresAttributeBonusAndUsesCardOutcome()
        {
            EventData gameEvent = PlayableEventTableRuntime.GetEvents().First(item => item.name == "random_faceless_hand");
            EventOption option = gameEvent.options.First(item => item.checkPresentation == EventCheckPresentationKind.OldMaid);
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 9108) { Name = "幸运者", Luck = 99 };
            settlement.Hunters.Add(hunter);
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            int optionIndex = gameEvent.options.IndexOf(option);

            PlayableEventChoiceTransaction oldMaid = eventSystem.PrepareChoice(gameEvent, optionIndex, hunter, 1);
            PlayableEventChoiceTransaction safeCard = eventSystem.PrepareChoice(gameEvent, optionIndex, hunter, option.checkSides);

            Assert.That(oldMaid, Is.Not.Null);
            Assert.That(oldMaid.Total, Is.EqualTo(1));
            Assert.That(oldMaid.Success, Is.False);
            Assert.That(safeCard, Is.Not.Null);
            Assert.That(safeCard.Success, Is.True);
        }

        [Test]
        public void MultiCardCheck_AcceptsConfiguredTotalRangeAndReroll()
        {
            var gameEvent = UnityEngine.ScriptableObject.CreateInstance<EventData>();
            var option = new EventOption { optionText = "翻两张牌", checkType = CheckType.Luck, checkTarget = 12, checkPresentation = EventCheckPresentationKind.FlipCards, checkCount = 2, checkSides = 10, checkDeckId = "test-deck" };
            gameEvent.options.Add(option);
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 9109) { Name = "翻牌者", Willpower = 1, WillpowerMax = 1 };
            settlement.Hunters.Add(hunter);

            try
            {
                PlayableEventChoiceTransaction transaction = new EventSystem(settlement, new FirstRandom()).PrepareChoice(gameEvent, 0, hunter, 15);

                Assert.That(transaction, Is.Not.Null);
                Assert.That(transaction.RollValue, Is.EqualTo(15));
                Assert.That(transaction.TryReroll(20), Is.True);
                Assert.That(transaction.RollValue, Is.EqualTo(20));
                Assert.That(hunter.Willpower, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public void HunterTargetedEffect_RequiresActorEvenWithoutCheckOrCondition()
        {
            EventData gameEvent = PlayableEventTableRuntime.GetEvents().First(item => item.name == "random_dark_bargain");
            EventOption sacrifice = gameEvent.options.First(option => option.successEffects.Any(effect => effect.effectType == EventEffectType.KillHunter));
            EventEffect death = sacrifice.successEffects.First(effect => effect.effectType == EventEffectType.KillHunter);
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 9106) { Name = "交易者" };
            settlement.Hunters.Add(hunter);
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            int optionIndex = gameEvent.options.IndexOf(sacrifice);

            Assert.That(PlayableEventOptionAvailability.RequiresHunter(sacrifice), Is.True);
            Assert.That(death.targetName, Is.EqualTo("dark_bargain"));
            Assert.That(death.description, Is.Not.Empty);
            Assert.That(eventSystem.PrepareChoice(gameEvent, optionIndex), Is.Null);
            EventResolutionResult legacyResult = eventSystem.ResolveChoice(gameEvent, optionIndex);
            Assert.That(legacyResult.Success, Is.False);
            Assert.That(legacyResult.ResultText, Does.Contain("猎人"));
            Assert.That(eventSystem.PrepareChoice(gameEvent, optionIndex, hunter), Is.Null);
            EventResolutionResult missingPortResult = eventSystem.ResolveChoice(gameEvent, optionIndex, hunter);
            Assert.That(missingPortResult.Success, Is.False);
            Assert.That(missingPortResult.ResultText, Does.Contain("死亡流程"));
            Assert.That(settlement.GetResource("black_salt"), Is.Zero);
            Assert.That(hunter.IsAlive, Is.True);

            var manager = new SettlementManager(1);
            var foreign = new HunterInstance(null, 9107) { Name = "外来交易者" };
            Assert.That(manager.Events.PrepareChoice(gameEvent, optionIndex, foreign), Is.Null);
            EventResolutionResult foreignResult = manager.Events.ResolveChoice(gameEvent, optionIndex, foreign);
            Assert.That(foreignResult.Success, Is.False);
            Assert.That(foreignResult.ResultText, Does.Contain("不属于"));
            Assert.That(manager.Data.GetResource("black_salt"), Is.Zero);
            Assert.That(foreign.IsAlive, Is.True);
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
