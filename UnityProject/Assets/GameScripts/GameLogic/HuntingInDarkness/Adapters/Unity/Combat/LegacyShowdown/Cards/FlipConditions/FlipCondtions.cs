using Core;
using UnityEngine;

namespace GameplayBase.CombatSystem.Cards.FlipConditions
{
        // ═══════════════════════════════════════════
        // 示例翻面/恢复条件实现
        // ═══════════════════════════════════════════

        /// <summary>使用后自动翻面（最常见的情况：卡牌打出即翻面）</summary>
        [System.Serializable]
        public class FlipOnPlayConditionData : FlipConditionData
        {
            public override IFlipCondition CreateRuntime() => new FlipOnPlayCondition();
        }

        public class FlipOnPlayCondition : IFlipCondition
        {
            public FlipTriggerTiming Timing => FlipTriggerTiming.OnPlay;
            public string Description => "使用后翻面";

            public bool Evaluate(FlipConditionContext context) => true; // 打出即翻
            public void Consume(FlipConditionContext context) { } // 无消耗
        }

        /// <summary>支付费用恢复（点击背面卡牌，支付费用翻回正面）</summary>
        [System.Serializable]
        public class PayCostRestoreConditionData : FlipConditionData
        {
            public int cost;
            public override IFlipCondition CreateRuntime() => new PayCostRestoreCondition(cost);
        }

        public class PayCostRestoreCondition : IFlipCondition
        {
            private readonly int _cost;

            public FlipTriggerTiming Timing => FlipTriggerTiming.OnPayCost;
            public string Description => $"支付 {_cost} 恢复";

            public PayCostRestoreCondition(int cost) => _cost = cost;

            public bool Evaluate(FlipConditionContext context)
            {
                // TODO: 接入资源检查 — context 需要暴露 IGameContext 或 ICurrencyProvider，
                //   以便查询当前玩家货币（金/骨/特殊资源）是否 >= _cost。
                //   目前始终返回 true，允许免费恢复（不影响现有 Boss 决战流程）。
                return true;
            }

            public void Consume(FlipConditionContext context)
            {
                // TODO: 扣除费用 — 与 Evaluate 联动，从 IGameContext 中扣除 _cost 对应资源。
                Debug.Log($"[PayCostRestore] 支付 {_cost} 恢复卡牌 #{context.CardInstanceId}");
            }
        }

        /// <summary>当任意其他卡牌翻面时，恢复自身</summary>
        [System.Serializable]
        public class RestoreOnOtherFlipData : FlipConditionData
        {
            public override IFlipCondition CreateRuntime() => new RestoreOnOtherFlipCondition();
        }

        public class RestoreOnOtherFlipCondition : IFlipCondition
        {
            public FlipTriggerTiming Timing => FlipTriggerTiming.OnOtherCardFlipped;
            public string Description => "当其他卡翻面时，恢复自身";

            public bool Evaluate(FlipConditionContext context)
            {
                // 只要有触发源（其他卡翻面了）就满足
                return context.TriggerSourceCardId.HasValue;
            }

            public void Consume(FlipConditionContext context) { }
        }

        /// <summary>
        /// 当恢复 N 张其他卡后恢复自身。
        /// 需要跨多次事件累计计数。
        /// </summary>
        [System.Serializable]
        public class RestoreAfterNRestoresData : FlipConditionData
        {
            public int requiredCount = 2;
            public override IFlipCondition CreateRuntime() => new RestoreAfterNRestoresCondition(requiredCount);
        }

        public class RestoreAfterNRestoresCondition : IFlipCondition
        {
            private readonly int _required;
            private int _currentCount;

            public FlipTriggerTiming Timing => FlipTriggerTiming.OnOtherCardRestored;
            public string Description => $"当恢复 {_required} 张其他卡后恢复自身 ({_currentCount}/{_required})";

            public RestoreAfterNRestoresCondition(int required)
            {
                _required = required;
                _currentCount = 0;
            }

            public bool Evaluate(FlipConditionContext context)
            {
                // 每次被评估时递增计数（由 Evaluator 在事件触发时调用）
                _currentCount++;
                return _currentCount >= _required;
            }

            public void Consume(FlipConditionContext context)
            {
                _currentCount = 0; // 恢复后重置计数
            }
        }

        /// <summary>回合结束时自动恢复</summary>
        [System.Serializable]
        public class RestoreOnTurnEndData : FlipConditionData
        {
            public override IFlipCondition CreateRuntime() => new RestoreOnTurnEndCondition();
        }

        public class RestoreOnTurnEndCondition : IFlipCondition
        {
            public FlipTriggerTiming Timing => FlipTriggerTiming.OnTurnEnd;
            public string Description => "回合结束时恢复";

            public bool Evaluate(FlipConditionContext context) => true;
            public void Consume(FlipConditionContext context) { }
        }
}
