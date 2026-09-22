using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Foundation;

namespace HuntingInDarkness.GameCore.Hunters
{
    public enum DeathCardType
    {
        Survive,
        Death,
        SurvivalEvent
    }

    public readonly struct DeathDeckCard
    {
        public DeathCardType Type { get; }
        public string SurvivalEventId { get; }

        public DeathDeckCard(DeathCardType type, string survivalEventId = "")
        {
            Type = type;
            SurvivalEventId = type == DeathCardType.SurvivalEvent ? survivalEventId ?? string.Empty : string.Empty;
        }
    }

    public readonly struct DeathDrawResult
    {
        public DeathCardType Card { get; }
        public bool Survived => Card != DeathCardType.Death;
        public bool DeathCardAdded { get; }
        public string SurvivalEventId { get; }

        public DeathDrawResult(DeathCardType card, bool deathCardAdded, string survivalEventId = "")
        {
            Card = card;
            DeathCardAdded = deathCardAdded;
            SurvivalEventId = survivalEventId ?? string.Empty;
        }
    }

    /// <summary>
    /// Visible persistent deck: cards remain in the deck after drawing, and every survival adds
    /// one death card so later fatal injuries become progressively more dangerous.
    /// </summary>
    public sealed class DeathDeck
    {
        private readonly List<DeathDeckCard> _cards;
        private readonly List<DeathCardType> _cardTypes;
        private readonly IReadOnlyList<DeathCardType> _visibleCards;

        public IReadOnlyList<DeathCardType> Cards => _visibleCards;
        public int OrdinarySurvivalCardCount => _cards.FindAll(card => card.Type == DeathCardType.Survive).Count;
        public int SurvivalEventCardCount => _cards.FindAll(card => card.Type == DeathCardType.SurvivalEvent).Count;
        public int SurvivalCardCount => OrdinarySurvivalCardCount + SurvivalEventCardCount;
        public int DeathCardCount => _cards.FindAll(card => card.Type == DeathCardType.Death).Count;
        public IReadOnlyList<string> SurvivalEventIds
        {
            get
            {
                var result = new List<string>();
                foreach (DeathDeckCard card in _cards)
                    if (card.Type == DeathCardType.SurvivalEvent && !string.IsNullOrWhiteSpace(card.SurvivalEventId))
                        result.Add(card.SurvivalEventId);
                return result;
            }
        }

        public DeathDeck() : this(new[] { DeathCardType.Survive }) { }

        public DeathDeck(IEnumerable<DeathCardType> cards)
        {
            if (cards == null)
                throw new ArgumentNullException(nameof(cards));
            _cards = new List<DeathDeckCard>();
            foreach (DeathCardType card in cards)
                _cards.Add(new DeathDeckCard(card));
            if (_cards.Count == 0)
                throw new ArgumentException("A death deck must contain at least one card.", nameof(cards));
            _cardTypes = _cards.ConvertAll(card => card.Type);
            _visibleCards = _cardTypes.AsReadOnly();
        }

        public DeathDeck(IEnumerable<DeathDeckCard> cards)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            _cards = new List<DeathDeckCard>(cards);
            if (_cards.Count == 0) throw new ArgumentException("A death deck must contain at least one card.", nameof(cards));
            _cardTypes = _cards.ConvertAll(card => card.Type);
            _visibleCards = _cardTypes.AsReadOnly();
        }

        public DeathDrawResult Draw(IRandomSource random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            return ResolveCardAt(random.Next(0, _cards.Count));
        }

        public DeathDeckDrawOrder PrepareDraw(IRandomSource random) => DeathDeckDrawOrder.Create(this, _cards.Count, random);

        public DeathDrawResult Draw(DeathDeckDrawOrder drawOrder, int facedownPosition)
        {
            if (drawOrder == null)
                throw new ArgumentNullException(nameof(drawOrder));
            if (!drawOrder.IsFor(this) || drawOrder.Count != _cards.Count)
                throw new InvalidOperationException("The death deck changed after it was shuffled.");
            return ResolveCardAt(drawOrder.ResolveCardIndex(facedownPosition));
        }

        private DeathDrawResult ResolveCardAt(int index)
        {
            index = Math.Max(0, Math.Min(index, _cards.Count - 1));
            DeathDeckCard card = _cards[index];
            bool addedDeathCard = card.Type != DeathCardType.Death;
            if (addedDeathCard)
            {
                _cards.Add(new DeathDeckCard(DeathCardType.Death));
                _cardTypes.Add(DeathCardType.Death);
            }
            return new DeathDrawResult(card.Type, addedDeathCard, card.SurvivalEventId);
        }
    }
}
