using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.CombatSystem;
using HuntingInDarkness.Combat;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.GameCore.Foundation;
using SO.Character;

namespace HuntingInDarkness.ActionFlow.Combat
{
    public sealed class LegacyCharacterAttackPipelineAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly AttackContext attackContext;
        private readonly AttackPipeline pipeline;
        private readonly IPlayerInputProvider input;
        private readonly ActionEventOutbox eventOutbox;

        public LegacyCharacterAttackPipelineAction(AttackContext attackContext, AttackPipeline pipeline, IPlayerInputProvider input, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.attackContext = attackContext ?? throw new ArgumentNullException(nameof(attackContext));
            this.pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            AttackResult result = await pipeline.Run(attackContext, input, cancellationToken);
            eventOutbox.Stage(new AttackCompletedEvent
            {
                AttackerId = attackContext.AttackerId,
                DefenderId = attackContext.DefenderId,
                AttackerIsBoss = false,
                Completed = result.Completed,
                AbortReason = result.AbortReason
            });
            return ActionOutcome.Success(result.Completed ? "攻击已结算" : result.AbortReason);
        }
    }

    /// <summary>玩家攻击的因果子树。每次判定、伤害、部位效果和胜负检查都是独立 Reactor 边界。</summary>
    public sealed class CharacterAttackFlowAction : CompositeGameAction, ISourceAction, ITargetAction
    {
        private readonly CharacterAttackExecution execution;
        private readonly List<GameAction> resolutionActions = new();
        private GameAction lastAction;
        private int resolutionIndex;
        private bool actionsBuilt;

        public CharacterAttackFlowAction(AttackContext attackContext, IPlayerInputProvider input, IHitLocationEffectResolver effectResolver, IRandomSource random, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            execution = new CharacterAttackExecution(attackContext, input, effectResolver, random, eventOutbox, source, target);
        }

        public AttackContext AttackContext => execution.Context;
        public bool Completed => !execution.IsAborted;
        public string AbortReason => execution.AbortReason;
        public IReactorEntity Source => execution.Source;
        public IReactorEntity Target => execution.Target;

        protected override GameAction GetNextChild(CompositeExecutionContext context)
        {
            ObserveLastOutcome(context);
            if (context.CompletedCount == 0) return SetNext(new DrawHitLocationsAction(execution));
            if (context.CompletedCount == 1 && !execution.IsAborted) return SetNext(new PrepareAttackResultDeckAction(execution));

            if (!actionsBuilt)
                BuildResolutionActions();

            if (execution.IsAborted)
                SkipToCleanup();

            if (resolutionIndex >= resolutionActions.Count) return null;
            return SetNext(resolutionActions[resolutionIndex++]);
        }

        protected override ActionOutcome Resolve(CompositeExecutionContext context)
        {
            if (context.CompletedCount == 0) return ActionOutcome.Failure("攻击流程没有执行任何步骤");
            if (lastAction is CompleteCharacterAttackAction && context.LastOutcome.IsSuccess)
                return ActionOutcome.Success(execution.IsAborted ? execution.AbortReason : "攻击已结算");
            return context.LastOutcome;
        }

        private GameAction SetNext(GameAction action)
        {
            lastAction = action;
            return action;
        }

        private void ObserveLastOutcome(CompositeExecutionContext context)
        {
            if (context.CompletedCount == 0 || lastAction == null) return;
            if (!context.LastOutcome.IsSuccess)
            {
                if (lastAction is ApplyHitLocationDamageAction damageAction)
                {
                    damageAction.ConvertPreventedDamageToFailure();
                    return;
                }
                if (lastAction is ResolveHitLocationEffectsAction || lastAction is ClaimBossDefeatAction || lastAction is HideHitLocationsAction)
                    return;
                execution.Abort(string.IsNullOrWhiteSpace(context.LastOutcome.Reason) ? $"{lastAction.DebugName} 未完成" : context.LastOutcome.Reason);
                return;
            }
            if (execution.Context.IsAborted)
                execution.Abort("受击部位效果中断了攻击");
        }

        private void BuildResolutionActions()
        {
            actionsBuilt = true;
            for (int index = 0; index < execution.Results.Count; index++)
            {
                var resolution = new HitResolutionState(index + 1, execution.Results.Count, execution.Results[index]);
                resolutionActions.Add(new SelectHitLocationAction(execution, resolution));
                resolutionActions.Add(new ApplyHitLocationDamageAction(execution, resolution));
                resolutionActions.Add(new ResolveHitLocationEffectsAction(execution, resolution));
                resolutionActions.Add(new PresentHitLocationResultAction(execution, resolution));
            }
            resolutionActions.Add(new HideHitLocationsAction(execution));
            resolutionActions.Add(new ClaimBossDefeatAction(execution));
            resolutionActions.Add(new CompleteCharacterAttackAction(execution));
        }

        private void SkipToCleanup()
        {
            while (resolutionIndex < resolutionActions.Count && resolutionActions[resolutionIndex] is not HideHitLocationsAction && resolutionActions[resolutionIndex] is not CompleteCharacterAttackAction)
                resolutionIndex++;
        }
    }

    internal sealed class CharacterAttackExecution
    {
        public CharacterAttackExecution(AttackContext context, IPlayerInputProvider input, IHitLocationEffectResolver effectResolver, IRandomSource random, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Input = input ?? throw new ArgumentNullException(nameof(input));
            EffectResolver = effectResolver ?? throw new ArgumentNullException(nameof(effectResolver));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            EventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public AttackContext Context { get; }
        public IPlayerInputProvider Input { get; }
        public IHitLocationEffectResolver EffectResolver { get; }
        public IRandomSource Random { get; }
        public ActionEventOutbox EventOutbox { get; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }
        public List<HitLocationRuntimeState> Remaining { get; } = new();
        public List<AttackResultCard> Results { get; } = new();
        public AttackResultDeckComposition Deck { get; set; }
        public bool IsAborted { get; private set; }
        public string AbortReason { get; private set; } = string.Empty;

        public void Abort(string reason)
        {
            IsAborted = true;
            AbortReason = string.IsNullOrWhiteSpace(reason) ? "攻击被中断" : reason;
            Context.IsAborted = true;
        }
    }

    internal sealed class HitResolutionState
    {
        public HitResolutionState(int index, int count, AttackResultCard resultCard)
        {
            Index = index;
            Count = count;
            ResultCard = resultCard;
        }

        public int Index { get; }
        public int Count { get; }
        public AttackResultCard ResultCard { get; }
        public HitLocationRuntimeState Selected { get; set; }
    }

    public sealed class DrawHitLocationsAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly CharacterAttackExecution execution;

        internal DrawHitLocationsAction(CharacterAttackExecution execution)
        {
            this.execution = execution;
        }

        public IReactorEntity Source => execution.Source;
        public IReactorEntity Target => execution.Target;

        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            int drawCount = Math.Max(1, execution.Context.AttackerStats?.Speed ?? 1);
            List<HitLocationRuntimeState> all = execution.Context.AllHitLocationStates ?? new List<HitLocationRuntimeState>();
            List<HitLocationRuntimeState> available = all.FindAll(state => state != null && !state.IsDestroyed);
            execution.Context.RevealedHitLocations = WeightedSelection.DrawWithoutReplacement(available, Math.Min(drawCount, available.Count), state => state.DomainState.Definition.DrawWeight, execution.Random);
            await context.AwaitPresentationAsync(execution.Input.PlayShuffleAndReveal(all, execution.Context.RevealedHitLocations, cancellationToken));
            return ActionOutcome.Success();
        }
    }

    public sealed class PrepareAttackResultDeckAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly CharacterAttackExecution execution;

        internal PrepareAttackResultDeckAction(CharacterAttackExecution execution)
        {
            this.execution = execution;
        }

        public IReactorEntity Source => execution.Source;
        public IReactorEntity Target => execution.Target;

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            execution.Context.CalculateAttackPower();
            execution.Remaining.Clear();
            execution.Remaining.AddRange(execution.Context.RevealedHitLocations ?? new List<HitLocationRuntimeState>());
            int strength = execution.Context.AttackerStats?.Strength ?? 0;
            int weaponPower = execution.Context.Weapon?.strengthBonus ?? 0;
            execution.Deck = AttackResultDeckRules.Build(strength, weaponPower, execution.Context.DefenderToughness);
            execution.Results.Clear();
            execution.Results.AddRange(AttackResultDeckRules.DrawBatch(execution.Deck, execution.Remaining.Count, execution.Random));
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }

    public sealed class SelectHitLocationAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly CharacterAttackExecution execution;
        private readonly HitResolutionState resolution;

        internal SelectHitLocationAction(CharacterAttackExecution execution, HitResolutionState resolution)
        {
            this.execution = execution;
            this.resolution = resolution;
        }

        public IReactorEntity Source => execution.Source;
        public IReactorEntity Target => execution.Target;

        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            if (execution.Remaining.Count == 0) return ActionOutcome.Failure("没有待分配的受击部位");
            if (execution.Input is IAttackResultBatchInputProvider batchInput)
            {
                float successRate = 100f * execution.Deck.SuccessCards / execution.Deck.TotalCards;
                string prompt = $"<b>攻击结果牌堆</b> [{resolution.Index}/{resolution.Count}]\nBoss韧性 {execution.Context.DefenderToughness} - 武器威力 {execution.Context.Weapon?.strengthBonus ?? 0}\n成功牌 {execution.Deck.SuccessCards} / 失败牌 {execution.Deck.FailureCards}  成功率 {successRate:0.#}%";
                await context.AwaitPresentationAsync(batchInput.RequestRevealAttackResult(prompt, cancellationToken));
            }

            string resultName = resolution.ResultCard == AttackResultCard.Success ? "成功" : "失败";
            HitLocationRuntimeState selected = await execution.Input.RequestSelectRevealedCard($"分配{resultName}结果 [{resolution.Index}/{resolution.Count}] — 选择部位", execution.Remaining, cancellationToken);
            if (selected == null || !execution.Remaining.Contains(selected)) return ActionOutcome.Cancelled("受击部位选择已取消");

            resolution.Selected = selected;
            execution.Context.CurrentHitLocation = selected.Data;
            execution.Context.RollResult = resolution.Index - 1;
            execution.Context.HitResult = resolution.ResultCard == AttackResultCard.Success ? HitResult.Success : HitResult.Failure;
            AttackCheck check = CombatRules.ResolveHitLocationAttack(execution.Context.TotalAttackPower, selected.DomainState.Definition.Toughness);
            execution.Context.IsCriticalHit = execution.Context.HitResult == HitResult.Success && check.IsCritical;
            return ActionOutcome.Success();
        }
    }

    public sealed class ApplyHitLocationDamageAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly CharacterAttackExecution execution;
        private readonly HitResolutionState resolution;

        internal ApplyHitLocationDamageAction(CharacterAttackExecution execution, HitResolutionState resolution)
        {
            this.execution = execution;
            this.resolution = resolution;
        }

        public HitLocationRuntimeState HitLocation => resolution.Selected;
        public IReactorEntity Source => execution.Source;
        public IReactorEntity Target => execution.Target;

        public void ConvertPreventedDamageToFailure()
        {
            execution.Context.HitResult = HitResult.Failure;
            execution.Context.IsCriticalHit = false;
        }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            if (resolution.Selected == null) return UniTask.FromResult(ActionOutcome.Failure("受击部位尚未选择"));
            if (execution.Context.HitResult != HitResult.Success) return UniTask.FromResult(ActionOutcome.Success("攻击未命中"));

            int appliedDamage = 0;
            if (execution.Context.GameContext?.Boss is IBossVitalityState vitality)
                appliedDamage = vitality.ApplyBossDamage(1);
            if (appliedDamage > 0)
                execution.EventOutbox.Stage(new EffectiveWeaponDamageEvent { CharacterId = execution.Context.AttackerId, WeaponName = execution.Context.Weapon?.weaponName });
            if (resolution.Selected.ApplyDamage(1))
            {
                execution.EventOutbox.Stage(new HitLocationDestroyedEvent
                {
                    CardData = resolution.Selected.Data,
                    PartName = resolution.Selected.Data.locationName
                });
            }
            execution.EventOutbox.PublishCheckpoint();
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }

    public sealed class ResolveHitLocationEffectsAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly CharacterAttackExecution execution;
        private readonly HitResolutionState resolution;

        internal ResolveHitLocationEffectsAction(CharacterAttackExecution execution, HitResolutionState resolution)
        {
            this.execution = execution;
            this.resolution = resolution;
        }

        public HitLocationRuntimeState HitLocation => resolution.Selected;
        public IReactorEntity Source => execution.Source;
        public IReactorEntity Target => execution.Target;

        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            if (resolution.Selected == null) return ActionOutcome.Failure("受击部位尚未选择");
            await execution.EffectResolver.ResolveHitLocationEffects(execution.Context, resolution.Selected, execution.Input, cancellationToken);
            return ActionOutcome.Success();
        }
    }

    public sealed class PresentHitLocationResultAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly CharacterAttackExecution execution;
        private readonly HitResolutionState resolution;

        internal PresentHitLocationResultAction(CharacterAttackExecution execution, HitResolutionState resolution)
        {
            this.execution = execution;
            this.resolution = resolution;
        }

        public IReactorEntity Source => execution.Source;
        public IReactorEntity Target => execution.Target;

        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            if (resolution.Selected == null) return ActionOutcome.Failure("受击部位尚未选择");
            string result = execution.Context.HitResult == HitResult.Success
                ? resolution.Selected.IsDestroyed
                    ? $"★ {resolution.Selected.Data.locationName} 已摧毁！"
                    : $"命中 {resolution.Selected.Data.locationName}！（剩余 HP: {resolution.Selected.CurrentHp}）{(execution.Context.IsCriticalHit ? " ★暴击" : string.Empty)}"
                : $"未能击穿 {resolution.Selected.Data.locationName}";
            if (execution.Context.GameContext?.Boss is IBossVitalityState vitality)
                result += $"\nBoss生命 {vitality.CurrentHealth}/{vitality.MaxHealth}";
            await context.AwaitPresentationAsync(execution.Input.ShowResult(result, cancellationToken));
            execution.Remaining.Remove(resolution.Selected);
            execution.Context.CurrentHitLocation = null;
            return ActionOutcome.Success();
        }
    }

    public sealed class HideHitLocationsAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly CharacterAttackExecution execution;

        internal HideHitLocationsAction(CharacterAttackExecution execution)
        {
            this.execution = execution;
        }

        public IReactorEntity Source => execution.Source;
        public IReactorEntity Target => execution.Target;

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            foreach (HitLocationRuntimeState state in execution.Context.RevealedHitLocations ?? new List<HitLocationRuntimeState>())
            {
                if (state == null || state.IsDestroyed) continue;
                state.Hide();
                execution.EventOutbox.Stage(new HitLocationFlippedFaceDownEvent { CardData = state.Data });
            }
            execution.EventOutbox.PublishCheckpoint();
            execution.Context.CurrentHitLocation = null;
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }

    public sealed class ClaimBossDefeatAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly CharacterAttackExecution execution;

        internal ClaimBossDefeatAction(CharacterAttackExecution execution)
        {
            this.execution = execution;
        }

        public IReactorEntity Source => execution.Source;
        public IReactorEntity Target => execution.Target;

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            if (execution.Context.GameContext?.Boss is IBossVitalityState vitality && vitality.TryClaimDefeat())
                execution.EventOutbox.StageAfterCommit(new BossDefeatedEvent());
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }

    public sealed class CompleteCharacterAttackAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly CharacterAttackExecution execution;

        internal CompleteCharacterAttackAction(CharacterAttackExecution execution)
        {
            this.execution = execution;
        }

        public IReactorEntity Source => execution.Source;
        public IReactorEntity Target => execution.Target;

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            execution.EventOutbox.Stage(new AttackCompletedEvent
            {
                AttackerId = execution.Context.AttackerId,
                DefenderId = execution.Context.DefenderId,
                AttackerIsBoss = false,
                Completed = !execution.IsAborted,
                AbortReason = execution.AbortReason
            });
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }
}
