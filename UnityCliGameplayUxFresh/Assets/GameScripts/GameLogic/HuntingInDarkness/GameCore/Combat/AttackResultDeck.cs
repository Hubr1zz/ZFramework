using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Foundation;

namespace HuntingInDarkness.GameCore.Combat
{
    public enum AttackResultCard
    {
        Success,
        Failure
    }

    public readonly struct AttackResultDeckComposition
    {
        public int SuccessCards { get; }
        public int FailureCards { get; }
        public int TotalCards => SuccessCards + FailureCards;
        public bool IsCertainSuccess => FailureCards == 0;

        public AttackResultDeckComposition(int successCards, int failureCards)
        {
            SuccessCards = Math.Max(1, successCards);
            FailureCards = Math.Max(0, Math.Min(failureCards, int.MaxValue - SuccessCards));
        }
    }

    public readonly struct AttackResultDeckDraw
    {
        public AttackResultDeckComposition Composition { get; }
        public int DrawIndex { get; }
        public AttackResultCard Card { get; }
        public bool IsSuccess => Card == AttackResultCard.Success;

        public AttackResultDeckDraw(AttackResultDeckComposition composition, int drawIndex, AttackResultCard card)
        {
            Composition = composition;
            DrawIndex = drawIndex;
            Card = card;
        }
    }

    public static class AttackResultDeckRules
    {
        public static AttackResultDeckComposition Build(int strength, int weaponPower, int toughness)
        {
            int successCards = Math.Max(1, strength);
            int effectiveToughness = Math.Max(0, toughness - Math.Max(0, weaponPower));
            return new AttackResultDeckComposition(successCards, effectiveToughness);
        }

        public static AttackResultDeckDraw ResolveDraw(AttackResultDeckComposition composition, int drawIndex)
        {
            if (composition.TotalCards <= 0)
                composition = new AttackResultDeckComposition(1, 0);

            int safeIndex = Math.Max(0, Math.Min(drawIndex, composition.TotalCards - 1));
            AttackResultCard card = safeIndex < composition.SuccessCards ? AttackResultCard.Success : AttackResultCard.Failure;
            return new AttackResultDeckDraw(composition, safeIndex, card);
        }

        public static List<AttackResultCard> DrawBatch(AttackResultDeckComposition composition, int drawCount, IRandomSource random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (composition.TotalCards <= 0)
                composition = new AttackResultDeckComposition(1, 0);

            var results = new List<AttackResultCard>(Math.Max(0, drawCount));
            int remainingSuccess = composition.SuccessCards;
            int remainingFailure = composition.FailureCards;
            for (int index = 0; index < drawCount; index++)
            {
                if (remainingSuccess + remainingFailure <= 0)
                {
                    remainingSuccess = composition.SuccessCards;
                    remainingFailure = composition.FailureCards;
                }

                int roll = random.Next(0, remainingSuccess + remainingFailure);
                if (roll < remainingSuccess)
                {
                    results.Add(AttackResultCard.Success);
                    remainingSuccess--;
                }
                else
                {
                    results.Add(AttackResultCard.Failure);
                    remainingFailure--;
                }
            }
            return results;
        }
    }
}
