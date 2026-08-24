using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameplayBase.CombatSystem;
using HuntingInDarkness.GameCore.Cards;
using HuntingInDarkness.Combat;

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

    /// <summary>Compatibility-only runner for legacy combat cards. New gameplay uses ActionEnvironment.</summary>
    [Obsolete("Compatibility-only combat card runner. New gameplay must use ActionEnvironment.")]
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

        public async UniTask<ActionCardCostTransaction> PrepareAsync(CharacterActionCardInstance card, CancellationToken cancellationToken = default)
        {
            if (card == null || !CanPayResourceCosts(card.OwnerCharacterId, card.Costs))
                return null;

            var prepared = new List<PreparedActionCardCost>(card.Costs.Count);
            var selectedInspirationIds = new HashSet<int>();
            foreach (ActionCardCostDefinition definition in card.Costs)
            {
                if (definition.Kind == ActionCardCostKind.CombatInspiration && definition.InspirationRequirement != InspirationRequirement.Any)
                {
                    if (!await PrepareInspirationCost(card.OwnerCharacterId, definition, prepared, selectedInspirationIds, cancellationToken)) return null;
                    continue;
                }
                if (definition.Kind == ActionCardCostKind.CombatInspiration)
                    continue;
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
                    int selectedId = await input.RequestSelectCard("选择要翻转的其他行动卡作为费用", candidates, cancellationToken);
                    if (!candidates.Contains(selectedId))
                        return null;
                    selected.Add(selectedId);
                }
                prepared.Add(new PreparedActionCardCost(definition, selected));
            }

            foreach (ActionCardCostDefinition definition in card.Costs)
                if (definition.Kind == ActionCardCostKind.CombatInspiration && definition.InspirationRequirement == InspirationRequirement.Any)
                    if (!await PrepareInspirationCost(card.OwnerCharacterId, definition, prepared, selectedInspirationIds, cancellationToken)) return null;

            return new ActionCardCostTransaction(prepared);
        }

        public async UniTask<ActionCardCostTransaction> PrepareInspirationCostsAsync(int ownerId, IReadOnlyList<ActionCardCostDefinition> costs, CancellationToken cancellationToken = default)
        {
            if (!_resources.CanPayCosts(ownerId, costs)) return null;

            var prepared = new List<PreparedActionCardCost>(costs.Count);
            var selectedTokenIds = new HashSet<int>();
            foreach (ActionCardCostDefinition cost in costs)
                if (cost.Kind == ActionCardCostKind.CombatInspiration && cost.InspirationRequirement != InspirationRequirement.Any)
                    if (!await PrepareInspirationCost(ownerId, cost, prepared, selectedTokenIds, cancellationToken)) return null;
            foreach (ActionCardCostDefinition cost in costs)
                if (cost.Kind == ActionCardCostKind.CombatInspiration && cost.InspirationRequirement == InspirationRequirement.Any)
                    if (!await PrepareInspirationCost(ownerId, cost, prepared, selectedTokenIds, cancellationToken)) return null;
            return new ActionCardCostTransaction(prepared);
        }

        public bool CanPay(int ownerId, IReadOnlyList<PreparedActionCardCost> costs)
        {
            int willpower = 0;
            var selectedCardIds = new HashSet<int>();
            var selectedInspirationIds = new HashSet<int>();

            foreach (PreparedActionCardCost prepared in costs)
            {
                ActionCardCostDefinition definition = prepared.Definition;
                switch (definition.Kind)
                {
                    case ActionCardCostKind.TimePoint:
                        break;
                    case ActionCardCostKind.CombatInspiration:
                        if (!_resources.CanSpendCombatInspiration(ownerId, prepared.SelectedResourceIds, definition.InspirationRequirement, definition.Amount)) return false;
                        foreach (int tokenId in prepared.SelectedResourceIds)
                            if (!selectedInspirationIds.Add(tokenId))
                                return false;
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
                   timeline.CanSpendWillpower(ownerId, willpower);
        }

        public void Commit(int ownerId, IReadOnlyList<PreparedActionCardCost> costs)
        {
            Commit(ownerId, costs, null);
        }

        public bool TryCommitWithCardFlipEvents(int ownerId, ActionCardCostTransaction transaction, ICollection<CardFlippedEvent> cardFlipEvents)
        {
            if (transaction == null) return true;
            if (!CanPay(ownerId, transaction.Costs)) return false;
            Commit(ownerId, transaction.Costs, cardFlipEvents);
            return true;
        }

        private void Commit(int ownerId, IReadOnlyList<PreparedActionCardCost> costs, ICollection<CardFlippedEvent> cardFlipEvents)
        {
            int timePoints = 0;
            int willpower = 0;
            var cardsToFlip = new List<int>();
            var inspirationTokenIds = new List<int>();

            foreach (PreparedActionCardCost prepared in costs)
            {
                switch (prepared.Definition.Kind)
                {
                    case ActionCardCostKind.TimePoint:
                        timePoints += prepared.Definition.Amount;
                        break;
                    case ActionCardCostKind.CombatInspiration:
                        inspirationTokenIds.AddRange(prepared.SelectedResourceIds);
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
            if (inspirationTokenIds.Count > 0)
            {
                int oldCount = _resources.GetCombatInspiration(ownerId);
                _resources.TrySpendCombatInspiration(ownerId, inspirationTokenIds);
                PublishInspirationChange(ownerId, oldCount);
            }
            if (willpower > 0)
                timeline.TrySpendWillpower(ownerId, willpower);
            if (timePoints > 0)
                timeline.AccumulateTimePoints(ownerId, timePoints);
            foreach (int cardId in cardsToFlip)
            {
                if (cardFlipEvents == null)
                {
                    _flipEvaluator.FlipAsCost(cardId);
                    continue;
                }
                if (_flipEvaluator.TryApplyFlipAsCost(cardId, out CardFlippedEvent evt))
                    cardFlipEvents.Add(evt);
            }
        }

        public int AddCombatInspiration(int ownerId, int amount)
        {
            int oldCount = _resources.GetCombatInspiration(ownerId);
            int newCount = _resources.AddCombatInspiration(ownerId, amount);
            PublishInspirationChange(ownerId, oldCount);
            return newCount;
        }

        public async UniTask<InspirationGain> AddCombatInspirationAsync(int ownerId, CombatInspirationColor color, CancellationToken cancellationToken = default)
        {
            int oldCount = _resources.GetCombatInspiration(ownerId);
            InspirationGain gain = _resources.TryAddCombatInspiration(ownerId, color);
            if (gain.Result == InspirationGainResult.RequiresReplacement)
            {
                if (_getInput() is not IPlayerOptionInputProvider optionInput)
                    return new InspirationGain(InspirationGainResult.Discarded, default);

                var options = new List<PlayerChoiceOption>();
                IReadOnlyList<CombatInspirationToken> tokens = _resources.GetTokens(ownerId);
                for (int index = 0; index < tokens.Count; index++)
                {
                    CombatInspirationToken token = tokens[index];
                    options.Add(new PlayerChoiceOption(token.Id, $"替换 {CombatInspirationPresentation.GetName(token.Color)}"));
                }
                int selectedTokenId = await optionInput.RequestSelectOption($"思维区已满：如何处理新获得的{CombatInspirationPresentation.GetName(color)}灵感？", options, cancelOptionId: -1, cancelLabel: "丢弃新灵感", cancellationToken: cancellationToken);
                if (selectedTokenId < 0)
                    return new InspirationGain(InspirationGainResult.Discarded, default);
                gain = _resources.TryAddCombatInspiration(ownerId, color, selectedTokenId);
            }

            if (gain.Result == InspirationGainResult.Added || gain.Result == InspirationGainResult.Replaced)
                PublishInspirationChange(ownerId, oldCount);
            return gain;
        }

        public int GetCombatInspiration(int ownerId) =>
            _resources.GetCombatInspiration(ownerId);

        public int GetCombatInspirationCapacity(int ownerId) => _resources.GetCapacity(ownerId);

        public IReadOnlyList<CombatInspirationToken> GetCombatInspirationTokens(int ownerId) => _resources.GetTokens(ownerId);

        private bool CanPayResourceCosts(
            int ownerId,
            IReadOnlyList<ActionCardCostDefinition> costs)
        {
            int willpower = 0;
            foreach (ActionCardCostDefinition cost in costs)
            {
                if (cost.Kind == ActionCardCostKind.Willpower)
                    willpower += cost.Amount;
            }

            TimelineManager timeline = _getTimeline();
            return timeline != null &&
                   timeline.Contains(ownerId) &&
                   _resources.CanPayCosts(ownerId, costs) &&
                   timeline.CanSpendWillpower(ownerId, willpower);
        }

        private async UniTask<bool> PrepareInspirationCost(int ownerId, ActionCardCostDefinition definition, List<PreparedActionCardCost> prepared, HashSet<int> selectedTokenIds, CancellationToken cancellationToken = default)
        {
            var selected = new List<int>(definition.Amount);
            for (int index = 0; index < definition.Amount; index++)
            {
                List<CombatInspirationToken> candidates = _resources.GetSpendableTokens(ownerId, definition.InspirationRequirement, selectedTokenIds);
                if (candidates.Count == 0) return false;

                int selectedTokenId = candidates[0].Id;
                if (candidates.Count > 1 && _getInput() is IPlayerOptionInputProvider optionInput)
                {
                    var options = new List<PlayerChoiceOption>(candidates.Count);
                    foreach (CombatInspirationToken candidate in candidates)
                        options.Add(new PlayerChoiceOption(candidate.Id, CombatInspirationPresentation.GetName(candidate.Color)));
                    selectedTokenId = await optionInput.RequestSelectOption("选择要支付的战斗灵感", options, cancellationToken: cancellationToken);
                }
                if (!candidates.Exists(candidate => candidate.Id == selectedTokenId)) return false;

                selected.Add(selectedTokenId);
                selectedTokenIds.Add(selectedTokenId);
            }
            prepared.Add(new PreparedActionCardCost(definition, selected));
            return true;
        }

        private void PublishInspirationChange(int ownerId, int oldCount)
        {
            int newCount = _resources.GetCombatInspiration(ownerId);
            EventBus.Publish(new CombatInspirationChangedEvent { CharacterId = ownerId, OldCount = oldCount, NewCount = newCount });
        }
    }
}
