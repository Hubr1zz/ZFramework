using Core;
using GameplayBase.CombatSystem;
using UnityEngine;

namespace GameplayBase.Card.HitLocationCard
{
    // ═══════════════════════════════════════════
    // 受击部位效果上下文
    // ═══════════════════════════════════════════

    /// <summary>
    /// 受击部位效果执行上下文。
    /// 在 HP 扣减之后构建，携带本次攻击的完整结果，供效果条件判断。
    /// </summary>
    public class HitLocationContext
    {
        /// <summary>攻击者ID</summary>
        public int AttackerId;

        /// <summary>本次攻击对该部位的判定结果</summary>
        public HitResult HitResult;

        /// <summary>是否暴击（攻击力超出韧性值的整数倍，由流程步骤写入）</summary>
        public bool IsCriticalHit;

        /// <summary>受击部位运行时状态（HP扣减后）</summary>
        public HitLocationRuntimeState HitLocationState;

        /// <summary>全局游戏上下文（只读查询）</summary>
        public IGameContext GameContext;

        /// <summary>棋盘查询接口</summary>
        public IBoardQuery BoardQuery;
    }

    // ═══════════════════════════════════════════
    // 受击部位效果基类
    // ═══════════════════════════════════════════

    /// <summary>
    /// 受击部位卡效果抽象基类。
    /// 由 HitLocationEffectData.CreateRuntime() 工厂化创建，
    /// 在 DefaultHitLocationEffectResolver.ResolveHitLocationEffects() 中驱动执行。
    /// </summary>
    public abstract class HitLocationEffect
    {
        public abstract string Description { get; }

        /// <summary>能否在当前上下文中执行</summary>
        public abstract bool CanExecute(HitLocationContext context);

        /// <summary>执行效果</summary>
        public abstract void Execute(HitLocationContext context);
    }

    // ═══════════════════════════════════════════
    // 掉落条件枚举
    // ═══════════════════════════════════════════

    /// <summary>
    /// DropResourceEffect 的触发条件（在 HitLocationEffectEntry.triggerCondition 之后
    /// 作为效果内部的二次判断）。
    /// </summary>
    public enum DropConditionType
    {
        /// <summary>满足外层触发条件后总是掉落</summary>
        Always,
        /// <summary>攻击判定成功时掉落</summary>
        OnAttackSuccess,
        /// <summary>攻击判定失败时掉落</summary>
        OnAttackFailure,
        /// <summary>部位 HP 归零（被摧毁）时掉落</summary>
        OnHitLocationDestroyed,
        /// <summary>暴击时掉落</summary>
        OnCriticalHit,
    }

    // ═══════════════════════════════════════════
    // 掉落资源效果
    // ═══════════════════════════════════════════

    /// <summary>
    /// 掉落资源效果数据（Inspector 可配置）。
    /// 支持二次条件判断：在外层 triggerCondition（OnSuccess/OnFailure/Always）
    /// 通过后，再检查此处配置的 dropCondition。
    /// </summary>
    [System.Serializable]
    public class DropResourceEffectData : HitLocationEffectData
    {
        [Header("掉落资源")]
        public string resourceName;
        [Min(1)] public int count = 1;

        [Header("掉落条件（叠加在外层触发条件之上）")]
        public DropConditionType dropCondition = DropConditionType.OnAttackSuccess;

        [Header("暴击额外数量")]
        [Min(0)] public int critBonus = 0;

        public override HitLocationEffect CreateRuntime() =>
            new DropResourceEffect(resourceName, count, dropCondition, critBonus);
    }

    /// <summary>
    /// 运行时掉落资源效果。
    /// 检查 DropConditionType 是否满足，满足则发布 ResourceDroppedEvent。
    /// </summary>
    public class DropResourceEffect : HitLocationEffect
    {
        private readonly string _resourceName;
        private readonly int _count;
        private readonly DropConditionType _condition;
        private readonly int _critBonus;

        public override string Description =>
            $"掉落 {_resourceName} ×{_count}（条件: {_condition}）";

        public DropResourceEffect(string resourceName, int count,
            DropConditionType condition, int critBonus = 0)
        {
            _resourceName = resourceName;
            _count        = count;
            _condition    = condition;
            _critBonus    = critBonus;
        }

        public override bool CanExecute(HitLocationContext context)
        {
            if (string.IsNullOrEmpty(_resourceName)) return false;
            return MeetsCondition(context);
        }

        public override void Execute(HitLocationContext context)
        {
            int amount = _count;
            if (context.IsCriticalHit && _critBonus > 0)
                amount += _critBonus;

            EventBus.Publish(new ResourceDroppedEvent
            {
                ResourceName            = _resourceName,
                Count                   = amount,
                SourceHitLocationName   = context.HitLocationState?.Data?.locationName ?? ""
            });

            Debug.Log($"[DropResourceEffect] 掉落 {_resourceName} ×{amount}" +
                      $"  条件:{_condition}  暴击:{context.IsCriticalHit}");
        }

        private bool MeetsCondition(HitLocationContext ctx) => _condition switch
        {
            DropConditionType.Always                 => true,
            DropConditionType.OnAttackSuccess        => ctx.HitResult == HitResult.Success,
            DropConditionType.OnAttackFailure        => ctx.HitResult == HitResult.Failure,
            DropConditionType.OnHitLocationDestroyed => ctx.HitLocationState?.IsDestroyed == true,
            DropConditionType.OnCriticalHit          => ctx.IsCriticalHit,
            _                                        => false
        };
    }

    // ═══════════════════════════════════════════
    // 基础扣血效果（可选显式配置）
    // ═══════════════════════════════════════════

    /// <summary>
    /// 基础扣血效果数据。
    /// 注意：ResolveHitLocationsStep 已内联处理 HP 扣减；
    /// 此效果适用于需要额外追加伤害的高级配置。
    /// </summary>
    [System.Serializable]
    public class BasicHitEffectData : HitLocationEffectData
    {
        [Min(1)] public int damage = 1;
        public override HitLocationEffect CreateRuntime() => new BasicHitEffect(damage);
    }

    /// <summary>攻击成功时对部位额外造成伤害</summary>
    public class BasicHitEffect : HitLocationEffect
    {
        private readonly int _damage;
        public override string Description => $"命中时额外造成 {_damage} 点伤害";

        public BasicHitEffect(int damage) => _damage = damage;

        public override bool CanExecute(HitLocationContext context)
            => context.HitResult == HitResult.Success;

        public override void Execute(HitLocationContext context)
        {
            if (context.HitLocationState == null) return;
            context.HitLocationState.DomainState.ApplyPendingDamage(_damage);
            Debug.Log($"[BasicHitEffect] {context.HitLocationState.Data?.locationName} " +
                      $"额外受到 {_damage} 点伤害，剩余 HP: {context.HitLocationState.CurrentHp}");
        }
    }

    // ═══════════════════════════════════════════
    // 效果数据基类（原 Core.HitLocationEffectData 迁移至此）
    // ═══════════════════════════════════════════

    /// <summary>
    /// 受击部位效果数据基类。可在 Inspector 中通过 [SerializeReference] 配置具体子类。
    /// </summary>
    [System.Serializable]
    public abstract class HitLocationEffectData
    {
        public abstract HitLocationEffect CreateRuntime();
    }
}
