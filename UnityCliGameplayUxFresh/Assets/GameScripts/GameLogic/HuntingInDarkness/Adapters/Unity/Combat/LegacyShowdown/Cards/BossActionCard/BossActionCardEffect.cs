using Cysharp.Threading.Tasks;

namespace GameplayBase.Card.BossActionCard
{
    /// <summary>
    /// Boss行动卡效果抽象基类。
    /// 由 BossActionCardEffectData.CreateRuntime() 工厂化创建，
    /// 在 BossController.ExecutePendingActions() 中驱动执行。
    /// </summary>
    public abstract class BossActionCardEffect
    {
        public abstract string Description { get; }

        /// <summary>能否在当前上下文中执行</summary>
        public abstract bool CanExecute(ActionCardContext context);

        /// <summary>执行效果；完成的任务即 Boss 回合可以继续推进的信号。</summary>
        public abstract UniTask ExecuteAsync(ActionCardContext context);
    }
}
