using System.Collections.Generic;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableInventionActiveEffectActionTests
    {
        [Test]
        public async Task Activate_ReusesEventChainAndEnforcesPersistedAnnualLimit()
        {
            var settlement = new SettlementInstance { CurrentYear = 2 };
            InventionData invention = CreateInvention();
            EventData gameEvent = CreateEvent();
            settlement.UnlockInvention(invention.ContentId);
            var inventionSystem = new InventionSystem(settlement) { AllInventions = new List<InventionData> { invention } };
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            using var session = new PlayableSettlementActionSession(settlement, new EmptyWeaponTrainingContent(), eventSystem, inventionSystem: inventionSystem, resolveEvent: id => id == gameEvent.name ? gameEvent : null);

            try
            {
                SettlementInventionActiveEffectCommandResult first = await session.ActivateInventionEffectAsync(invention, invention.activeEffects[0]);
                SettlementInventionActiveEffectCommandResult repeated = await session.ActivateInventionEffectAsync(invention, invention.activeEffects[0]);
                settlement.CurrentYear++;
                SettlementInventionActiveEffectCommandResult nextYear = await session.ActivateInventionEffectAsync(invention, invention.activeEffects[0]);

                Assert.That(first.Succeeded, Is.True, first.Reason);
                Assert.That(repeated.Succeeded, Is.False);
                Assert.That(nextYear.Succeeded, Is.True, nextYear.Reason);
                Assert.That(settlement.GetResource("broken_stone"), Is.EqualTo(2));
                Assert.That(InventionActiveEffectRules.GetUseCount(settlement.InventionActiveEffectUses, "prayer:vigil", 2), Is.EqualTo(1));
                Assert.That(InventionActiveEffectRules.GetUseCount(settlement.InventionActiveEffectUses, "prayer:vigil", 3), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(invention);
                Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public async Task PreventedRoot_LeavesEventAndUsageUncommitted()
        {
            var settlement = new SettlementInstance { CurrentYear = 1 };
            InventionData invention = CreateInvention();
            EventData gameEvent = CreateEvent();
            settlement.UnlockInvention(invention.ContentId);
            var inventionSystem = new InventionSystem(settlement) { AllInventions = new List<InventionData> { invention } };
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            using var session = new PlayableSettlementActionSession(settlement, new EmptyWeaponTrainingContent(), eventSystem, inventionSystem: inventionSystem, resolveEvent: id => id == gameEvent.name ? gameEvent : null);
            session.Reactors.RegisterGlobal(new PreventActivationReactor());

            try
            {
                SettlementInventionActiveEffectCommandResult result = await session.ActivateInventionEffectAsync(invention, invention.activeEffects[0]);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(settlement.GetResource("broken_stone"), Is.Zero);
                Assert.That(settlement.InventionActiveEffectUses, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(invention);
                Object.DestroyImmediate(gameEvent);
            }
        }

        private static InventionData CreateInvention()
        {
            InventionData invention = ScriptableObject.CreateInstance<InventionData>();
            invention.ConfigureContentId("prayer");
            invention.inventionName = "祈祷";
            invention.activeEffects.Add(new InventionActiveEffect { effectId = "prayer:vigil", effectName = "夜祷", eventId = "active_prayer", maxUsesPerYear = 1 });
            return invention;
        }

        private static EventData CreateEvent()
        {
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = "active_prayer";
            gameEvent.eventName = "夜祷";
            gameEvent.eventType = GameEventType.Narrative;
            gameEvent.category = EventCategory.Triggered;
            gameEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.AddResource, targetName = "broken_stone", value = 1 });
            return gameEvent;
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }

        private sealed class EmptyWeaponTrainingContent : IWeaponTrainingContent
        {
            public string RequiredInventionId => string.Empty;
            public string CostResourceId => string.Empty;
            public int ResourceCost => 0;
            public int Experience => 0;
            public bool TryGetFamily(string masteryId, out WeaponMasteryFamilyDefinition family)
            {
                family = null;
                return false;
            }
        }

        private sealed class PreventActivationReactor : GameActionReactor<ActivateSettlementInventionEffectAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(ActivateSettlementInventionEffectAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试阻止主动效果");
        }
    }
}
