using Core;
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

}
