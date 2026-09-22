using System.Threading;
using Cysharp.Threading.Tasks;
using GameplayBase.CombatSystem;

namespace CardTactics.CombatSystem
{
    // ═══════════════════════════════════════════
    // 攻击管线步骤接口
    // ═══════════════════════════════════════════

    /// <summary>
    /// 攻击管线中的一个步骤。
    /// 
    /// 设计理念：攻击流程 = 一系列有序的 IAttackStep。
    /// 每个步骤可以：
    ///   - await 玩家输入（通过 IPlayerInputProvider）
    ///   - 修改 AttackContext 中的数据
    ///   - 抛出 AttackAbortedException 终止整个攻击
    ///   - 正常返回让管线继续下一步
    ///
    /// 扩展案例：
    ///   - 部位卡"翻开时额外判定" → 新增一个 IAttackStep 插入管线
    ///   - 角色特性"命中后可移动" → 在命中判定后插入一个移动步骤
    ///   - 部位卡"直接终止攻击" → 在步骤中抛出 AttackAbortedException
    /// </summary>
    public interface IAttackStep
    {
        /// <summary>执行此步骤。可以是异步的（等待玩家输入等）。</summary>
        UniTask Execute(AttackContext context, IPlayerInputProvider input, CancellationToken cancellationToken = default);
    }
}
