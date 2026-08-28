using System.Collections.Generic;
using System.Linq;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Hunt;
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
            Assert.That(stranger.Understanding, Is.Zero);
            Assert.That(eventSystem.PrepareChoice(gameEvent, optionIndex, watcher), Is.Not.Null);
        }

        [Test]
        public void HuntResourceAvailability_UsesSquadCollectiblesInsteadOfSettlementInventory()
        {
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 9104) { Name = "携带者" };
            var deadHunter = new HunterInstance(null, 9105) { Name = "已故携带者" };
            deadHunter.HP.head = 0;
            var resource = UnityEngine.ScriptableObject.CreateInstance<ItemData>();
            resource.itemName = "测试素材";
            resource.itemType = ItemType.Resource;
            resource.ConfigureContentId("test_hunt_resource");
            var gameEvent = UnityEngine.ScriptableObject.CreateInstance<EventData>();
            gameEvent.ConfigureContentId("test_hunt_resource_event");
            var option = new EventOption
            {
                alwaysAvailable = false,
                conditions = new List<EventOptionCondition>
                {
                    new() { conditionKind = EventOptionConditionKind.MinimumResource, key = resource.ContentId, value = 2 }
                }
            };
            gameEvent.options.Add(option);
            settlement.Hunters.Add(hunter);
            settlement.Hunters.Add(deadHunter);
            settlement.AddResource(resource.ContentId, 9);
            hunter.Collectibles = new List<ItemInstance> { new(resource, 1) };
            deadHunter.Collectibles = new List<ItemInstance> { new(resource, 7) };
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            var manager = new HuntManager(eventSystem, bindInitialContent: false);
            manager.OnEnter(new List<HunterInstance> { hunter, deadHunter });

            try
            {
                PlayableSettlementItemRegistry.Configure(new[] { resource });
                var command = new HuntEventResourceCommand(manager);

                Assert.That(command.Scope, Is.EqualTo(PlayableEventResourceScope.HuntCollectibles));
                Assert.That(command.GetAvailableAmount(resource.ContentId), Is.EqualTo(1));
                Assert.That(command.GetAvailableAmount("unknown_resource"), Is.Zero);
                Assert.That(PlayableEventOptionAvailability.GetRequirements(option, command), Does.Contain("小队携带"));
                Assert.That(PlayableEventOptionAvailability.GetRequirements(option, new ScopedResourceAvailability(PlayableEventResourceScope.Settlement)), Does.Contain("营地拥有"));
                Assert.That(PlayableEventOptionAvailability.CanUse(option, null, settlement, command, out _), Is.False);
                Assert.That(eventSystem.PrepareChoice(gameEvent, 0, resourceCommand: command), Is.Null);

                option.conditions[0].value = 1;
                Assert.That(PlayableEventOptionAvailability.CanUse(option, null, settlement, command, out string huntReason), Is.True, huntReason);
                Assert.That(PlayableEventOptionAvailability.CanUse(option, null, settlement, out string settlementReason), Is.True, settlementReason);
            }
            finally
            {
                PlayableSettlementItemRegistry.Configure(null);
                UnityEngine.Object.DestroyImmediate(gameEvent);
                UnityEngine.Object.DestroyImmediate(resource);
            }
        }

        [Test]
        public void HuntCarriedItemOption_UsesSelectedHuntersOwnInventory()
        {
            ItemData dressing = PlayableItemTableRuntime.GetItems().First(item => item.ContentId == "weathered_field_dressing");
            var settlement = new SettlementInstance();
            var carrier = new HunterInstance(null, 9116) { Name = "携带者" };
            var companion = new HunterInstance(null, 9117) { Name = "同伴" };
            carrier.Collectibles.Add(new ItemInstance(dressing, 1));
            settlement.Hunters.Add(carrier);
            settlement.Hunters.Add(companion);
            var manager = new HuntManager(new EventSystem(settlement, new FirstRandom()), bindInitialContent: false);
            manager.OnEnter(new List<HunterInstance> { carrier, companion });
            try
            {
                PlayableSettlementItemRegistry.Configure(PlayableItemTableRuntime.GetItems());
                EventData gameEvent = PlayableEventTableRuntime.GetEvents().First(item => item.ContentId == "hunt_worm_rain");
                EventOption option = gameEvent.options.First(item => item.successEffects.Any(effect => effect.effectType == EventEffectType.RemoveItem));
                var itemCommand = new HuntEventItemCommand(manager);
                IPlayableEventResourceAvailability availability = PlayableEventAvailabilityScope.Compose(new HuntEventResourceCommand(manager), itemCommand);

                Assert.That(option.conditions.Single().conditionKind, Is.EqualTo(EventOptionConditionKind.MinimumCarriedItem));
                Assert.That(PlayableEventOptionAvailability.GetRequirements(option, availability), Does.Contain("旧式包扎布 ×1"));
                Assert.That(PlayableEventOptionAvailability.CanUse(option, carrier, settlement, availability, out string reason), Is.True, reason);
                Assert.That(PlayableEventOptionAvailability.CanUse(option, companion, settlement, availability, out reason), Is.False);
                Assert.That(reason, Does.Contain("旧式包扎布"));
            }
            finally
            {
                PlayableSettlementItemRegistry.Configure(null);
            }
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
        public void StoneForestGearAndAilment_UnlockHuntEventBranches()
        {
            ItemData blade = PlayableItemTableRuntime.GetItems().First(item => item.ContentId == "bone_saw_blade");
            ItemData bracer = PlayableItemTableRuntime.GetItems().First(item => item.ContentId == "carapace_bracer");
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 9115) { Name = "荒原采集者" };
            hunter.Equipment.Add(new ItemInstance(blade));
            hunter.Equipment.Add(new ItemInstance(bracer));
            hunter.EquippedItemIds.Add(blade.ContentId);
            hunter.EquippedItemIds.Add(bracer.ContentId);
            Assert.That(PlayableSymptomRuntime.TryAcquire(hunter, "symptom_whisper_sickness", out _, out bool added, out string acquireReason), Is.True, acquireReason);
            Assert.That(added, Is.True);
            settlement.Hunters.Add(hunter);

            try
            {
                PlayableSettlementItemRegistry.Configure(PlayableItemTableRuntime.GetItems());
                string[] eventIds = { "hunt_sap_suture", "hunt_carapace_cairn", "hunt_white_hair_lure", "hunt_root_pulse", "hunt_worm_rain" };
                foreach (string eventId in eventIds)
                {
                    EventData gameEvent = PlayableEventTableRuntime.GetEvents().First(item => item.ContentId == eventId);
                    EventOption guardedOption = gameEvent.options.First(option => !option.alwaysAvailable);
                    Assert.That(PlayableEventOptionAvailability.CanUse(guardedOption, hunter, settlement, out string reason), Is.True, $"{eventId}: {reason}");
                }
                EventOption symptomOption = PlayableEventTableRuntime.GetEvents().First(item => item.ContentId == "hunt_root_pulse").options.First(option => !option.alwaysAvailable);
                Assert.That(symptomOption.conditions.Single().displayName, Is.EqualTo("低语症"));
                Assert.That(hunter.Ailments, Does.Contain("低语症").And.Not.Contain("symptom_whisper_sickness"));
                Assert.That(PlayableEventOptionAvailability.CanUse(symptomOption, new HunterInstance(null, 9118), settlement, out string unavailableReason), Is.False);
                Assert.That(unavailableReason, Does.Contain("低语症"));
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
            Assert.That(eventSystem.PrepareChoice(gameEvent, optionIndex, hunter), Is.Null);
            Assert.That(settlement.GetResource("black_salt"), Is.Zero);
            Assert.That(hunter.IsAlive, Is.True);

            var manager = new SettlementManager(1);
            var foreign = new HunterInstance(null, 9107) { Name = "外来交易者" };
            Assert.That(manager.Events.PrepareChoice(gameEvent, optionIndex, foreign), Is.Null);
            Assert.That(manager.Data.GetResource("black_salt"), Is.Zero);
            Assert.That(foreign.IsAlive, Is.True);
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }

        private sealed class ScopedResourceAvailability : IPlayableEventResourceAvailability
        {
            public ScopedResourceAvailability(PlayableEventResourceScope scope)
            {
                Scope = scope;
            }

            public PlayableEventResourceScope Scope { get; }
            public int GetAvailableAmount(string resourceId) => 0;
        }
    }
}
