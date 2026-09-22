using Cysharp.Threading.Tasks;
using GameplayBase.CombatSystem;

namespace GameplayBase.Card.CharacterActionCard
{
    /// <summary>
    /// 角色行动卡效果抽象基类。
    /// 正面效果/背面效果均继承此类，由 CharacterActionCardEffectData.CreateRuntime() 工厂化创建。
    /// </summary>
    public abstract class CharacterActionCardEffect
    {
        public abstract string Description { get; }
        public abstract TargetType TargetType { get; }

        /// <summary>
        /// 目标/范围规则（来自效果数据；可空）。由 CharacterActionCardInstance 在工厂化后注入。
        /// 攻击/移动等效果据此做范围门控；UI 据此做悬浮预览。
        /// </summary>
        public TargetingRuleData Targeting { get; set; }

        /// <summary>能否在当前上下文中执行</summary>
        public abstract bool CanExecute(ActionCardContext context);

        /// <summary>执行效果</summary>
        public abstract void Execute(ActionCardContext context);

        /// <summary>
        /// Queue-aware execution path. Synchronous legacy effects complete immediately;
        /// input/animation effects override this method and are awaited by the adapter runner.
        /// </summary>
        public virtual UniTask ExecuteAsync(ActionCardContext context)
        {
            Execute(context);
            return UniTask.CompletedTask;
        }
    }
}
