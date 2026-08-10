using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Foundation;

namespace HuntingInDarkness.GameCore.Hunters
{
    public enum DeathCardType
    {
        Survive,
        Death
    }

    public readonly struct DeathDrawResult
    {
        public DeathCardType Card { get; }
        public bool Survived => Card == DeathCardType.Survive;
        public bool DeathCardAdded { get; }

        public DeathDrawResult(DeathCardType card, bool deathCardAdded)
        {
            Card = card;
            DeathCardAdded = deathCardAdded;
        }
    }

    /// <summary>
    /// Visible persistent deck: cards remain in the deck after drawing, and every survival adds
    /// one death card so later fatal injuries become progressively more dangerous.
    /// </summary>
    public sealed class DeathDeck
    {
        private readonly List<DeathCardType> _cards;
        private readonly IReadOnlyList<DeathCardType> _visibleCards;

        public IReadOnlyList<DeathCardType> Cards => _visibleCards;
        public int SurvivalCardCount => _cards.FindAll(card => card == DeathCardType.Survive).Count;
        public int DeathCardCount => _cards.Count - SurvivalCardCount;

        public DeathDeck() : this(new[] { DeathCardType.Survive }) { }

        public DeathDeck(IEnumerable<DeathCardType> cards)
        {
            if (cards == null)
                throw new ArgumentNullException(nameof(cards));
            _cards = new List<DeathCardType>(cards);
            if (_cards.Count == 0)
                throw new ArgumentException("A death deck must contain at least one card.", nameof(cards));
            _visibleCards = _cards.AsReadOnly();
        }

        public DeathDrawResult Draw(IRandomSource random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            int index = random.Next(0, _cards.Count);
            DeathCardType card = _cards[index];
            bool addedDeathCard = card == DeathCardType.Survive;
            if (addedDeathCard)
                _cards.Add(DeathCardType.Death);
            return new DeathDrawResult(card, addedDeathCard);
        }
    }
}
