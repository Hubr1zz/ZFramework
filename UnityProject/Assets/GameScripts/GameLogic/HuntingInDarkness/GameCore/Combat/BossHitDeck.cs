using System;

namespace HuntingInDarkness.GameCore.Combat
{
    public enum BossHitResultCard
    {
        Hit,
        Dodge
    }

    public readonly struct BossHitDeckComposition
    {
        public int HitCards { get; }
        public int DodgeCards { get; }
        public int TotalCards => HitCards + DodgeCards;
        public bool IsAutomaticHit => DodgeCards == 0;

        public BossHitDeckComposition(int hitCards, int dodgeCards)
        {
            HitCards = Math.Max(1, hitCards);
            DodgeCards = Math.Max(0, Math.Min(dodgeCards, int.MaxValue - HitCards));
        }
    }

    public readonly struct BossHitDeckDraw
    {
        public BossHitDeckComposition Composition { get; }
        public int DrawIndex { get; }
        public BossHitResultCard Card { get; }
        public bool IsHit => Card == BossHitResultCard.Hit;

        public BossHitDeckDraw(BossHitDeckComposition composition, int drawIndex, BossHitResultCard card)
        {
            Composition = composition;
            DrawIndex = drawIndex;
            Card = card;
        }
    }

    public static class BossHitDeckRules
    {
        public static BossHitDeckComposition Build(int accuracy, int agility) => new(Math.Max(1, accuracy), Math.Max(0, agility));

        public static BossHitDeckDraw ResolveDraw(BossHitDeckComposition composition, int drawIndex)
        {
            if (composition.TotalCards <= 0)
                composition = new BossHitDeckComposition(1, 0);

            int safeIndex = Math.Max(0, Math.Min(drawIndex, composition.TotalCards - 1));
            BossHitResultCard card = safeIndex < composition.HitCards ? BossHitResultCard.Hit : BossHitResultCard.Dodge;
            return new BossHitDeckDraw(composition, safeIndex, card);
        }
    }
}
