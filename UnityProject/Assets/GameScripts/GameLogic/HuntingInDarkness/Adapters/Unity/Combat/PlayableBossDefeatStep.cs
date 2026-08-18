using CardTactics.CombatSystem;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase.CombatSystem;
using System.Collections.Generic;

namespace HuntingInDarkness.Combat
{
    /// <summary>在整次攻击与结果展示结束后判定 Boss 是否失去全部部位。</summary>
    public sealed class PlayableBossDefeatStep : IAttackStep
    {
        private bool hasPublished;

        public UniTask Execute(AttackContext context, IPlayerInputProvider input)
        {
            TryPublish(context?.GameContext?.Boss as GameplayBase.IBossVitalityState, context?.AllHitLocationStates);
            return UniTask.CompletedTask;
        }

        public bool TryPublish(IReadOnlyList<HitLocationRuntimeState> states) => TryPublish(null, states);

        public bool TryPublish(GameplayBase.IBossVitalityState vitality, IReadOnlyList<HitLocationRuntimeState> states)
        {
            if (hasPublished) return false;

            if (vitality != null)
            {
                if (!vitality.TryClaimDefeat()) return false;
            }
            else
            {
                if (states == null || states.Count == 0) return false;

                foreach (var state in states)
                    if (state != null && !state.IsDestroyed)
                        return false;
            }

            hasPublished = true;
            EventBus.Publish(new BossDefeatedEvent());
            return true;
        }
    }
}
