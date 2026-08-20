using System.Linq;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableConditionalEventOptionTests
    {
        [Test]
        public void TableContent_ProvidesGuardedWatcherOption()
        {
            EventData gameEvent = PlayableEventTableRuntime.GetEvents().First(item => item.name == "main_giant_face");
            EventOption option = gameEvent.options.First(item => !item.alwaysAvailable);

            Assert.That(option.conditions, Has.Count.EqualTo(1));
            Assert.That(option.conditions[0].conditionKind, Is.EqualTo(EventOptionConditionKind.HasTrait));
            Assert.That(option.conditions[0].key, Is.EqualTo("守望者"));
        }

        [Test]
        public void PrepareChoice_RejectsBypassAndAcceptsEligibleHunter()
        {
            var settlement = new SettlementInstance();
            var watcher = new HunterInstance(null, 9101) { Name = "守望者" };
            watcher.Traits.Add("守望者");
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
                Assert.That(settlement.GetResource("黑盐"), Is.EqualTo(1));
                Assert.That(hunter.Understanding, Is.EqualTo(1));
            }
            finally
            {
                PlayableSettlementItemRegistry.Configure(null);
                UnityEngine.Object.DestroyImmediate(stoneItem);
            }
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
