using GameplayBase;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 游戏大阶段状态机（纯 C#）。
    /// 职责：管理 Settlement / Hunt / BossFight 三个阶段的切换，
    /// 通过 EventBus 广播 GamePhaseChangedEvent，并回调 GameManager
    /// 以 Enable/Disable 对应的根 GameObject。
    ///
    /// 合法转换路径：
    ///   Settlement → Hunt → BossFight → Settlement（循环）
    ///   开发者模式可任意跳转。
    /// </summary>
    public class PhaseManager
    {
        public GamePhase CurrentPhase { get; private set; } = GamePhase.Settlement;

        /// <summary>阶段切换时由 GameManager 注入；参数为（旧阶段, 新阶段）</summary>
        public System.Action<GamePhase, GamePhase> OnPhaseTransition;

        // ─── 切换 ───────────────────────────────────────────────────

        /// <summary>切换到指定阶段（允许开发者任意跳转）</summary>
        public void TransitionTo(GamePhase newPhase)
        {
            if (newPhase == CurrentPhase)
            {
                Debug.LogWarning($"[PhaseManager] 已处于 {newPhase} 阶段，忽略重复跳转");
                return;
            }

            var prev = CurrentPhase;
            CurrentPhase = newPhase;

            Debug.Log($"[PhaseManager] {prev} → {newPhase}");

            // 通知 GameManager 做 Enable/Disable
            OnPhaseTransition?.Invoke(prev, newPhase);

            // 广播事件（供 UI、系统监听）
            EventBus.Publish(new GamePhaseChangedEvent
            {
                PreviousPhase = prev,
                NewPhase      = newPhase
            });
        }

        // ─── 便捷方法 ────────────────────────────────────────────────

        public void ToSettlement() => TransitionTo(GamePhase.Settlement);
        public void ToHunt()       => TransitionTo(GamePhase.Hunt);
        public void ToBossFight()  => TransitionTo(GamePhase.BossFight);
    }
}
