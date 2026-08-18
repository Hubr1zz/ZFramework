using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Foundation;

namespace HuntingInDarkness.GameCore.Hunters
{
    /// <summary>只暴露背面位置到牌堆索引的洗牌映射，不向 View 泄漏牌面。</summary>
    public sealed class DeathDeckDrawOrder
    {
        private readonly DeathDeck owner;
        private readonly IReadOnlyList<int> cardIndices;

        public int Count => cardIndices.Count;

        private DeathDeckDrawOrder(DeathDeck owner, IReadOnlyList<int> cardIndices)
        {
            this.owner = owner;
            this.cardIndices = cardIndices;
        }

        internal static DeathDeckDrawOrder Create(DeathDeck owner, int cardCount, IRandomSource random)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (cardCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(cardCount));
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            var indices = new List<int>(cardCount);
            for (int i = 0; i < cardCount; i++)
                indices.Add(i);
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(0, i + 1);
                (indices[i], indices[swapIndex]) = (indices[swapIndex], indices[i]);
            }
            return new DeathDeckDrawOrder(owner, indices.AsReadOnly());
        }

        internal bool IsFor(DeathDeck deck) => ReferenceEquals(owner, deck);

        public int ResolveCardIndex(int facedownPosition)
        {
            int safePosition = Math.Max(0, Math.Min(facedownPosition, cardIndices.Count - 1));
            return cardIndices[safePosition];
        }
    }
}
