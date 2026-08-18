using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using CardTactics.CombatSystem;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.Card.BossActionCard;
using GameplayBase.Card.Effect;
using GameplayBase.CombatSystem;
using HuntingInDarkness.Combat;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunters;
using SO.Boss.ActionCard;

namespace HuntingInDarkness.ActionFlow.Combat
{
    public readonly struct BossActionRequest
    {
        public int CardId { get; }
        public BossActionCardData Card { get; }

        public BossActionRequest(int cardId, BossActionCardData card)
        {
            CardId = cardId;
            Card = card;
        }
    }

    public readonly struct BossTurnCommandResult
    {
        public bool Success { get; }
        public string Reason { get; }
        public int ExecutedCardCount { get; }

        public BossTurnCommandResult(bool success, string reason, int executedCardCount)
        {
            Success = success;
            Reason = reason ?? string.Empty;
            ExecutedCardCount = Math.Max(0, executedCardCount);
        }
    }

    public interface IPlayableQueuedBossActionEffect
    {
        GameAction CreateAction(ActionCardContext context, ActionEventOutbox eventOutbox, IReactorEntity boss, IReactorEntity combat, Func<int, IReactorEntity> resolveTarget);
    }

    public interface IPlayableCancellableBossActionEffect
    {
        UniTask ExecuteAsync(ActionCardContext context, CancellationToken cancellationToken);
    }

    public sealed class ExecuteBossTurnAction : CompositeGameAction, ISourceAction, ITargetAction
    {
        private readonly IReadOnlyList<BossActionRequest> requests;
        private readonly IGameContext gameContext;
        private readonly IBoardQuery boardQuery;
        private readonly ActionEventOutbox eventOutbox;
        private readonly IReactorEntity boss;
        private readonly IReactorEntity combat;
        private readonly Func<int, IReactorEntity> resolveTarget;
        private int requestIndex;

        public ExecuteBossTurnAction(IReadOnlyList<BossActionRequest> requests, IGameContext gameContext, IBoardQuery boardQuery, ActionEventOutbox eventOutbox, IReactorEntity boss, IReactorEntity combat, Func<int, IReactorEntity> resolveTarget)
        {
            this.requests = requests ?? Array.Empty<BossActionRequest>();
            this.gameContext = gameContext ?? throw new ArgumentNullException(nameof(gameContext));
            this.boardQuery = boardQuery;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            this.boss = boss ?? throw new ArgumentNullException(nameof(boss));
            this.combat = combat ?? throw new ArgumentNullException(nameof(combat));
            this.resolveTarget = resolveTarget ?? throw new ArgumentNullException(nameof(resolveTarget));
        }

        public int ExecutedCardCount { get; private set; }
        public IReactorEntity Source => boss;
        public IReactorEntity Target => combat;

        protected override GameAction GetNextChild(CompositeExecutionContext context)
        {
            if (context.CompletedCount > 0 && context.LastOutcome.IsSuccess)
                ExecutedCardCount++;
            while (requestIndex < requests.Count)
            {
                BossActionRequest request = requests[requestIndex++];
                if (request.Card == null) continue;
                return new ExecuteBossCardAction(request, gameContext, boardQuery, eventOutbox, boss, combat, resolveTarget);
            }
            return null;
        }

        protected override ActionOutcome Resolve(CompositeExecutionContext context) => ActionOutcome.Success();
    }

    public sealed class ExecuteBossCardAction : CompositeGameAction, ISourceAction, ITargetAction
    {
        private readonly BossActionRequest request;
        private readonly ActionCardContext effectContext;
        private readonly ActionEventOutbox eventOutbox;
        private readonly IReactorEntity boss;
        private readonly IReactorEntity combat;
        private readonly Func<int, IReactorEntity> resolveTarget;
        private readonly List<BossActionCardEffect> effects = new();
        private int effectIndex;
        private bool completionScheduled;

        public ExecuteBossCardAction(BossActionRequest request, IGameContext gameContext, IBoardQuery boardQuery, ActionEventOutbox eventOutbox, IReactorEntity boss, IReactorEntity combat, Func<int, IReactorEntity> resolveTarget)
        {
            this.request = request;
            this.eventOutbox = eventOutbox;
            this.boss = boss;
            this.combat = combat;
            this.resolveTarget = resolveTarget;
            effectContext = new ActionCardContext
            {
                SourceCharacterId = gameContext.Boss?.Id ?? -1,
                TargetEntityId = -1,
                GameContext = gameContext,
                BoardQuery = boardQuery
            };
            foreach (BossActionCardEffectData effectData in request.Card.effects)
            {
                BossActionCardEffect effect = effectData?.CreateRuntime();
                if (effect != null)
                    effects.Add(effect);
            }
        }

        public int CardId => request.CardId;
        public BossActionCardData Card => request.Card;
        public IReactorEntity Source => boss;
        public IReactorEntity Target => combat;

        protected override GameAction GetNextChild(CompositeExecutionContext context)
        {
            while (effectIndex < effects.Count)
            {
                BossActionCardEffect effect = effects[effectIndex++];
                if (!effect.CanExecute(effectContext)) continue;
                if (effect is IPlayableQueuedBossActionEffect queuedEffect)
                {
                    GameAction action = queuedEffect.CreateAction(effectContext, eventOutbox, boss, combat, resolveTarget);
                    return action ?? new InvalidBossEffectAction(effect.Description, boss, combat);
                }
                return new ExecuteBossEffectAction(effect, effectContext, boss, combat);
            }

            if (completionScheduled) return null;
            completionScheduled = true;
            return new CompleteBossCardAction(request.CardId, eventOutbox, boss, combat);
        }

        protected override ActionOutcome Resolve(CompositeExecutionContext context)
        {
            if (!completionScheduled) return context.CompletedCount == 0 ? ActionOutcome.Failure("Boss 行动卡没有完成") : context.LastOutcome;
            return ActionOutcome.Success();
        }
    }

    public sealed class ExecuteBossEffectAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly BossActionCardEffect effect;
        private readonly ActionCardContext effectContext;

        internal ExecuteBossEffectAction(BossActionCardEffect effect, ActionCardContext effectContext, IReactorEntity source, IReactorEntity target)
        {
            this.effect = effect;
            this.effectContext = effectContext;
            Source = source;
            Target = target;
        }

        public override string DebugName => $"BossEffect:{effect.GetType().Name}";
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            if (effect is IPlayableCancellableBossActionEffect cancellableEffect)
                await context.AwaitPresentationAsync(cancellableEffect.ExecuteAsync(effectContext, cancellationToken));
            else
                await context.AwaitPresentationAsync(effect.ExecuteAsync(effectContext).AttachExternalCancellation(cancellationToken));
            return ActionOutcome.Success();
        }
    }

    public sealed class InvalidBossEffectAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly string effectName;

        internal InvalidBossEffectAction(string effectName, IReactorEntity source, IReactorEntity target)
        {
            this.effectName = effectName;
            Source = source;
            Target = target;
        }

        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }
        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken) => UniTask.FromResult(ActionOutcome.Failure($"无法创建 Boss 效果：{effectName}"));
    }

    public sealed class CompleteBossCardAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly int cardId;
        private readonly ActionEventOutbox eventOutbox;

        internal CompleteBossCardAction(int cardId, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.cardId = cardId;
            this.eventOutbox = eventOutbox;
            Source = source;
            Target = target;
        }

        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }
        public override ReactionPhases OpenReactionPhases => ReactionPhases.AfterResolved;

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            eventOutbox.Stage(new BossActionExecutedEvent { ActionCardId = cardId });
            eventOutbox.PublishCheckpoint();
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }

    public sealed class DirectedBossAttackAction : CompositeGameAction, ISourceAction, ITargetAction
    {
        private readonly ActionCardContext cardContext;
        private readonly string actionName;
        private readonly int woundCount;
        private readonly int accuracy;
        private readonly int attackCount;
        private readonly BossTargetPolicy targetPolicy;
        private readonly IRandomSource random;
        private readonly ActionEventOutbox eventOutbox;
        private readonly IReactorEntity boss;
        private readonly IReactorEntity combat;
        private readonly Func<int, IReactorEntity> resolveTarget;
        private SelectBossTargetAction selection;
        private bool attackScheduled;

        public DirectedBossAttackAction(ActionCardContext cardContext, string actionName, int woundCount, int accuracy, int attackCount, BossTargetPolicy targetPolicy, IRandomSource random, ActionEventOutbox eventOutbox, IReactorEntity boss, IReactorEntity combat, Func<int, IReactorEntity> resolveTarget)
        {
            this.cardContext = cardContext;
            this.actionName = actionName;
            this.woundCount = Math.Max(1, woundCount);
            this.accuracy = Math.Max(1, accuracy);
            this.attackCount = Math.Max(1, attackCount);
            this.targetPolicy = targetPolicy;
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.eventOutbox = eventOutbox;
            this.boss = boss;
            this.combat = combat;
            this.resolveTarget = resolveTarget;
        }

        public IReactorEntity Source => boss;
        public IReactorEntity Target => combat;

        protected override GameAction GetNextChild(CompositeExecutionContext context)
        {
            if (context.CompletedCount == 0)
            {
                selection = new SelectBossTargetAction(cardContext, actionName, targetPolicy, random, boss, combat);
                return selection;
            }
            if (!context.LastOutcome.IsSuccess || selection.TargetId < 0 || attackScheduled)
                return null;
            if (cardContext.GameContext is not ICombatProvider combatProvider || combatProvider.CombatManager == null || cardContext.GameContext is not ICombatRuntimeDataProvider combatData)
                return null;
            CharacterCombatStats defenderStats = combatData.GetCharacterData(selection.TargetId)?.CombatStats;
            if (defenderStats == null || defenderStats.IsDead) return null;

            attackScheduled = true;
            cardContext.TargetEntityId = selection.TargetId;
            return combatProvider.CombatManager.CreateBossAttackAction(selection.TargetId, defenderStats, woundCount, HunterBodyPart.Torso, accuracy, attackCount, eventOutbox, boss, resolveTarget(selection.TargetId));
        }

        protected override ActionOutcome Resolve(CompositeExecutionContext context) => context.CompletedCount == 0 ? ActionOutcome.Failure("Boss 攻击没有执行目标选择") : ActionOutcome.Success();
    }

    public sealed class SelectBossTargetAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly ActionCardContext cardContext;
        private readonly string actionName;
        private readonly BossTargetPolicy targetPolicy;
        private readonly IRandomSource random;

        internal SelectBossTargetAction(ActionCardContext cardContext, string actionName, BossTargetPolicy targetPolicy, IRandomSource random, IReactorEntity source, IReactorEntity target)
        {
            this.cardContext = cardContext;
            this.actionName = actionName;
            this.targetPolicy = targetPolicy;
            this.random = random;
            Source = source;
            Target = target;
        }

        public int TargetId { get; private set; } = -1;
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            if (cardContext.GameContext is not ICombatRuntimeDataProvider combatData) return ActionOutcome.Failure("战斗数据不可用");
            var candidates = new List<BossTargetCandidate>();
            foreach (ICharacterState character in cardContext.GameContext.PlayerCharacters ?? Array.Empty<ICharacterState>())
            {
                if (character == null) continue;
                CharacterCombatStats stats = combatData.GetCharacterData(character.Id)?.CombatStats;
                if (stats == null || stats.IsDead) continue;
                candidates.Add(new BossTargetCandidate(character.Id, GetDistance(character.Id), GetDamageTaken(stats.InjuryState)));
            }
            IPlayerInputProvider input = (cardContext.GameContext as ICombatProvider)?.CombatManager?.InputProvider;
            TargetId = await new PlayableBossTargetResolver(random).ResolveAsync(actionName, targetPolicy, candidates, input, cancellationToken);
            return ActionOutcome.Success();
        }

        private int GetDistance(int targetId)
        {
            if (targetPolicy != BossTargetPolicy.Nearest || cardContext.BoardQuery == null || cardContext.GameContext.Boss == null) return 0;
            try
            {
                UnityEngine.Vector2Int bossPosition = cardContext.BoardQuery.GetEntityPosition(cardContext.GameContext.Boss.Id);
                UnityEngine.Vector2Int targetPosition = cardContext.BoardQuery.GetEntityPosition(targetId);
                return Math.Max(0, cardContext.BoardQuery.GetDistance(bossPosition, targetPosition));
            }
            catch (KeyNotFoundException)
            {
                return int.MaxValue;
            }
        }

        private static int GetDamageTaken(HunterInjuryState injuryState)
        {
            int damage = 0;
            foreach (HunterBodyPart part in Enum.GetValues(typeof(HunterBodyPart)))
            {
                HunterBodyPartState state = injuryState.GetPart(part);
                damage += state.Definition.MaxHealth - state.CurrentHealth;
            }
            return damage;
        }
    }

    public sealed class BossAttackFlowAction : CompositeGameAction, ISourceAction, ITargetAction
    {
        private readonly BossAttackExecution execution;
        private int attemptIndex;
        private bool completionScheduled;

        public BossAttackFlowAction(AttackContext context, IPlayerInputProvider input, int woundCount, HunterBodyPart bodyPart, int accuracy, int attackCount, IRandomSource random, IArmorMitigationRule armorRule, IPermanentInjuryResolver permanentInjuryResolver, ISurvivalEventResolver survivalEventResolver, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            execution = new BossAttackExecution(context, input, woundCount, bodyPart, accuracy, attackCount, random, armorRule, permanentInjuryResolver, survivalEventResolver, eventOutbox, source, target);
        }

        public IReactorEntity Source => execution.Source;
        public IReactorEntity Target => execution.Target;

        protected override GameAction GetNextChild(CompositeExecutionContext context)
        {
            if (attemptIndex < execution.AttackCount && !execution.Context.DefenderStats.IsDead)
            {
                attemptIndex++;
                return new BossAttackAttemptAction(execution, attemptIndex);
            }
            if (completionScheduled) return null;
            completionScheduled = true;
            return new CompleteBossAttackAction(execution);
        }

        protected override ActionOutcome Resolve(CompositeExecutionContext context) => completionScheduled ? ActionOutcome.Success() : context.LastOutcome;
    }

    internal sealed class BossAttackExecution
    {
        public BossAttackExecution(AttackContext context, IPlayerInputProvider input, int woundCount, HunterBodyPart bodyPart, int accuracy, int attackCount, IRandomSource random, IArmorMitigationRule armorRule, IPermanentInjuryResolver permanentInjuryResolver, ISurvivalEventResolver survivalEventResolver, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            Context = context;
            Input = input;
            WoundCount = Math.Max(1, woundCount);
            BodyPart = bodyPart;
            Accuracy = Math.Max(1, accuracy);
            AttackCount = Math.Max(1, attackCount);
            Random = random;
            ArmorRule = armorRule;
            PermanentInjuryResolver = permanentInjuryResolver;
            SurvivalEventResolver = survivalEventResolver;
            EventOutbox = eventOutbox;
            Source = source;
            Target = target;
        }

        public AttackContext Context { get; }
        public IPlayerInputProvider Input { get; }
        public int WoundCount { get; }
        public HunterBodyPart BodyPart { get; }
        public int Accuracy { get; }
        public int AttackCount { get; }
        public IRandomSource Random { get; }
        public IArmorMitigationRule ArmorRule { get; }
        public IPermanentInjuryResolver PermanentInjuryResolver { get; }
        public ISurvivalEventResolver SurvivalEventResolver { get; }
        public ActionEventOutbox EventOutbox { get; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }
    }

    public sealed class BossAttackAttemptAction : CompositeGameAction, ISourceAction, ITargetAction
    {
        private readonly BossAttackExecution execution;
        private readonly BossAttackAttemptState attempt;
        private int phase;
        private GameAction lastAction;

        internal BossAttackAttemptAction(BossAttackExecution execution, int attemptIndex)
        {
            this.execution = execution;
            attempt = new BossAttackAttemptState(attemptIndex, execution.AttackCount);
        }

        public int AttemptIndex => attempt.Index;
        public IReactorEntity Source => execution.Source;
        public IReactorEntity Target => execution.Target;

        protected override GameAction GetNextChild(CompositeExecutionContext context)
        {
            ObserveLastOutcome(context);
            GameAction next = phase switch
            {
                0 => new BossHitCheckAction(execution, attempt),
                1 => new PrepareHunterWoundAction(execution, attempt),
                2 => new ApplyHunterWoundAction(execution, attempt),
                3 => new PresentHunterWoundAction(execution, attempt),
                4 => new ResolveHunterSurvivalEventAction(execution, attempt),
                _ => null
            };
            phase++;
            lastAction = next;
            return next;
        }

        protected override ActionOutcome Resolve(CompositeExecutionContext context) => ActionOutcome.Success();

        private void ObserveLastOutcome(CompositeExecutionContext context)
        {
            if (context.CompletedCount == 0 || context.LastOutcome.IsSuccess) return;
            if (lastAction is BossHitCheckAction)
                execution.Context.HitResult = HitResult.Failure;
            if (lastAction is PrepareHunterWoundAction || lastAction is ApplyHunterWoundAction)
                attempt.WoundPrevented = true;
        }
    }

    internal sealed class BossAttackAttemptState
    {
        public BossAttackAttemptState(int index, int count)
        {
            Index = index;
            Count = count;
        }

        public int Index { get; }
        public int Count { get; }
        public DeathDeckDrawOrder DeathDrawOrder { get; set; }
        public int DeathCardPosition { get; set; }
        public HunterDamageResult? Damage { get; set; }
        public bool WoundPrevented { get; set; }
    }

    public sealed class BossHitCheckAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly BossAttackExecution execution;
        private readonly BossAttackAttemptState attempt;

        internal BossHitCheckAction(BossAttackExecution execution, BossAttackAttemptState attempt)
        {
            this.execution = execution;
            this.attempt = attempt;
        }

        public IReactorEntity Source => execution.Source;
        public IReactorEntity Target => execution.Target;

        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            CharacterCombatStats stats = execution.Context.DefenderStats;
            if (stats == null || stats.IsDead) return ActionOutcome.Success("目标已经死亡");
            BossHitDeckComposition deck = BossHitDeckRules.Build(execution.Accuracy, stats.Evasion);
            if (deck.IsAutomaticHit)
            {
                execution.Context.RollResult = 0;
                execution.Context.HitResult = HitResult.Success;
                return ActionOutcome.Success();
            }

            string prompt = $"<b>Boss 命中牌堆</b> [{attempt.Index}/{attempt.Count}]\n怪物精准 {execution.Accuracy}：命中牌 {deck.HitCards} 张\n猎人敏捷 {stats.Evasion}：闪避牌 {deck.DodgeCards} 张\n闪避率 {100f * deck.DodgeCards / deck.TotalCards:0.#}%";
            int roll = execution.Input is IBossHitDeckInputProvider deckInput
                ? await deckInput.RequestDrawBossHitResult(prompt, deck, cancellationToken)
                : await execution.Input.RequestRoll(prompt, deck.TotalCards, cancellationToken);
            BossHitDeckDraw draw = BossHitDeckRules.ResolveDraw(deck, roll);
            execution.Context.RollResult = roll;
            execution.Context.HitResult = draw.IsHit ? HitResult.Success : HitResult.Failure;
            await context.AwaitPresentationAsync(execution.Input.ShowResult(draw.IsHit ? "未能闪避，被Boss命中！" : "闪避成功！躲开了Boss的攻击", cancellationToken));
            return ActionOutcome.Success();
        }
    }

    public sealed class PrepareHunterWoundAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly BossAttackExecution execution;
        private readonly BossAttackAttemptState attempt;

        internal PrepareHunterWoundAction(BossAttackExecution execution, BossAttackAttemptState attempt)
        {
            this.execution = execution;
            this.attempt = attempt;
        }

        public IReactorEntity Source => execution.Source;
        public IReactorEntity Target => execution.Target;

        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            CharacterCombatStats stats = execution.Context.DefenderStats;
            if (execution.Context.HitResult != HitResult.Success || stats == null || stats.IsDead) return ActionOutcome.Success();
            if (!stats.WillTriggerFatalInjury(execution.BodyPart, execution.WoundCount, execution.ArmorRule) || execution.Input is not IDeathDeckInputProvider deathInput)
                return ActionOutcome.Success();

            DeathDeck deck = stats.InjuryState.DeathDeck;
            var composition = new DeathDeckComposition(deck.SurvivalCardCount, deck.DeathCardCount);
            string partName = HunterBodyPartPresentation.GetName(execution.BodyPart);
            await context.AwaitPresentationAsync(execution.Input.ShowResult($"<b>死亡判定</b>\n\n这次伤害会击中已经归零的{partName}。\n当前牌堆：存活 {composition.SurvivalCards} 张 / 死亡 {composition.DeathCards} 张。\n\n确认后所有牌将翻至背面并洗混。", cancellationToken));
            attempt.DeathDrawOrder = deck.PrepareDraw(execution.Random);
            attempt.DeathCardPosition = await deathInput.RequestDrawDeathCard("<b>牌已洗混</b>\n选择一张背面牌并承担结果。", composition, cancellationToken);
            return ActionOutcome.Success();
        }
    }

    public sealed class ApplyHunterWoundAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly BossAttackExecution execution;
        private readonly BossAttackAttemptState attempt;

        internal ApplyHunterWoundAction(BossAttackExecution execution, BossAttackAttemptState attempt)
        {
            this.execution = execution;
            this.attempt = attempt;
        }

        public IReactorEntity Source => execution.Source;
        public IReactorEntity Target => execution.Target;

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            CharacterCombatStats stats = execution.Context.DefenderStats;
            if (attempt.WoundPrevented || execution.Context.HitResult != HitResult.Success || stats == null || stats.IsDead)
                return UniTask.FromResult(ActionOutcome.Success());

            HunterDamageResult damage = stats.ApplyDamage(execution.BodyPart, execution.WoundCount, execution.Random, execution.ArmorRule, execution.PermanentInjuryResolver, attempt.DeathDrawOrder, attempt.DeathCardPosition);
            attempt.Damage = damage;
            int permanentWoundsAdded = damage.PermanentInjury != null ? 1 : 0;
            if (permanentWoundsAdded > 0)
                stats.AddPermanentWounds(permanentWoundsAdded);
            if (damage.IsDead)
            {
                execution.EventOutbox.Stage(new CharacterDiedEvent { CharacterId = execution.Context.DefenderId });
            }
            else
            {
                execution.EventOutbox.Stage(new CharacterWoundedEvent
                {
                    CharacterId = execution.Context.DefenderId,
                    BodyPart = damage.BodyPart,
                    IncomingDamage = damage.IncomingDamage,
                    ArmorPrevented = damage.ArmorPrevented,
                    HealthLost = damage.HealthLost,
                    RemainingHealth = damage.RemainingHealth,
                    FatalInjuryTriggered = damage.FatalInjuryTriggered,
                    PermanentWoundsAdded = permanentWoundsAdded,
                    TotalTemporaryWounds = stats.TemporaryWounds,
                    TotalPermanentWounds = stats.PermanentWounds
                });
            }
            execution.EventOutbox.PublishCheckpoint();
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }

    public sealed class PresentHunterWoundAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly BossAttackExecution execution;
        private readonly BossAttackAttemptState attempt;

        internal PresentHunterWoundAction(BossAttackExecution execution, BossAttackAttemptState attempt)
        {
            this.execution = execution;
            this.attempt = attempt;
        }

        public IReactorEntity Source => execution.Source;
        public IReactorEntity Target => execution.Target;

        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            if (!attempt.Damage.HasValue) return ActionOutcome.Success();
            HunterDamageResult damage = attempt.Damage.Value;
            string message = $"{HunterBodyPartPresentation.GetName(damage.BodyPart)}受到 {damage.IncomingDamage} 点伤害，护甲抵消 {damage.ArmorPrevented}，剩余生命 {damage.RemainingHealth}。";
            if (damage.IsDead)
                message += "\n<color=#ff4444>翻开死亡牌：你失去希望。你死了。</color>";
            else if (damage.FatalInjuryTriggered)
            {
                message += "\n<color=#e8c46a>翻开存活牌！</color> 死亡牌堆加入 1 张死亡牌。";
                if (damage.PermanentInjury != null)
                    message += $"\n获得永久损伤：{damage.PermanentInjury.DisplayName}";
            }
            await context.AwaitPresentationAsync(execution.Input.ShowResult(message, cancellationToken));
            return ActionOutcome.Success();
        }
    }

    public sealed class ResolveHunterSurvivalEventAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly BossAttackExecution execution;
        private readonly BossAttackAttemptState attempt;

        internal ResolveHunterSurvivalEventAction(BossAttackExecution execution, BossAttackAttemptState attempt)
        {
            this.execution = execution;
            this.attempt = attempt;
        }

        public IReactorEntity Source => execution.Source;
        public IReactorEntity Target => execution.Target;

        protected override async UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            if (!attempt.Damage.HasValue || !attempt.Damage.Value.FatalInjuryTriggered || attempt.Damage.Value.IsDead || execution.SurvivalEventResolver == null)
                return ActionOutcome.Success();
            await execution.SurvivalEventResolver.ResolveAsync(execution.Context.DefenderId, attempt.Damage.Value, execution.Input, cancellationToken);
            return ActionOutcome.Success();
        }
    }

    public sealed class CompleteBossAttackAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly BossAttackExecution execution;

        internal CompleteBossAttackAction(BossAttackExecution execution)
        {
            this.execution = execution;
        }

        public IReactorEntity Source => execution.Source;
        public IReactorEntity Target => execution.Target;
        public override ReactionPhases OpenReactionPhases => ReactionPhases.AfterResolved;

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            execution.EventOutbox.Stage(new AttackCompletedEvent
            {
                AttackerId = execution.Context.AttackerId,
                DefenderId = execution.Context.DefenderId,
                AttackerIsBoss = true,
                Completed = true,
                AbortReason = string.Empty
            });
            execution.EventOutbox.PublishCheckpoint();
            return UniTask.FromResult(ActionOutcome.Success());
        }
    }

    internal static class HunterBodyPartPresentation
    {
        public static string GetName(HunterBodyPart bodyPart)
        {
            return bodyPart switch
            {
                HunterBodyPart.Head => "头部",
                HunterBodyPart.Torso => "躯干",
                HunterBodyPart.Arms => "手臂",
                HunterBodyPart.Legs => "腿部",
                _ => bodyPart.ToString()
            };
        }
    }
}
