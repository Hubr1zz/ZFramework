using System.Collections.Generic;
using System.Linq;
using Core;
using GameplayBase.CombatSystem;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableEventFatalInjuryIntegrationTests
    {
        [Test]
        public void FatalInjuryDeath_UsesHunterManagementAftermathExactlyOnce()
        {
            var settlement = new SettlementInstance { CurrentYear = 4 };
            var deceased = new HunterInstance(null, 1) { Name = "先行者", Age = 3, IsAlive = true };
            var survivor = new HunterInstance(null, 2) { Name = "守望者", Age = 2, IsAlive = true };
            settlement.Hunters.Add(deceased);
            settlement.Hunters.Add(survivor);
            deceased.HP.arms = 0;
            deceased.SurvivalCards = 0;
            deceased.DeathCards = 1;

            ItemData gear = ScriptableObject.CreateInstance<ItemData>();
            gear.name = "fatal_stable_gear";
            gear.ConfigureContentId("fatal-stable-gear");
            gear.itemName = "稳定装备";
            gear.itemType = ItemType.Weapon;
            deceased.Equipment.Add(new ItemInstance(gear));
            deceased.EquippedItemIds.Add(gear.ContentId);

            List<ItemData> previousItems = PlayableSettlementItemRegistry.Items.ToList();
            PlayableSettlementItemRegistry.Configure(previousItems.Concat(new[] { gear }));
            var management = new HunterManagementSystem(settlement, new FirstRandom());
            var command = new PlayableHuntFatalInjuryCommand(settlement, new FirstRandom(), new FirstRandom(), management);
            var effect = new EventEffect
            {
                effectType = EventEffectType.FatalInjury,
                targetName = "selected",
                bodyPart = "arms",
                fatalDeckId = EventFatalInjuryRules.HunterDeathDeckId,
                value = 1,
                description = "压垮手臂"
            };
            int deathEventCount = 0;
            System.Action<HunterDiedEvent> deathHandler = _ => deathEventCount++;
            EventBus.Subscribe(deathHandler);

            try
            {
                Assert.That(command.TryPrepare(effect, deceased, out PlayableEventFatalInjuryPreparation preparation, out string prepareReason), Is.True, prepareReason);
                Assert.That(preparation.RequiresDeathDraw, Is.True);
                Assert.That(preparation.FacedownCardTypes, Is.EqualTo(new[] { DeathCardType.Death }));

                Assert.That(command.TryCommit(preparation, 0, "fatal-integration", 0, out PlayableEventEffectResult result, out string commitReason), Is.True, commitReason);
                Assert.That(result.DeathCard, Is.EqualTo(DeathCardType.Death));
                Assert.That(result.HunterDied, Is.True);
                Assert.That(deceased.IsAlive, Is.False);
                Assert.That(settlement.GetStoredEquipment(gear.ContentId), Is.EqualTo(1));
                Assert.That(settlement.Timeline.FindAll(entry => entry.EventId == "death:1"), Has.Count.EqualTo(1));
                Assert.That(survivor.UnspentGrowth, Is.EqualTo(1));
                Assert.That(deathEventCount, Is.EqualTo(1));

                Assert.That(command.TryCommit(preparation, 0, "fatal-integration", 0, out _, out _), Is.False);
                Assert.That(command.TryPrepare(effect, deceased, out _, out _), Is.False);
                Assert.That(settlement.GetStoredEquipment(gear.ContentId), Is.EqualTo(1));
                Assert.That(settlement.Timeline.FindAll(entry => entry.EventId == "death:1"), Has.Count.EqualTo(1));
                Assert.That(survivor.UnspentGrowth, Is.EqualTo(1));
                Assert.That(deathEventCount, Is.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe(deathHandler);
                PlayableSettlementItemRegistry.Configure(previousItems);
                Object.DestroyImmediate(gear);
            }
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
