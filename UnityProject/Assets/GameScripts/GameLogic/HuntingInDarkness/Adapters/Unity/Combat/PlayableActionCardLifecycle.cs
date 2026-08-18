using System.Collections.Generic;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.CombatSystem.Cards.FlipConditions;
using HuntingInDarkness.GameCore.Cards;
using UnityEngine;

namespace HuntingInDarkness.Combat
{
    public interface IPreparedActionCardRestoreCost
    {
        ActionCardCostDefinition Cost { get; }
    }

    [System.Serializable]
    public sealed class CombatInspirationRestoreConditionData : FlipConditionData
    {
        [Min(1)] public int amount = 1;
        public InspirationRequirement requiredColor = InspirationRequirement.Any;

        public override IFlipCondition CreateRuntime() => new CombatInspirationRestoreCondition(amount, requiredColor);
    }

    public sealed class CombatInspirationRestoreCondition : IFlipCondition, IPreparedActionCardRestoreCost
    {
        public FlipTriggerTiming Timing => FlipTriggerTiming.OnPayCost;
        public string Description => $"支付 {Cost.Amount} 点战斗灵感恢复";
        public ActionCardCostDefinition Cost { get; }

        public CombatInspirationRestoreCondition(int amount, InspirationRequirement requiredColor)
        {
            Cost = new ActionCardCostDefinition(ActionCardCostKind.CombatInspiration, Mathf.Max(1, amount), inspirationRequirement: requiredColor);
        }

        public bool Evaluate(FlipConditionContext context) => true;
        public void Consume(FlipConditionContext context) { }
    }

    public sealed class PlayableActionCardLifecycleService
    {
        private readonly FlipConditionEvaluator flipEvaluator;
        private readonly ActionCardCostService costService;

        public PlayableActionCardLifecycleService(FlipConditionEvaluator flipEvaluator, ActionCardCostService costService)
        {
            this.flipEvaluator = flipEvaluator;
            this.costService = costService;
        }

        public async UniTask<bool> TryRestoreAsync(CharacterActionCardInstance card)
        {
            if (card == null || card.CurrentFace != CardFace.FaceDown) return false;

            var costs = new List<ActionCardCostDefinition>();
            foreach (IFlipCondition condition in card.RestoreConditions)
                if (condition is IPreparedActionCardRestoreCost preparedCost)
                    costs.Add(preparedCost.Cost);

            ActionCardCostTransaction transaction = costs.Count > 0
                ? await costService.PrepareInspirationCostsAsync(card.OwnerCharacterId, costs)
                : null;
            if (costs.Count > 0 && transaction == null) return false;
            if (!await flipEvaluator.TryRestoreAsync(card.InstanceId)) return false;
            if (transaction == null || transaction.TryCommit(card.OwnerCharacterId, costService)) return true;

            flipEvaluator.FlipAsCost(card.InstanceId);
            return false;
        }
    }
}
