using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameplayBase.CombatSystem;
using HuntingInDarkness.GameCore.Cards;

namespace Core
{
    public interface IAsyncActionQueueAction : IActionQueueAction
    {
        UniTask<ActionQueueActionResult> ExecuteAsync(ActionQueue queue);
    }

    public sealed class DelegateActionQueueAction : IAsyncActionQueueAction
    {
        private readonly Func<ActionQueue, UniTask<ActionQueueActionResult>> _execute;

        public string Name { get; }

        public DelegateActionQueueAction(
            string name,
            Func<ActionQueue, UniTask<ActionQueueActionResult>> execute)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "action" : name;
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public UniTask<ActionQueueActionResult> ExecuteAsync(ActionQueue queue) =>
            _execute(queue);
    }

    /// <summary>Adapter runner that owns asynchronous waits while GameCore owns queue state.</summary>
    public sealed class ActionQueueRunner
    {
        public async UniTask<ActionQueueStatus> RunAsync(ActionQueue queue)
        {
            if (queue == null) throw new ArgumentNullException(nameof(queue));
            queue.Start();

            while (queue.TryBeginNext(out IActionQueueAction action))
            {
                if (!(action is IAsyncActionQueueAction asyncAction))
                {
                    queue.CompleteCurrent(ActionQueueActionResult.Failed);
                    break;
                }

                queue.Pause();
                ActionQueueActionResult result;
                try
                {
                    result = await asyncAction.ExecuteAsync(queue);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                    result = ActionQueueActionResult.Failed;
                }

                if (queue.Status == ActionQueueStatus.Paused)
                    queue.Resume();
                if (queue.Status == ActionQueueStatus.Running)
                    queue.CompleteCurrent(result);
            }

            return queue.Status;
        }
    }

    /// <summary>
    /// Maps prepared card costs to timeline, combat-resource and card-state adapters.
    /// All mutable costs are revalidated as one set immediately before commit.
    /// </summary>
    public sealed class ActionCardCostService : IActionCardCostGateway
    {
        private readonly Func<TimelineManager> _getTimeline;
        private readonly Func<IPlayerInputProvider> _getInput;
        private readonly FlipConditionEvaluator _flipEvaluator;
        private readonly ActionCardResourcePool _resources;

        public ActionCardCostService(
            Func<TimelineManager> getTimeline,
            Func<IPlayerInputProvider> getInput,
            FlipConditionEvaluator flipEvaluator,
            ActionCardResourcePool resources)
        {
            _getTimeline = getTimeline ?? throw new ArgumentNullException(nameof(getTimeline));
            _getInput = getInput ?? throw new ArgumentNullException(nameof(getInput));
            _flipEvaluator = flipEvaluator ?? throw new ArgumentNullException(nameof(flipEvaluator));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        }

        public async UniTask<ActionCardCostTransaction> PrepareAsync(
            CharacterActionCardInstance card)
        {
            if (card == null || !CanPayResourceCosts(card.OwnerCharacterId, card.Costs))
                return null;

            var prepared = new List<PreparedActionCardCost>(card.Costs.Count);
            foreach (ActionCardCostDefinition definition in card.Costs)
            {
                if (definition.Kind != ActionCardCostKind.FlipOtherCard)
                {
                    prepared.Add(new PreparedActionCardCost(definition));
                    continue;
                }

                var selected = new List<int>(definition.Amount);
                for (int i = 0; i < definition.Amount; i++)
                {
                    List<int> candidates = _flipEvaluator.GetFlippableCostCandidates(
                        card.OwnerCharacterId,
                        card.InstanceId,
                        definition.RequiredCardTag,
                        selected);
                    if (candidates.Count == 0)
                        return null;

                    IPlayerInputProvider input = _getInput();
                    if (input == null)
                        return null;
                    int selectedId = await input.RequestSelectCard(
                        "选择要翻转的其他行动卡作为费用",
                        candidates);
                    if (!candidates.Contains(selectedId))
                        return null;
                    selected.Add(selectedId);
                }
                prepared.Add(new PreparedActionCardCost(definition, selected));
            }

            return new ActionCardCostTransaction(prepared);
        }

        public bool CanPay(int ownerId, IReadOnlyList<PreparedActionCardCost> costs)
        {
            int inspiration = 0;
            int willpower = 0;
            var selectedCardIds = new HashSet<int>();

            foreach (PreparedActionCardCost prepared in costs)
            {
                ActionCardCostDefinition definition = prepared.Definition;
                switch (definition.Kind)
                {
                    case ActionCardCostKind.TimePoint:
                        break;
                    case ActionCardCostKind.CombatInspiration:
                        inspiration += definition.Amount;
                        break;
                    case ActionCardCostKind.Willpower:
                        willpower += definition.Amount;
                        break;
                    case ActionCardCostKind.FlipOtherCard:
                        if (prepared.SelectedCardIds.Count != definition.Amount)
                            return false;
                        foreach (int cardId in prepared.SelectedCardIds)
                        {
                            if (!selectedCardIds.Add(cardId) ||
                                !_flipEvaluator.CanFlipAsCost(
                                    cardId,
                                    ownerId,
                                    definition.RequiredCardTag))
                                return false;
                        }
                        break;
                }
            }

            TimelineManager timeline = _getTimeline();
            return timeline != null &&
                   timeline.Contains(ownerId) &&
                   _resources.CanSpendCombatInspiration(ownerId, inspiration) &&
                   timeline.CanSpendWillpower(ownerId, willpower);
        }

        public void Commit(int ownerId, IReadOnlyList<PreparedActionCardCost> costs)
        {
            int timePoints = 0;
            int inspiration = 0;
            int willpower = 0;
            var cardsToFlip = new List<int>();

            foreach (PreparedActionCardCost prepared in costs)
            {
                switch (prepared.Definition.Kind)
                {
                    case ActionCardCostKind.TimePoint:
                        timePoints += prepared.Definition.Amount;
                        break;
                    case ActionCardCostKind.CombatInspiration:
                        inspiration += prepared.Definition.Amount;
                        break;
                    case ActionCardCostKind.Willpower:
                        willpower += prepared.Definition.Amount;
                        break;
                    case ActionCardCostKind.FlipOtherCard:
                        cardsToFlip.AddRange(prepared.SelectedCardIds);
                        break;
                }
            }

            TimelineManager timeline = _getTimeline();
            if (inspiration > 0)
                _resources.TrySpendCombatInspiration(ownerId, inspiration);
            if (willpower > 0)
                timeline.TrySpendWillpower(ownerId, willpower);
            if (timePoints > 0)
                timeline.AccumulateTimePoints(ownerId, timePoints);
            foreach (int cardId in cardsToFlip)
                _flipEvaluator.FlipAsCost(cardId);
        }

        public int AddCombatInspiration(int ownerId, int amount) =>
            _resources.AddCombatInspiration(ownerId, amount);

        public int GetCombatInspiration(int ownerId) =>
            _resources.GetCombatInspiration(ownerId);

        private bool CanPayResourceCosts(
            int ownerId,
            IReadOnlyList<ActionCardCostDefinition> costs)
        {
            int inspiration = 0;
            int willpower = 0;
            foreach (ActionCardCostDefinition cost in costs)
            {
                if (cost.Kind == ActionCardCostKind.CombatInspiration)
                    inspiration += cost.Amount;
                else if (cost.Kind == ActionCardCostKind.Willpower)
                    willpower += cost.Amount;
            }

            TimelineManager timeline = _getTimeline();
            return timeline != null &&
                   timeline.Contains(ownerId) &&
                   _resources.CanSpendCombatInspiration(ownerId, inspiration) &&
                   timeline.CanSpendWillpower(ownerId, willpower);
        }
    }
}
