namespace GameplayBase.CombatSystem.Cards.FlipConditions
{
    // ─── 翻面条件 ────────────────────────────────────────────────────────────
    /// <summary>
    /// 翻面/恢复条件接口。每张卡可挂载多个条件，全部满足才可翻面。
    /// </summary>
    public interface IFlipCondition
    {
        FlipTriggerTiming Timing { get; }
        string Description { get; }

        bool Evaluate(FlipConditionContext context);
        void Consume(FlipConditionContext context);
    }
}
