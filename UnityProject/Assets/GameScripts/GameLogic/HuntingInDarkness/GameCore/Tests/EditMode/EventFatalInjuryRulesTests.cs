using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunters;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests.EditMode
{
    public sealed class EventFatalInjuryRulesTests
    {
        [Test]
        public void PrepareDoesNotMutateDeckAndCommitUsesStablePosition()
        {
            var deck = new DeathDeck(new[] { DeathCardType.Survive, DeathCardType.Death });
            var state = new HunterInjuryState(HunterInjuryProfile.CreateDefault(), deck);
            state.ApplyDamage(HunterBodyPart.Arms, 3, new FirstRandom());

            Assert.That(EventFatalInjuryRules.TryPrepare(state, HunterBodyPart.Arms, 1, new FirstRandom(), out EventFatalInjuryPlan plan, out string reason), Is.True, reason);
            Assert.That(plan.RequiresDeathDraw, Is.True);
            Assert.That(deck.DeathCardCount, Is.EqualTo(1));

            HunterDamageResult result = plan.Commit(new FirstRandom(), null, 0);

            Assert.That(result.FatalInjuryTriggered, Is.True);
            Assert.That(result.DeathDraw.Value.Card, Is.EqualTo(DeathCardType.Death));
            Assert.That(result.IsDead, Is.True);
            Assert.That(deck.DeathCardCount, Is.EqualTo(1));
        }

        [Test]
        public void CancelBeforeCommitLeavesPreparedPlanStateUntouched()
        {
            var deck = new DeathDeck(new[] { DeathCardType.Survive });
            var state = new HunterInjuryState(HunterInjuryProfile.CreateDefault(), deck);
            state.ApplyDamage(HunterBodyPart.Legs, 3, new FirstRandom());

            Assert.That(EventFatalInjuryRules.TryPrepare(state, HunterBodyPart.Legs, 1, new FirstRandom(), out EventFatalInjuryPlan plan, out string reason), Is.True, reason);

            Assert.That(state.IsDead, Is.False);
            Assert.That(deck.DeathCardCount, Is.Zero);
            Assert.That(state.GetPart(HunterBodyPart.Legs).CurrentHealth, Is.Zero);
            Assert.That(plan.RequiresDeathDraw, Is.True);
        }

        [Test]
        public void NonFatalPartDamageDoesNotPrepareDeathDraw()
        {
            var state = new HunterInjuryState(HunterInjuryProfile.CreateDefault());

            Assert.That(EventFatalInjuryRules.TryPrepare(state, HunterBodyPart.Torso, 1, new FirstRandom(), out EventFatalInjuryPlan plan, out string reason), Is.True, reason);
            Assert.That(plan.RequiresDeathDraw, Is.False);

            HunterDamageResult result = plan.Commit(new FirstRandom(), null, 0);

            Assert.That(result.FatalInjuryTriggered, Is.False);
            Assert.That(result.HealthLost, Is.EqualTo(1));
            Assert.That(state.GetPart(HunterBodyPart.Torso).CurrentHealth, Is.EqualTo(3));
        }

        [Test]
        public void InvalidBodyPartFailsClosedWithoutTouchingState()
        {
            var state = new HunterInjuryState(HunterInjuryProfile.CreateDefault());

            Assert.That(EventFatalInjuryRules.TryPrepare(state, (HunterBodyPart)99, 1, new FirstRandom(), out _, out string reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(state.DeathDeck.Cards, Has.Count.EqualTo(1));
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
