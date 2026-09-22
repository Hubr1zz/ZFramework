using HuntingInDarkness.GameCore.Combat;
using NUnit.Framework;
using HuntingInDarkness.GameCore.Foundation;

namespace HuntingInDarkness.GameCore.Tests.EditMode
{
    public class AttackResultDeckRulesTests
    {
        [Test]
        public void Build_UsesStrengthForSuccessAndRemainingToughnessForFailure()
        {
            AttackResultDeckComposition deck = AttackResultDeckRules.Build(2, 1, 3);

            Assert.AreEqual(2, deck.SuccessCards);
            Assert.AreEqual(2, deck.FailureCards);
            Assert.AreEqual(4, deck.TotalCards);
        }

        [Test]
        public void Build_AlwaysContainsAtLeastOneSuccessCard()
        {
            AttackResultDeckComposition deck = AttackResultDeckRules.Build(0, 0, 2);

            Assert.AreEqual(1, deck.SuccessCards);
            Assert.AreEqual(2, deck.FailureCards);
        }

        [Test]
        public void Build_WhenWeaponPowerMeetsToughness_IsCertainSuccess()
        {
            AttackResultDeckComposition deck = AttackResultDeckRules.Build(1, 4, 3);

            Assert.Zero(deck.FailureCards);
            Assert.IsTrue(deck.IsCertainSuccess);
        }

        [TestCase(0, AttackResultCard.Success)]
        [TestCase(1, AttackResultCard.Success)]
        [TestCase(2, AttackResultCard.Failure)]
        [TestCase(3, AttackResultCard.Failure)]
        public void ResolveDraw_UsesCompositionBoundary(int drawIndex, AttackResultCard expected)
        {
            var deck = new AttackResultDeckComposition(2, 2);

            Assert.AreEqual(expected, AttackResultDeckRules.ResolveDraw(deck, drawIndex).Card);
        }

        [Test]
        public void Build_ClampsNegativeInputs()
        {
            AttackResultDeckComposition deck = AttackResultDeckRules.Build(-2, -3, -4);

            Assert.AreEqual(1, deck.TotalCards);
            Assert.IsTrue(deck.IsCertainSuccess);
        }

        [TestCase(-1, 0, AttackResultCard.Success)]
        [TestCase(99, 3, AttackResultCard.Failure)]
        public void ResolveDraw_ClampsInvalidIndexes(int drawIndex, int expectedIndex, AttackResultCard expectedCard)
        {
            var deck = new AttackResultDeckComposition(2, 2);

            AttackResultDeckDraw draw = AttackResultDeckRules.ResolveDraw(deck, drawIndex);

            Assert.AreEqual(expectedIndex, draw.DrawIndex);
            Assert.AreEqual(expectedCard, draw.Card);
        }

        [Test]
        public void ResolveDraw_DefaultComposition_FallsBackToCertainSuccess()
        {
            AttackResultDeckDraw draw = AttackResultDeckRules.ResolveDraw(default, 0);

            Assert.AreEqual(1, draw.Composition.TotalCards);
            Assert.IsTrue(draw.IsSuccess);
        }

        [Test]
        public void Composition_ExtremeValues_DoNotOverflowTotalCards()
        {
            var deck = new AttackResultDeckComposition(int.MaxValue, int.MaxValue);

            Assert.AreEqual(int.MaxValue, deck.TotalCards);
            Assert.Zero(deck.FailureCards);
        }

        [Test]
        public void DrawBatch_DrawsWithoutReplacementUntilDeckIsEmpty()
        {
            var deck = new AttackResultDeckComposition(2, 1);

            var results = AttackResultDeckRules.DrawBatch(deck, 4, new FirstIndexRandom());

            Assert.That(results, Is.EqualTo(new[] { AttackResultCard.Success, AttackResultCard.Success, AttackResultCard.Failure, AttackResultCard.Success }));
        }

        [Test]
        public void DrawBatch_CertainSuccess_RemainsPlayableBeyondDeckSize()
        {
            var deck = new AttackResultDeckComposition(1, 0);

            var results = AttackResultDeckRules.DrawBatch(deck, 3, new FirstIndexRandom());

            Assert.That(results, Is.EqualTo(new[] { AttackResultCard.Success, AttackResultCard.Success, AttackResultCard.Success }));
        }

        [Test]
        public void DrawBatch_DefaultComposition_FallsBackToSuccess()
        {
            var results = AttackResultDeckRules.DrawBatch(default, 2, new FirstIndexRandom());

            Assert.That(results, Is.EqualTo(new[] { AttackResultCard.Success, AttackResultCard.Success }));
        }

        private sealed class FirstIndexRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
