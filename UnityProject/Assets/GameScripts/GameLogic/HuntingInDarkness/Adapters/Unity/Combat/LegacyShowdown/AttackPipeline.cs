using System.Collections.Generic;
using CardTactics.CombatSystem;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameplayBase.CombatSystem
{
    /// <summary>
    /// 攻击管线：一组有序的 IAttackStep。
    /// 依次执行每个步骤，任意步骤抛出 AttackAbortedException 则终止。
    ///
    /// 扩展方式：
    ///   - 在构建管线时插入额外步骤（角色特性、装备效果等）
    ///   - 受击部位卡效果通过 IHitLocationEffectResolver 注入步骤
    ///   - 步骤本身内部可以 await 玩家操作
    /// </summary>
    public class AttackPipeline
    {
        private readonly List<IAttackStep> _steps = new();
        public IReadOnlyList<IAttackStep> Steps => _steps;

        public AttackPipeline(IEnumerable<IAttackStep> steps)
        {
            _steps.AddRange(steps);
        }

        /// <summary>在指定步骤类型之后插入新步骤</summary>
        public void InsertAfter<T>(IAttackStep newStep) where T : IAttackStep
        {
            for (int i = 0; i < _steps.Count; i++)
            {
                if (_steps[i] is T)
                {
                    _steps.Insert(i + 1, newStep);
                    return;
                }
            }
            // 找不到则追加到末尾
            _steps.Add(newStep);
        }

        /// <summary>在指定步骤类型之前插入新步骤</summary>
        public void InsertBefore<T>(IAttackStep newStep) where T : IAttackStep
        {
            for (int i = 0; i < _steps.Count; i++)
            {
                if (_steps[i] is T)
                {
                    _steps.Insert(i, newStep);
                    return;
                }
            }
            _steps.Insert(0, newStep);
        }

        /// <summary>追加步骤到末尾</summary>
        public void Append(IAttackStep step)
        {
            _steps.Add(step);
        }

        /// <summary>
        /// 执行整个管线。
        /// 任意步骤抛出 AttackAbortedException → 捕获并标记中断。
        /// </summary>
        public async UniTask<AttackResult> Run(
            AttackContext context, IPlayerInputProvider input)
        {
            try
            {
                foreach (var step in _steps)
                {
                    await step.Execute(context, input);

                    // 步骤执行后检查中断标记
                    if (context.IsAborted)
                    {
                        Debug.Log($"[AttackPipeline] 攻击被中断（标记方式）");
                        return new AttackResult
                        {
                            Completed = false,
                            AbortReason = "攻击被中断",
                            Context = context
                        };
                    }
                }

                return new AttackResult
                {
                    Completed = true,
                    Context = context
                };
            }
            catch (AttackAbortedException ex)
            {
                Debug.Log($"[AttackPipeline] 攻击被中断（异常方式）: {ex.Reason}");
                return new AttackResult
                {
                    Completed = false,
                    AbortReason = ex.Reason,
                    Context = context
                };
            }
        }
    }

    /// <summary>攻击管线执行结果</summary>
    public struct AttackResult
    {
        public bool Completed;
        public string AbortReason;
        public AttackContext Context;
    }
}