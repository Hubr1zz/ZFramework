using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunters;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests.EditMode
{
    public sealed class DeathDeckDrawTests
    {
        [Test]
        public void DrawOrder_ShufflesEveryIndexExactlyOnce()
        {
            var deck = new DeathDeck(new[] { DeathCardType.Survive, DeathCardType.Death, DeathCardType.Death, DeathCardType.Death });
            DeathDeckDrawOrder order = deck.PrepareDraw(new FirstRandom());
            var resolved = new HashSet<int>();

            for (int i = 0; i < order.Count; i++)
                resolved.Add(order.ResolveCardIndex(i));

            Assert.AreEqual(4, resolved.Count);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, resolved);
        }

        [Test]
        public void DrawOrder_ClampsInvalidViewPositions()
        {
            var deck = new DeathDeck(new[] { DeathCardType.Survive, DeathCardType.Death });
            DeathDeckDrawOrder order = deck.PrepareDraw(new FirstRandom());

            Assert.AreEqual(order.ResolveCardIndex(0), order.ResolveCardIndex(-99));
            Assert.AreEqual(order.ResolveCardIndex(1), order.ResolveCardIndex(99));
        }

        [Test]
        public void PreparedDraw_SurvivalAddsExactlyOneDeathCard()
        {
            var deck = new DeathDeck(new[] { DeathCardType.Survive, DeathCardType.Death });
            DeathDeckDrawOrder order = deck.PrepareDraw(new FirstRandom());

            DeathDrawResult result = deck.Draw(order, 1);

            Assert.IsTrue(result.Survived);
            Assert.IsTrue(result.DeathCardAdded);
            Assert.AreEqual(2, deck.DeathCardCount);
        }

        [Test]
        public void PreparedDraw_DeathDoesNotMutateDeck()
        {
            var deck = new DeathDeck(new[] { DeathCardType.Survive, DeathCardType.Death });
            DeathDeckDrawOrder order = deck.PrepareDraw(new FirstRandom());

            DeathDrawResult result = deck.Draw(order, 0);

            Assert.IsFalse(result.Survived);
            Assert.IsFalse(result.DeathCardAdded);
            Assert.AreEqual(2, deck.Cards.Count);
        }

        [Test]
        public void PreparedDraw_RejectsStaleOrderAfterDeckChanges()
        {
            var deck = new DeathDeck(new[] { DeathCardType.Survive });
            DeathDeckDrawOrder order = deck.PrepareDraw(new FirstRandom());
            deck.Draw(order, 0);

            Assert.Throws<InvalidOperationException>(() => deck.Draw(order, 0));
        }

        [Test]
        public void PrepareDraw_RejectsMissingRandom()
        {
            var deck = new DeathDeck();

            Assert.Throws<ArgumentNullException>(() => deck.PrepareDraw(null));
        }

        [Test]
        public void PreparedDraw_RejectsOrderFromAnotherDeck()
        {
            var firstDeck = new DeathDeck(new[] { DeathCardType.Survive, DeathCardType.Death });
            var secondDeck = new DeathDeck(new[] { DeathCardType.Survive, DeathCardType.Death });
            DeathDeckDrawOrder order = firstDeck.PrepareDraw(new FirstRandom());

            Assert.Throws<InvalidOperationException>(() => secondDeck.Draw(order, 0));
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
