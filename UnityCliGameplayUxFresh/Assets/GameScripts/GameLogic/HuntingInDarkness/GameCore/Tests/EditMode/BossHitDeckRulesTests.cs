using HuntingInDarkness.GameCore.Combat;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests.EditMode
{
    public class BossHitDeckRulesTests
    {
        [Test]
        public void Build_UsesAccuracyForHitsAndAgilityForDodges()
        {
            BossHitDeckComposition deck = BossHitDeckRules.Build(3, 2);

            Assert.AreEqual(3, deck.HitCards);
            Assert.AreEqual(2, deck.DodgeCards);
            Assert.AreEqual(5, deck.TotalCards);
        }

        [Test]
        public void Build_ZeroAgility_IsAutomaticHit()
        {
            BossHitDeckComposition deck = BossHitDeckRules.Build(2, 0);

            Assert.IsTrue(deck.IsAutomaticHit);
            Assert.Zero(deck.DodgeCards);
        }

        [Test]
        public void Build_AlwaysContainsAtLeastOneHitCard()
        {
            BossHitDeckComposition deck = BossHitDeckRules.Build(0, 2);

            Assert.AreEqual(1, deck.HitCards);
            Assert.AreEqual(3, deck.TotalCards);
        }

        [TestCase(0, BossHitResultCard.Hit)]
        [TestCase(1, BossHitResultCard.Hit)]
        [TestCase(2, BossHitResultCard.Dodge)]
        [TestCase(3, BossHitResultCard.Dodge)]
        public void ResolveDraw_UsesCompositionBoundary(int drawIndex, BossHitResultCard expected)
        {
            var deck = new BossHitDeckComposition(2, 2);

            Assert.AreEqual(expected, BossHitDeckRules.ResolveDraw(deck, drawIndex).Card);
        }

        [Test]
        public void ResolveDraw_DefaultComposition_FallsBackToHit()
        {
            BossHitDeckDraw draw = BossHitDeckRules.ResolveDraw(default, 0);

            Assert.AreEqual(1, draw.Composition.TotalCards);
            Assert.IsTrue(draw.IsHit);
        }

        [Test]
        public void Composition_ExtremeValues_DoNotOverflow()
        {
            var deck = new BossHitDeckComposition(int.MaxValue, int.MaxValue);

            Assert.AreEqual(int.MaxValue, deck.TotalCards);
            Assert.Zero(deck.DodgeCards);
        }

        [TestCase(-1, 0, BossHitResultCard.Hit)]
        [TestCase(99, 3, BossHitResultCard.Dodge)]
        public void ResolveDraw_ClampsInvalidIndexes(int drawIndex, int expectedIndex, BossHitResultCard expectedCard)
        {
            var deck = new BossHitDeckComposition(2, 2);

            BossHitDeckDraw draw = BossHitDeckRules.ResolveDraw(deck, drawIndex);

            Assert.AreEqual(expectedIndex, draw.DrawIndex);
            Assert.AreEqual(expectedCard, draw.Card);
        }
    }
}
