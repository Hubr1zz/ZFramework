using System.Collections.Generic;
using CardTactics.CombatSystem;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase.Card.HitLocationCard;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.GameCore.Foundation;
using SO.Boss.HitLocation;
using UnityEngine;

namespace GameplayBase.CombatSystem
{
    // ═══════════════════════════════════════════
    // 步骤1：洗牌 + 翻开受击部位卡
    // ═══════════════════════════════════════════

    /// <summary>
    /// 从Boss受击部位卡池（不含已摧毁的）中按权重随机选出"角色速度"张，
    /// 触发洗牌动画后将选中的卡翻至正面。
    /// 读取 context.AllHitLocationStates，写入 context.RevealedHitLocations。
    /// </summary>
    public class DrawHitLocationStep : IAttackStep
    {
        private readonly IRandomSource _random;

        public DrawHitLocationStep(IRandomSource random)
        {
            _random = random ?? throw new System.ArgumentNullException(nameof(random));
        }

        public async UniTask Execute(AttackContext context, IPlayerInputProvider input)
        {
            int drawCount = context.AttackerStats?.Speed ?? 1;
            if (drawCount <= 0) drawCount = 1;

            var all       = context.AllHitLocationStates ?? new List<HitLocationRuntimeState>();
            var available = all.FindAll(s => !s.IsDestroyed);

            context.RevealedHitLocations = WeightedSelection.DrawWithoutReplacement(
                available,
                System.Math.Min(drawCount, available.Count),
                state => state.DomainState.Definition.DrawWeight,
                _random);

            await input.PlayShuffleAndReveal(all, context.RevealedHitLocations);

            Debug.Log($"[Combat] 翻出 {context.RevealedHitLocations.Count} 张受击部位卡");
        }

    }

    // ═══════════════════════════════════════════
    // 步骤2：力量尝试（玩家逐张选择判定）
    // ═══════════════════════════════════════════

    /// <summary>
    /// 力量尝试阶段：
    ///   1. 玩家从翻开的卡中点击选择一张
    ///   2. 掷骰，攻击力 >= 韧性视为成功，攻击力 >= 2×韧性视为暴击
    ///   3. 成功：部位 -1 HP；HP归零则永久摧毁（保持正面）
    ///   4. 触发受击部位卡效果（使用 HitLocationContext）
    ///   5. 重复直到所有翻开的卡全部结算
    ///   6. 未摧毁的卡翻回背面
    /// </summary>
    public class ResolveHitLocationsStep : IAttackStep
    {
        private readonly IHitLocationEffectResolver _effectResolver;

        public ResolveHitLocationsStep(IHitLocationEffectResolver effectResolver)
        {
            _effectResolver = effectResolver;
        }

        public async UniTask Execute(AttackContext context, IPlayerInputProvider input)
        {
            context.CalculateAttackPower();

            var remaining = new List<HitLocationRuntimeState>(
                context.RevealedHitLocations ?? new List<HitLocationRuntimeState>());
            int total = remaining.Count;

            while (remaining.Count > 0)
            {
                int current = total - remaining.Count + 1;
                var selected = await input.RequestSelectRevealedCard(
                    $"力量尝试 [{current}/{total}] — 选择要攻击的部位", remaining);

                context.CurrentHitLocation = selected.Data;

                // ─── 掷骰判定 ───
                string rollPrompt =
                    $"{selected.Data.locationName}" +
                    $"（韧性 {selected.Data.toughness}  HP {selected.CurrentHp}/{selected.Data.maxHp}）\n" +
                    $"攻击力: {context.TotalAttackPower}\n点击掷骰";

                context.RollResult = await input.RequestRoll(rollPrompt, 100);

                AttackCheck check = CombatRules.ResolveHitLocationAttack(
                    context.TotalAttackPower,
                    selected.DomainState.Definition.Toughness);
                context.HitResult = check.Outcome == AttackOutcome.Success
                    ? HitResult.Success
                    : HitResult.Failure;
                context.IsCriticalHit = check.IsCritical;

                Debug.Log($"[Combat] {selected.Data.locationName}: " +
                          $"攻击力 {context.TotalAttackPower} vs 韧性 {selected.Data.toughness} " +
                          $"→ {context.HitResult}{(context.IsCriticalHit ? " ★暴击" : "")}");

                // ─── HP 扣减 ───
                if (context.HitResult == HitResult.Success)
                {
                    if (selected.ApplyDamage(1))
                    {
                        EventBus.Publish(new HitLocationDestroyedEvent
                        {
                            CardData = selected.Data,
                            PartName = selected.Data.locationName
                        });
                    }
                }

                // ─── 触发受击部位卡效果 ───
                await _effectResolver.ResolveHitLocationEffects(
                    context, selected, input);

                if (context.IsAborted)
                    throw new AttackAbortedException(
                        $"受击部位 [{selected.Data.locationName}] 效果中断了攻击", context);

                // ─── 展示结果 ───
                string resultMsg = context.HitResult == HitResult.Success
                    ? (selected.IsDestroyed
                        ? $"★ {selected.Data.locationName} 已摧毁！"
                        : $"命中 {selected.Data.locationName}！（剩余 HP: {selected.CurrentHp}）" +
                          (context.IsCriticalHit ? " ★暴击" : ""))
                    : $"未能击穿 {selected.Data.locationName}";

                await input.ShowResult(resultMsg);

                remaining.Remove(selected);
            }

            // ─── 未摧毁的卡翻回背面 ───
            foreach (var state in context.RevealedHitLocations ?? new List<HitLocationRuntimeState>())
            {
                if (!state.IsDestroyed)
                {
                    state.Hide();
                    EventBus.Publish(new HitLocationFlippedFaceDownEvent { CardData = state.Data });
                }
            }

            context.CurrentHitLocation = null;
        }
    }

    // ═══════════════════════════════════════════
    // 受击部位效果解析器接口
    // ═══════════════════════════════════════════

    public interface IHitLocationEffectResolver
    {
        UniTask OnHitLocationRevealed(
            AttackContext context, HitLocationCardData hitLocation,
            IPlayerInputProvider input);

        UniTask ResolveHitLocationEffects(
            AttackContext context, HitLocationRuntimeState hitLocationState,
            IPlayerInputProvider input);
    }

    // ═══════════════════════════════════════════
    // 格子选择攻击步骤（特殊战斗覆盖模式）
    // ═══════════════════════════════════════════

    /// <summary>
    /// 当 AttackContext.EffectiveTargetSelector 为 TileTargetSelector 时，
    /// 替换默认受击部位流程，改为让玩家选择一个格子后执行攻击。
    /// </summary>
    public class TileSelectAttackStep : IAttackStep
    {
        public async UniTask Execute(AttackContext context, IPlayerInputProvider input)
        {
            var selector = context.EffectiveTargetSelector;

            if (selector == null || !selector.HasValidTargets(context))
            {
                await input.ShowResult("没有合法的目标格子，攻击取消");
                context.IsAborted = true;
                return;
            }

            var selection = await selector.RequestSelection("选择目标格子", context, input);

            if (selection == null)
            {
                context.IsAborted = true;
                return;
            }

            context.SelectedTile = selection.Value.Tile;
            context.CalculateAttackPower();

            Debug.Log($"[TileSelectAttackStep] {context.AttackerId} → " +
                      $"格子 {context.SelectedTile}  攻击力:{context.TotalAttackPower}");

            await input.ShowResult(
                $"攻击格子 {context.SelectedTile}！攻击力：{context.TotalAttackPower}");
        }
    }

    // ═══════════════════════════════════════════
    // 默认受击部位效果解析器
    // ═══════════════════════════════════════════

    public class DefaultHitLocationEffectResolver : IHitLocationEffectResolver
    {
        private readonly IGameContext _gameContext;
        private readonly IBoardQuery  _boardQuery;

        public DefaultHitLocationEffectResolver(IGameContext gameContext, IBoardQuery boardQuery)
        {
            _gameContext = gameContext;
            _boardQuery  = boardQuery;
        }

        public async UniTask OnHitLocationRevealed(
            AttackContext context, HitLocationCardData hitLocation,
            IPlayerInputProvider input)
        {
            await UniTask.CompletedTask;
        }

        /// <summary>
        /// 在攻击判定和HP扣减完成后执行受击部位卡上的所有效果词条。
        /// 先检查外层 triggerCondition（OnSuccess / OnFailure / Always），
        /// 再由效果自身的 CanExecute 检查内层条件（如 DropConditionType）。
        /// </summary>
        public async UniTask ResolveHitLocationEffects(
            AttackContext context, HitLocationRuntimeState hitLocationState,
            IPlayerInputProvider input)
        {
            // 构建受击部位效果上下文（HP 已扣减完毕，携带完整判定结果）
            var effectContext = new HitLocationContext
            {
                AttackerId        = context.AttackerId,
                HitResult         = context.HitResult,
                IsCriticalHit     = context.IsCriticalHit,
                HitLocationState  = hitLocationState,
                GameContext       = _gameContext,
                BoardQuery        = _boardQuery
            };

            foreach (var entry in hitLocationState.Data.effects)
            {
                // 外层触发时机检查
                bool shouldTrigger = entry.triggerCondition switch
                {
                    HitLocationTriggerCondition.OnSuccess => context.HitResult == HitResult.Success,
                    HitLocationTriggerCondition.OnFailure => context.HitResult == HitResult.Failure,
                    HitLocationTriggerCondition.Always    => true,
                    _                                     => false
                };

                if (!shouldTrigger) continue;

                // 实例化效果并执行（效果内部可再做二次条件判断）
                var effect = entry.effectData.CreateRuntime();
                if (effect.CanExecute(effectContext))
                {
                    effect.Execute(effectContext);
                    Debug.Log($"[Combat] 受击部位效果触发: {effect.Description} " +
                              $"(外层条件: {entry.triggerCondition})");
                }

                if (context.IsAborted) break;
            }

            await UniTask.CompletedTask;
        }
    }
}
