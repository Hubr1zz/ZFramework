using HuntingInDarkness.GameCore.Combat;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class HitLocationSequenceRulesTests
    {
        [Test]
        public void PreviousSuccess_AppliesConfiguredToughnessModifier()
        {
            var definition = new HitLocationDefinition("torso", "躯干", string.Empty, 3, 1, 2, previousSuccessToughnessModifier: 2);

            Assert.That(HitLocationSequenceRules.GetEffectiveToughness(definition, false), Is.EqualTo(3));
            Assert.That(HitLocationSequenceRules.GetEffectiveToughness(definition, true), Is.EqualTo(5));
            Assert.That(HitLocationSequenceRules.DoesSuccessCardSucceed(definition, 4, true), Is.False);
            Assert.That(HitLocationSequenceRules.DoesSuccessCardSucceed(definition, 5, true), Is.True);
        }

        [Test]
        public void PersistentPart_StaysFaceUpUntilDestroyed()
        {
            var state = new HitLocationState(new HitLocationDefinition("limb", "前肢", string.Empty, 1, 1, 1, isPersistent: true));

            state.Reveal();
            state.ActivatePersistent();
            state.Hide();

            Assert.That(state.IsPersistentActive, Is.True);
            Assert.That(state.IsFaceUp, Is.True);
            Assert.That(state.ApplyDamage(1), Is.True);
            Assert.That(state.IsPersistentActive, Is.False);
            Assert.That(state.IsFaceUp, Is.True);
        }
    }
}
