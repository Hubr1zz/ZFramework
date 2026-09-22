using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class HunterSymptomRulesTests
    {
        private static readonly SymptomDefinition cowardice = new SymptomDefinition("symptom_cowardice", "胆怯", "", new SymptomStatModifiers(-1, 0, 0, 0), new SymptomStatModifiers(0, 0, 1, 0), 2, 1, 2, 1);

        [Test]
        public void RegisterAndInternalize_AppliesEachModifierOnceAndRetainsAilment()
        {
            var hunter = new HunterState { Willpower = 3 };
            hunter.Stats.strength = 2;

            HunterSymptomRules.Register(hunter, cowardice);
            HunterSymptomRules.Register(hunter, cowardice);
            Assert.That(hunter.Stats.strength, Is.EqualTo(1));
            Assert.That(hunter.SymptomStates, Has.Count.EqualTo(1));

            Assert.That(HunterSymptomRules.TryInternalize(hunter, cowardice, 1, out string firstReason), Is.True, firstReason);
            Assert.That(HunterSymptomRules.TryInternalize(hunter, cowardice, 1, out _), Is.False);
            Assert.That(HunterSymptomRules.TryInternalize(hunter, cowardice, 2, out string secondReason), Is.True, secondReason);
            Assert.That(hunter.Willpower, Is.EqualTo(1));
            Assert.That(hunter.Stats.evasion, Is.EqualTo(1));
            Assert.That(hunter.Ailments, Contains.Item("胆怯"));
            Assert.That(hunter.Traits, Contains.Item("trait_symptom_cowardice_internalized"));
        }

        [Test]
        public void Overcome_ReversesAppliedPenaltyButKeepsEarnedInternalizationReward()
        {
            var hunter = new HunterState { Willpower = 3, Courage = 2, UnspentGrowth = 1 };
            hunter.Stats.strength = 2;
            HunterSymptomRules.Register(hunter, cowardice);
            HunterSymptomRules.TryInternalize(hunter, cowardice, 1, out _);
            HunterSymptomRules.TryInternalize(hunter, cowardice, 2, out _);

            Assert.That(HunterSymptomRules.TryOvercome(hunter, cowardice, out string reason), Is.True, reason);
            Assert.That(hunter.Stats.strength, Is.EqualTo(2));
            Assert.That(hunter.Stats.evasion, Is.EqualTo(1));
            Assert.That(hunter.UnspentGrowth, Is.Zero);
            Assert.That(hunter.Ailments, Does.Not.Contain("胆怯"));
            Assert.That(hunter.Traits, Contains.Item("trait_symptom_cowardice_internalized"));
            Assert.That(hunter.Traits, Contains.Item("trait_symptom_cowardice_overcome"));
            Assert.That(HunterSymptomRules.TryOvercome(hunter, cowardice, out _), Is.False);
        }
    }
}
