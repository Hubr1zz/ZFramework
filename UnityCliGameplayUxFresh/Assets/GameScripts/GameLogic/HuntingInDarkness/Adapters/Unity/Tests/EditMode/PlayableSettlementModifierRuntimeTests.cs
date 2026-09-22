using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableSettlementModifierRuntimeTests
    {
        [Test]
        public void Synchronize_LegacySaveSeedsContributionWithoutApplyingTwice()
        {
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 1) { WillpowerMax = 3, Willpower = 2 };
            settlement.Hunters.Add(hunter);
            settlement.UnlockInvention("ritual");
            InventionData invention = CreateInvention();
            try
            {
                Assert.That(PlayableSettlementModifierRuntime.Synchronize(settlement, new[] { invention }), Is.True);
                Assert.That(hunter.WillpowerMax, Is.EqualTo(3));
                Assert.That(hunter.SettlementModifierContributions, Has.Count.EqualTo(1));
                Assert.That(settlement.SettlementModifierSchemaVersion, Is.EqualTo(PlayableSettlementModifierRuntime.CurrentSchemaVersion));
            }
            finally
            {
                Object.DestroyImmediate(invention);
            }
        }

        [Test]
        public void Synchronize_PreservesExplicitEffectiveValueOverride()
        {
            var settlement = new SettlementInstance { SettlementModifierSchemaVersion = 1 };
            settlement.UnlockInvention("ritual");
            settlement.ActiveModifiers.Add(new SettlementModifierState { ModifierId = "ritual:willpower", SourceKind = SettlementModifierSourceKind.Invention, SourceId = "ritual", Kind = InventionEffectKind.ModifyWillpowerMaximum, Target = InventionEffectTarget.AllLivingAndFutureHunters, ConfiguredValue = 1, Value = 2, HasValueOverride = true });
            InventionData invention = CreateInvention();
            invention.unlockEffects[0].value = 3;
            try
            {
                Assert.That(PlayableSettlementModifierRuntime.Synchronize(settlement, new[] { invention }), Is.True);
                Assert.That(settlement.ActiveModifiers[0].ConfiguredValue, Is.EqualTo(3));
                Assert.That(settlement.ActiveModifiers[0].Value, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(invention);
            }
        }

        private static InventionData CreateInvention()
        {
            InventionData invention = ScriptableObject.CreateInstance<InventionData>();
            invention.ConfigureContentId("ritual");
            invention.unlockEffects.Add(new InventionPassiveEffect { lifetime = InventionEffectLifetime.Campaign, modifierId = "ritual:willpower", kind = InventionEffectKind.ModifyWillpowerMaximum, target = InventionEffectTarget.AllLivingAndFutureHunters, value = 1 });
            return invention;
        }
    }
}
