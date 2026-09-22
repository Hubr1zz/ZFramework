using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Foundation;

namespace HuntingInDarkness.GameCore.Combat
{
    public static class CombatRules
    {
        public static int CalculateAttackPower(CombatantStats attacker, WeaponProfile weapon) =>
            (attacker?.Strength ?? 0) + (weapon?.StrengthBonus ?? 0);

        public static AttackCheck ResolveHitLocationAttack(int attackPower, int toughness)
        {
            int threshold = Math.Max(0, toughness);
            bool success = attackPower >= threshold;
            bool critical = success && attackPower >= threshold * 2;
            return new AttackCheck(
                attackPower,
                success ? AttackOutcome.Success : AttackOutcome.Failure,
                critical);
        }

        public static bool IsBossAttackDodged(int roll, CombatantStats defender) =>
            roll < (defender?.Evasion ?? 0);

    }

    public static class WeightedSelection
    {
        public static List<T> DrawWithoutReplacement<T>(
            IReadOnlyList<T> source,
            int count,
            Func<T, int> getWeight,
            IRandomSource random)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (getWeight == null) throw new ArgumentNullException(nameof(getWeight));
            if (random == null) throw new ArgumentNullException(nameof(random));

            var result = new List<T>();
            var remaining = new List<T>(source);
            int requested = Math.Max(0, Math.Min(count, remaining.Count));

            for (int i = 0; i < requested; i++)
            {
                int totalWeight = 0;
                foreach (T item in remaining)
                    totalWeight += Math.Max(0, getWeight(item));

                if (totalWeight <= 0)
                    break;

                int roll = random.Next(0, totalWeight);
                int cumulative = 0;
                for (int index = 0; index < remaining.Count; index++)
                {
                    cumulative += Math.Max(0, getWeight(remaining[index]));
                    if (roll >= cumulative)
                        continue;

                    result.Add(remaining[index]);
                    remaining.RemoveAt(index);
                    break;
                }
            }

            return result;
        }
    }
}
