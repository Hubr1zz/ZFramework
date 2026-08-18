using System.Collections.Generic;
using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class WeaponMasteryRulesTests
    {
        [Test]
        public void TryGain_DifferentFamilies_KeepIndependentExperience()
        {
            var hunter = CreateHunter();
            var blade = CreateFamily("blade", "刃器");
            var sling = CreateFamily("sling", "投石索");

            Assert.That(WeaponMasteryRules.TryGain(hunter, blade, 2, out _), Is.True);
            Assert.That(WeaponMasteryRules.TryGain(hunter, sling, 1, out _), Is.True);

            Assert.That(hunter.WeaponMasteries[0].Experience, Is.EqualTo(2));
            Assert.That(hunter.WeaponMasteries[1].Experience, Is.EqualTo(1));
            Assert.That(hunter.WeaponProficiency, Is.EqualTo(2));
        }

        [Test]
        public void TryGain_LegacyValue_MigratesOnlyIntoFirstUsedFamily()
        {
            var hunter = CreateHunter();
            hunter.WeaponProficiency = 2;

            WeaponMasteryRules.TryGain(hunter, CreateFamily("blade", "刃器"), 1, out WeaponMasteryGainOutcome bladeOutcome);
            WeaponMasteryRules.TryGain(hunter, CreateFamily("sling", "投石索"), 1, out WeaponMasteryGainOutcome slingOutcome);

            Assert.That(bladeOutcome.OldValue, Is.EqualTo(2));
            Assert.That(bladeOutcome.NewValue, Is.EqualTo(3));
            Assert.That(slingOutcome.OldValue, Is.Zero);
            Assert.That(slingOutcome.NewValue, Is.EqualTo(1));
        }

        [Test]
        public void TryGain_CrossedMilestone_ClaimsTraitOnce()
        {
            var hunter = CreateHunter();
            var milestones = new[] { new WeaponMasteryMilestoneDefinition("blade_2", "刃器学徒", 2, "刃器学徒") };
            var family = new WeaponMasteryFamilyDefinition("blade", "刃器", milestones);

            WeaponMasteryRules.TryGain(hunter, family, 2, out WeaponMasteryGainOutcome first);
            WeaponMasteryRules.TryGain(hunter, family, 1, out WeaponMasteryGainOutcome second);

            Assert.That(first.ReachedMilestoneNames, Is.EqualTo(new[] { "刃器学徒" }));
            Assert.That(second.ReachedMilestoneNames, Is.Empty);
            Assert.That(hunter.Traits.FindAll(trait => trait == "刃器学徒"), Has.Count.EqualTo(1));
        }

        private static HunterState CreateHunter()
        {
            return new HunterState { Name = "测试猎人", HP = new HunterHitPoints(), MaxHP = new HunterHitPoints() };
        }

        private static WeaponMasteryFamilyDefinition CreateFamily(string id, string displayName)
        {
            return new WeaponMasteryFamilyDefinition(id, displayName, new List<WeaponMasteryMilestoneDefinition>());
        }
    }
}
