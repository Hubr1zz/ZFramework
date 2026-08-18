using System;
using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Cards
{
    public enum CombatInspirationColor
    {
        Red,
        Blue,
        Yellow
    }

    public enum InspirationRequirement
    {
        Any,
        Red,
        Blue,
        Yellow
    }

    public enum InspirationGainResult
    {
        Added,
        Replaced,
        RequiresReplacement,
        Discarded,
        Rejected
    }

    public readonly struct CombatInspirationToken
    {
        public int Id { get; }
        public CombatInspirationColor Color { get; }

        public CombatInspirationToken(int id, CombatInspirationColor color)
        {
            Id = id;
            Color = color;
        }
    }

    public readonly struct InspirationGain
    {
        public InspirationGainResult Result { get; }
        public CombatInspirationToken Token { get; }
        public int ReplacedTokenId { get; }

        public InspirationGain(InspirationGainResult result, CombatInspirationToken token, int replacedTokenId = -1)
        {
            Result = result;
            Token = token;
            ReplacedTokenId = replacedTokenId;
        }
    }

    public sealed class CombatInspirationMind
    {
        public const int DefaultCapacity = 4;

        private readonly List<CombatInspirationToken> tokens = new();
        private int nextTokenId = 1;

        public int Capacity { get; }
        public IReadOnlyList<CombatInspirationToken> Tokens => tokens;

        public CombatInspirationMind(int capacity = DefaultCapacity)
        {
            Capacity = Math.Max(1, capacity);
        }

        public InspirationGain TryAdd(CombatInspirationColor color, int replaceTokenId = -1)
        {
            if (!Enum.IsDefined(typeof(CombatInspirationColor), color))
                return new InspirationGain(InspirationGainResult.Rejected, default);

            if (tokens.Count < Capacity)
            {
                CombatInspirationToken added = CreateToken(color);
                tokens.Add(added);
                return new InspirationGain(InspirationGainResult.Added, added);
            }

            int replaceIndex = tokens.FindIndex(token => token.Id == replaceTokenId);
            if (replaceIndex < 0)
                return new InspirationGain(InspirationGainResult.RequiresReplacement, default);

            CombatInspirationToken replacement = CreateToken(color);
            tokens[replaceIndex] = replacement;
            return new InspirationGain(InspirationGainResult.Replaced, replacement, replaceTokenId);
        }

        public List<CombatInspirationToken> GetSpendable(InspirationRequirement requirement, ISet<int> excludedTokenIds = null)
        {
            var result = new List<CombatInspirationToken>();
            foreach (CombatInspirationToken token in tokens)
            {
                if (excludedTokenIds != null && excludedTokenIds.Contains(token.Id)) continue;
                if (Matches(token.Color, requirement)) result.Add(token);
            }
            return result;
        }

        public bool CanSpend(IReadOnlyList<int> tokenIds, InspirationRequirement requirement, int amount)
        {
            if (tokenIds == null || tokenIds.Count != amount || amount < 0) return false;

            var uniqueIds = new HashSet<int>();
            foreach (int tokenId in tokenIds)
            {
                if (!uniqueIds.Add(tokenId)) return false;
                CombatInspirationToken token = tokens.Find(candidate => candidate.Id == tokenId);
                if (token.Id <= 0 || !Matches(token.Color, requirement)) return false;
            }
            return true;
        }

        public bool TrySpend(IReadOnlyList<int> tokenIds)
        {
            if (tokenIds == null) return false;

            var uniqueIds = new HashSet<int>(tokenIds);
            if (uniqueIds.Count != tokenIds.Count) return false;
            foreach (int tokenId in uniqueIds)
                if (!tokens.Exists(token => token.Id == tokenId))
                    return false;

            tokens.RemoveAll(token => uniqueIds.Contains(token.Id));
            return true;
        }

        private CombatInspirationToken CreateToken(CombatInspirationColor color) => new(nextTokenId++, color);

        private static bool Matches(CombatInspirationColor color, InspirationRequirement requirement)
        {
            return requirement == InspirationRequirement.Any ||
                   requirement == InspirationRequirement.Red && color == CombatInspirationColor.Red ||
                   requirement == InspirationRequirement.Blue && color == CombatInspirationColor.Blue ||
                   requirement == InspirationRequirement.Yellow && color == CombatInspirationColor.Yellow;
        }
    }

    public static class FocusInspirationRules
    {
        public const int OutcomeCount = 9;

        public static (CombatInspirationColor first, CombatInspirationColor second) ResolveRoll(int roll)
        {
            int normalized = Math.Clamp(roll, 0, OutcomeCount - 1);
            return ((CombatInspirationColor)(normalized / 3), (CombatInspirationColor)(normalized % 3));
        }
    }
}
