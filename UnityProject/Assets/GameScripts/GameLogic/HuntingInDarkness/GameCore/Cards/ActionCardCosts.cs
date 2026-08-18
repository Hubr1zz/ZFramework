using System;
using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Cards
{
    public enum ActionCardCostKind
    {
        TimePoint,
        CombatInspiration,
        Willpower,
        FlipOtherCard
    }

    public sealed class ActionCardCostDefinition
    {
        public ActionCardCostKind Kind { get; }
        public int Amount { get; }
        public string RequiredCardTag { get; }
        public InspirationRequirement InspirationRequirement { get; }

        public ActionCardCostDefinition(
            ActionCardCostKind kind,
            int amount,
            string requiredCardTag = null,
            InspirationRequirement inspirationRequirement = InspirationRequirement.Any)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Kind = kind;
            Amount = amount;
            RequiredCardTag = requiredCardTag ?? string.Empty;
            InspirationRequirement = inspirationRequirement;
        }
    }

    public sealed class ActionCardDefinition
    {
        private readonly ActionCardCostDefinition[] _costs;

        public string Id { get; }
        public IReadOnlyList<ActionCardCostDefinition> Costs => _costs;
        public bool ResetsEachTurn { get; }
        public bool AllowsBurst { get; }
        public bool IsWillAction { get; }

        public ActionCardDefinition(
            string id,
            IEnumerable<ActionCardCostDefinition> costs,
            bool resetsEachTurn,
            bool allowsBurst)
        {
            Id = id ?? string.Empty;
            _costs = costs == null
                ? Array.Empty<ActionCardCostDefinition>()
                : new List<ActionCardCostDefinition>(costs).ToArray();
            IsWillAction = Array.Exists(
                _costs,
                cost => cost.Kind == ActionCardCostKind.Willpower);
            ResetsEachTurn = resetsEachTurn || IsWillAction;
            AllowsBurst = allowsBurst && !IsWillAction;
        }
    }

    public sealed class PreparedActionCardCost
    {
        private readonly int[] _selectedCardIds;

        public ActionCardCostDefinition Definition { get; }
        public IReadOnlyList<int> SelectedCardIds => _selectedCardIds;
        public IReadOnlyList<int> SelectedResourceIds => _selectedCardIds;

        public PreparedActionCardCost(
            ActionCardCostDefinition definition,
            IEnumerable<int> selectedCardIds = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _selectedCardIds = selectedCardIds == null
                ? Array.Empty<int>()
                : new List<int>(selectedCardIds).ToArray();
        }
    }

    public interface IActionCardCostGateway
    {
        bool CanPay(int ownerId, IReadOnlyList<PreparedActionCardCost> costs);
        void Commit(int ownerId, IReadOnlyList<PreparedActionCardCost> costs);
    }

    /// <summary>
    /// A prepared payment is committed only after the gateway validates the whole set.
    /// Commit is deliberately void: a gateway must not partially mutate state and then fail.
    /// </summary>
    public sealed class ActionCardCostTransaction
    {
        private readonly PreparedActionCardCost[] _costs;

        public IReadOnlyList<PreparedActionCardCost> Costs => _costs;

        public ActionCardCostTransaction(IEnumerable<PreparedActionCardCost> costs)
        {
            _costs = costs == null
                ? Array.Empty<PreparedActionCardCost>()
                : new List<PreparedActionCardCost>(costs).ToArray();
        }

        public bool TryCommit(int ownerId, IActionCardCostGateway gateway)
        {
            if (gateway == null) throw new ArgumentNullException(nameof(gateway));
            if (!gateway.CanPay(ownerId, _costs))
                return false;

            gateway.Commit(ownerId, _costs);
            return true;
        }
    }

    public sealed class ActionCardResourcePool
    {
        private readonly Dictionary<int, CombatInspirationMind> minds = new();

        public void Register(int ownerId, int initialCombatInspiration = 0, int capacity = CombatInspirationMind.DefaultCapacity)
        {
            var mind = new CombatInspirationMind(capacity);
            int initialCount = Math.Min(Math.Max(0, initialCombatInspiration), mind.Capacity);
            for (int index = 0; index < initialCount; index++)
                mind.TryAdd((CombatInspirationColor)(index % 3));
            minds[ownerId] = mind;
        }

        public int GetCombatInspiration(int ownerId) =>
            GetMind(ownerId)?.Tokens.Count ?? 0;

        public int GetCapacity(int ownerId) => GetMind(ownerId)?.Capacity ?? 0;

        public IReadOnlyList<CombatInspirationToken> GetTokens(int ownerId) =>
            GetMind(ownerId)?.Tokens ?? Array.Empty<CombatInspirationToken>();

        public List<CombatInspirationToken> GetSpendableTokens(int ownerId, InspirationRequirement requirement, ISet<int> excludedTokenIds = null) =>
            GetMind(ownerId)?.GetSpendable(requirement, excludedTokenIds) ?? new List<CombatInspirationToken>();

        public bool CanSpendCombatInspiration(int ownerId, int amount) =>
            amount >= 0 && GetCombatInspiration(ownerId) >= amount;

        public bool CanSpendCombatInspiration(int ownerId, IReadOnlyList<int> tokenIds, InspirationRequirement requirement, int amount) =>
            GetMind(ownerId)?.CanSpend(tokenIds, requirement, amount) == true;

        public bool TrySpendCombatInspiration(int ownerId, int amount)
        {
            if (!CanSpendCombatInspiration(ownerId, amount))
                return false;
            var tokenIds = new List<int>(amount);
            IReadOnlyList<CombatInspirationToken> tokens = GetTokens(ownerId);
            for (int index = 0; index < amount; index++)
                tokenIds.Add(tokens[index].Id);
            return TrySpendCombatInspiration(ownerId, tokenIds);
        }

        public bool TrySpendCombatInspiration(int ownerId, IReadOnlyList<int> tokenIds) =>
            GetMind(ownerId)?.TrySpend(tokenIds) == true;

        public int AddCombatInspiration(int ownerId, int amount)
        {
            CombatInspirationMind mind = GetMind(ownerId);
            if (mind == null || amount == 0) return GetCombatInspiration(ownerId);

            if (amount < 0)
            {
                int spendCount = Math.Min(-amount, mind.Tokens.Count);
                var tokenIds = new List<int>(spendCount);
                for (int index = 0; index < spendCount; index++)
                    tokenIds.Add(mind.Tokens[index].Id);
                mind.TrySpend(tokenIds);
                return mind.Tokens.Count;
            }

            for (int index = 0; index < amount && mind.Tokens.Count < mind.Capacity; index++)
                mind.TryAdd((CombatInspirationColor)(index % 3));
            return mind.Tokens.Count;
        }

        public InspirationGain TryAddCombatInspiration(int ownerId, CombatInspirationColor color, int replaceTokenId = -1)
        {
            CombatInspirationMind mind = GetMind(ownerId);
            return mind == null
                ? new InspirationGain(InspirationGainResult.Rejected, default)
                : mind.TryAdd(color, replaceTokenId);
        }

        public bool CanPayCosts(int ownerId, IReadOnlyList<ActionCardCostDefinition> costs)
        {
            CombatInspirationMind mind = GetMind(ownerId);
            if (mind == null) return false;

            int red = 0;
            int blue = 0;
            int yellow = 0;
            foreach (CombatInspirationToken token in mind.Tokens)
            {
                if (token.Color == CombatInspirationColor.Red) red++;
                else if (token.Color == CombatInspirationColor.Blue) blue++;
                else if (token.Color == CombatInspirationColor.Yellow) yellow++;
            }

            int requiredRed = 0;
            int requiredBlue = 0;
            int requiredYellow = 0;
            int requiredAny = 0;
            foreach (ActionCardCostDefinition cost in costs)
            {
                if (cost.Kind != ActionCardCostKind.CombatInspiration) continue;
                if (cost.InspirationRequirement == InspirationRequirement.Red) requiredRed += cost.Amount;
                else if (cost.InspirationRequirement == InspirationRequirement.Blue) requiredBlue += cost.Amount;
                else if (cost.InspirationRequirement == InspirationRequirement.Yellow) requiredYellow += cost.Amount;
                else requiredAny += cost.Amount;
            }

            if (requiredRed > red || requiredBlue > blue || requiredYellow > yellow) return false;
            return red + blue + yellow - requiredRed - requiredBlue - requiredYellow >= requiredAny;
        }

        private CombatInspirationMind GetMind(int ownerId) =>
            minds.TryGetValue(ownerId, out CombatInspirationMind mind) ? mind : null;
    }
}
