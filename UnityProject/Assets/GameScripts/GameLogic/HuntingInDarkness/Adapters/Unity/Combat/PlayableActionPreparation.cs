using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.Card.CharacterActionCard;
using HuntingInDarkness.ActionFlow;
using HuntingInDarkness.GameCore.Cards;

namespace HuntingInDarkness.Combat
{
    /// <summary>需要玩家异步确认的效果先准备选择结果，费用提交后才改变战斗状态。</summary>
    public interface IPlayablePreparedActionEffect
    {
        bool IsPrepared { get; }
        UniTask<bool> PrepareAsync(ActionCardContext context, CancellationToken cancellationToken = default);
        UniTask ExecutePreparedAsync(ActionCardContext context, CancellationToken cancellationToken = default);
        void ResetPreparation();
    }

    /// <summary>把准备完成的卡牌效果展开为当前 Root 的因果 Child Action。</summary>
    public interface IPlayableQueuedActionEffect
    {
        GameAction CreateAction(ActionCardContext context, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target);
    }

    public interface IPlayableCancellableActionEffect
    {
        UniTask ExecuteAsync(ActionCardContext context, CancellationToken cancellationToken);
    }

    public static class PlayableActionPreparation
    {
        public static void EnqueuePreparation(ActionQueue queue, IReadOnlyList<CharacterActionCardEffect> effects, ActionCardContext context)
        {
            queue.EnqueueBack(new DelegateActionQueueAction("prepare-effects", async _ =>
            {
                try
                {
                    foreach (var effect in effects)
                    {
                        if (effect is not IPlayablePreparedActionEffect prepared) continue;
                        prepared.ResetPreparation();
                        if (!effect.CanExecute(context) || !await prepared.PrepareAsync(context))
                        {
                            Reset(effects);
                            return ActionQueueActionResult.Cancelled;
                        }
                    }
                    return ActionQueueActionResult.Completed;
                }
                catch
                {
                    Reset(effects);
                    throw;
                }
            }));
        }

        public static async UniTask<ActionQueueActionResult> ExecuteAsync(CharacterActionCardEffect effect, ActionCardContext context)
        {
            if (effect == null) return ActionQueueActionResult.Completed;
            if (effect is not IPlayablePreparedActionEffect prepared)
            {
                if (!effect.CanExecute(context)) return ActionQueueActionResult.Completed;
                await effect.ExecuteAsync(context);
                return ActionQueueActionResult.Completed;
            }
            if (!prepared.IsPrepared) return ActionQueueActionResult.Failed;

            try
            {
                await prepared.ExecutePreparedAsync(context);
                return ActionQueueActionResult.Completed;
            }
            finally
            {
                prepared.ResetPreparation();
            }
        }

        public static void Reset(IReadOnlyList<CharacterActionCardEffect> effects)
        {
            foreach (var effect in effects)
                if (effect is IPlayablePreparedActionEffect prepared)
                    prepared.ResetPreparation();
        }
    }
}
