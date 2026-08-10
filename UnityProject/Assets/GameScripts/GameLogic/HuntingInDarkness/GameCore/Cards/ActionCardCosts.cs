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

        public ActionCardCostDefinition(
            ActionCardCostKind kind,
            int amount,
            string requiredCardTag = null)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Kind = kind;
            Amount = amount;
            RequiredCardTag = requiredCardTag ?? string.Empty;
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
        private readonly Dictionary<int, int> _combatInspiration =
            new Dictionary<int, int>();

        public void Register(int ownerId, int initialCombatInspiration = 0) =>
            _combatInspiration[ownerId] = Math.Max(0, initialCombatInspiration);

        public int GetCombatInspiration(int ownerId) =>
            _combatInspiration.TryGetValue(ownerId, out int value) ? value : 0;

        public bool CanSpendCombatInspiration(int ownerId, int amount) =>
            amount >= 0 && GetCombatInspiration(ownerId) >= amount;

        public bool TrySpendCombatInspiration(int ownerId, int amount)
        {
            if (!CanSpendCombatInspiration(ownerId, amount))
                return false;
            _combatInspiration[ownerId] = GetCombatInspiration(ownerId) - amount;
            return true;
        }

        public int AddCombatInspiration(int ownerId, int amount)
        {
            int value = Math.Max(0, GetCombatInspiration(ownerId) + amount);
            _combatInspiration[ownerId] = value;
            return value;
        }
    }
}
