using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.Card.BossActionCard;
using GameplayBase.Card.CharacterActionCard;
using GameplayBase.CombatSystem;
using GameplayBase.CombatSystem.Cards.FlipConditions;
using HuntingInDarkness.GameCore.Cards;
using HuntingInDarkness.Combat;
using SO.Boss.ActionCard;
using UI;
using UnityEngine;

namespace Core
{
    // ═══════════════════════════════════════════
    // 卡牌效果结算器
    // ═══════════════════════════════════════════

    /// <summary>
    /// 统一处理卡牌打出、效果执行的流水线。
    /// 职责：验证 → 扣时点 → 执行效果 → 翻面检查 → 发事件
    /// </summary>
    public class CardEffectResolver
    {
        private readonly FlipConditionEvaluator _flipEvaluator;
        private readonly IGameContext _gameContext;
        private readonly IBoardQuery _boardQuery;
        private readonly IBoardCommand _boardCommand;
        private readonly ActionCardCostService _costService;
        private readonly ActionQueueRunner _queueRunner = new();

        public CardEffectResolver(
            FlipConditionEvaluator flipEvaluator,
            IGameContext gameContext,
            IBoardQuery boardQuery,
            IBoardCommand boardCommand,
            ActionCardCostService costService)
        {
            _flipEvaluator = flipEvaluator;
            _gameContext = gameContext;
            _boardQuery = boardQuery;
            _boardCommand = boardCommand;
            _costService = costService ?? throw new ArgumentNullException(nameof(costService));
        }

        /// <summary>
        /// 尝试打出一张卡。返回是否成功。
        /// </summary>
        public async UniTask<bool> TryPlayCardAsync(
            CharacterActionCardInstance characterActionCard,
            int targetEntityId)
        {
            if (!ValidatePlay(characterActionCard))
                return false;

            var queue = new ActionQueue();
            var effectContext = new ActionCardContext
            {
                SourceCharacterId = characterActionCard.OwnerCharacterId,
                TargetEntityId = targetEntityId,
                GameContext = _gameContext,
                BoardQuery = _boardQuery,
                BoardCommand = _boardCommand,
                ActionQueue = queue
            };

            var effects = characterActionCard.CurrentFace == CardFace.FaceUp
                ? characterActionCard.FaceUpEffects
                : characterActionCard.FaceDownEffects;
            ActionCardCostTransaction transaction = null;

            queue.EnqueueBack(new DelegateActionQueueAction(
                "prepare-costs",
                async _ =>
                {
                    transaction = await _costService.PrepareAsync(characterActionCard);
                    return transaction == null
                        ? ActionQueueActionResult.Cancelled
                        : ActionQueueActionResult.Completed;
                }));

            PlayableActionPreparation.EnqueuePreparation(queue, effects, effectContext);

            queue.EnqueueBack(new DelegateActionQueueAction(
                "commit-costs",
                _ =>
                {
                    if (transaction != null && transaction.TryCommit(characterActionCard.OwnerCharacterId, _costService))
                        return UniTask.FromResult(ActionQueueActionResult.Completed);
                    PlayableActionPreparation.Reset(effects);
                    return UniTask.FromResult(ActionQueueActionResult.Failed);
                }));

            foreach (var effect in effects)
            {
                CharacterActionCardEffect queuedEffect = effect;
                queue.EnqueueBack(new DelegateActionQueueAction(
                    $"effect:{queuedEffect?.Description ?? "missing"}",
                    _ => PlayableActionPreparation.ExecuteAsync(queuedEffect, effectContext)));
            }

            queue.EnqueueBack(new DelegateActionQueueAction(
                "publish-and-update-card",
                _ =>
                {
                    characterActionCard.MarkUsed();
                    EventBus.Publish(new CardPlayedEvent
                    {
                        CardInstanceId = characterActionCard.InstanceId,
                        OwnerCharacterId = characterActionCard.OwnerCharacterId,
                        TimePointCost = characterActionCard.TimePointCost
                    });
                    _flipEvaluator.EvaluateAfterCardPlayed(
                        characterActionCard.InstanceId,
                        characterActionCard.OwnerCharacterId);
                    return UniTask.FromResult(ActionQueueActionResult.Completed);
                }));

            ActionQueueStatus status = await _queueRunner.RunAsync(queue);
            return status == ActionQueueStatus.Completed;
        }

        /// <summary>验证卡牌能否打出</summary>
        private bool ValidatePlay(CharacterActionCardInstance characterActionCard)
        {
            if (characterActionCard == null || !characterActionCard.CanPlay)
                return false;
            // 耗尽状态/时点由 TurnStateMachine.RequestPlayCard 校验。
            // 这里做目标/范围有效性校验：若某效果配置了 targeting 但当前没有任何合法目标格
            //   （如正前方越界），则不可打出。具体「目标格上是否有敌人」由各效果自身门控
            //   （见 CharacterAttackEffect），避免在此耦合效果语义。
            var effects = characterActionCard.CurrentFace == CardFace.FaceUp
                ? characterActionCard.FaceUpEffects
                : characterActionCard.FaceDownEffects;

            foreach (var effect in effects)
            {
                if (effect?.Targeting == null) continue;
                var tiles = effect.Targeting.GetValidTiles(_boardQuery, characterActionCard.OwnerCharacterId);
                if (tiles.Count == 0)
                {
                    Debug.Log($"[CardEffectResolver] 卡#{characterActionCard.InstanceId} 无合法目标格，无法打出。");
                    return false;
                }
            }
            return true;
        }
    }

    // ═══════════════════════════════════════════
    // 翻面/恢复条件评估器
    // ═══════════════════════════════════════════

    /// <summary>
    /// 集中评估所有卡牌的翻面和恢复条件。
    /// 监听事件，在合适时机触发条件检查。
    /// </summary>
    public class FlipConditionEvaluator : IDisposable
    {
        private readonly IGameContext _gameContext;
        private readonly ActionQueueRunner _queueRunner = new();
        // 运行时卡牌注册表
        private readonly Dictionary<int, CharacterActionCardInstance> _allCards = new();

        public FlipConditionEvaluator(IGameContext gameContext)
        {
            _gameContext = gameContext;

            // 订阅翻面事件 → 检查 OnOtherCardFlipped 联动
            EventBus.Subscribe<CardFlippedEvent>(OnCardFlipped);
            // 订阅恢复事件 → 检查 OnOtherCardRestored 联动
            EventBus.Subscribe<CardRestoredEvent>(OnCardRestored);
            // 订阅弃置事件 → 检查 OnOtherCardDiscarded 联动
            EventBus.Subscribe<CardDiscardedEvent>(OnCardDiscarded);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<CardFlippedEvent>(OnCardFlipped);
            EventBus.Unsubscribe<CardRestoredEvent>(OnCardRestored);
            EventBus.Unsubscribe<CardDiscardedEvent>(OnCardDiscarded);
            _allCards.Clear();
        }

        public void RegisterCard(CharacterActionCardInstance characterActionCard)
        {
            _allCards[characterActionCard.InstanceId] = characterActionCard;
        }

        public void UnregisterCard(int cardInstanceId)
        {
            _allCards.Remove(cardInstanceId);
        }

        public List<int> GetFlippableCostCandidates(
            int ownerCharacterId,
            int excludedCardId,
            string requiredTag,
            IReadOnlyCollection<int> excludedSelections)
        {
            var candidates = new List<int>();
            foreach (CharacterActionCardInstance card in _allCards.Values)
            {
                if (card.InstanceId == excludedCardId ||
                    card.OwnerCharacterId != ownerCharacterId ||
                    card.CurrentFace != CardFace.FaceUp ||
                    (excludedSelections != null && excludedSelections.Contains(card.InstanceId)) ||
                    (!string.IsNullOrWhiteSpace(requiredTag) &&
                     (card.Template.tags == null || !card.Template.tags.Contains(requiredTag))))
                    continue;
                candidates.Add(card.InstanceId);
            }
            return candidates;
        }

        public bool CanFlipAsCost(int cardInstanceId, int ownerCharacterId, string requiredTag)
        {
            if (!_allCards.TryGetValue(cardInstanceId, out CharacterActionCardInstance card))
                return false;
            return card.OwnerCharacterId == ownerCharacterId &&
                   card.CurrentFace == CardFace.FaceUp &&
                   (string.IsNullOrWhiteSpace(requiredTag) ||
                    (card.Template.tags != null && card.Template.tags.Contains(requiredTag)));
        }

        public void FlipAsCost(int cardInstanceId)
        {
            if (_allCards.TryGetValue(cardInstanceId, out CharacterActionCardInstance card) &&
                card.CurrentFace == CardFace.FaceUp)
                DoFlip(card);
        }

        public void ResetPerTurnAvailability()
        {
            foreach (CharacterActionCardInstance card in _allCards.Values)
                card.ResetForNewTurn();
        }

        /// <summary>
        /// 玩家主动点击恢复一张背面卡。
        /// 检查恢复条件 → 执行消耗 → 翻回正面。
        /// </summary>
        public async UniTask<bool> TryRestoreAsync(int cardInstanceId)
        {
            if (!_allCards.TryGetValue(cardInstanceId, out var card)) return false;
            if (card.CurrentFace != CardFace.FaceDown) return false;

            var context = BuildContext(card, triggerSource: null);
            List<IFlipCondition> manualConditions = card.RestoreConditions.FindAll(condition => condition.Timing == FlipTriggerTiming.OnPayCost);
            if (manualConditions.Count == 0) return false;
            foreach (IFlipCondition condition in manualConditions)
            {
                if (!condition.Evaluate(context))
                    return false;
            }

            var queue = new ActionQueue();
            queue.EnqueueBack(new DelegateActionQueueAction(
                "consume-restore-costs",
                _ =>
                {
                    foreach (IFlipCondition condition in manualConditions)
                        condition.Consume(context);
                    return UniTask.FromResult(ActionQueueActionResult.Completed);
                }));
            queue.EnqueueBack(new DelegateActionQueueAction(
                "restore-card",
                _ =>
                {
                    DoRestore(card);
                    return UniTask.FromResult(ActionQueueActionResult.Completed);
                }));

            return await _queueRunner.RunAsync(queue) == ActionQueueStatus.Completed;
        }

        public bool CanManuallyRestore(CharacterActionCardInstance card)
        {
            if (card == null || card.CurrentFace != CardFace.FaceDown) return false;
            var context = BuildContext(card, triggerSource: null);
            return CardConditionRules.AllMatchingConditionsPass(card.RestoreConditions, FlipTriggerTiming.OnPayCost, condition => condition.Timing, condition => condition.Evaluate(context));
        }

        public bool TryApplyManualRestore(CharacterActionCardInstance card, out CardRestoredEvent evt)
        {
            evt = default;
            if (!CanManuallyRestore(card)) return false;
            var context = BuildContext(card, triggerSource: null);
            foreach (IFlipCondition condition in card.RestoreConditions)
                if (condition.Timing == FlipTriggerTiming.OnPayCost)
                    condition.Consume(context);
            card.SetFace(CardFace.FaceUp);
            evt = new CardRestoredEvent { CardInstanceId = card.InstanceId, OwnerCharacterId = card.OwnerCharacterId };
            return true;
        }

        public bool TryApplyTurnStartTransition(CharacterActionCardInstance card, out CardFlippedEvent? flippedEvent, out CardRestoredEvent? restoredEvent)
        {
            flippedEvent = null;
            restoredEvent = null;
            if (card == null) return false;
            var context = BuildContext(card, triggerSource: null);
            if (card.CurrentFace == CardFace.FaceUp)
            {
                List<IFlipCondition> conditions = card.FlipConditions.FindAll(condition => condition.Timing == FlipTriggerTiming.OnTurnEnd);
                if (conditions.Count == 0 || conditions.Exists(condition => !condition.Evaluate(context))) return false;
                foreach (IFlipCondition condition in conditions)
                    condition.Consume(context);
                card.SetFace(CardFace.FaceDown);
                flippedEvent = new CardFlippedEvent
                {
                    CardInstanceId = card.InstanceId,
                    OwnerCharacterId = card.OwnerCharacterId,
                    OldFace = CardFace.FaceUp,
                    NewFace = CardFace.FaceDown
                };
                return true;
            }

            List<IFlipCondition> restoreConditions = card.RestoreConditions.FindAll(condition => condition.Timing == FlipTriggerTiming.OnTurnEnd);
            if (restoreConditions.Count == 0 || restoreConditions.Exists(condition => !condition.Evaluate(context))) return false;
            foreach (IFlipCondition condition in restoreConditions)
                condition.Consume(context);
            card.SetFace(CardFace.FaceUp);
            restoredEvent = new CardRestoredEvent { CardInstanceId = card.InstanceId, OwnerCharacterId = card.OwnerCharacterId };
            return true;
        }

        /// <summary>卡牌打出后，检查是否有翻面触发</summary>
        public void EvaluateAfterCardPlayed(int playedCardId, int ownerCharacterId)
        {
            if (TryApplyAfterCardPlayed(playedCardId, ownerCharacterId, out CardFlippedEvent evt))
                EventBus.Publish(evt);
        }

        /// <summary>提交卡牌状态但不发布事实，供 Action Outbox 保持事件提交顺序。</summary>
        public bool TryApplyAfterCardPlayed(int playedCardId, int ownerCharacterId, out CardFlippedEvent evt)
        {
            evt = default;
            if (!_allCards.TryGetValue(playedCardId, out CharacterActionCardInstance playedCard)) return false;
            if (playedCard.OwnerCharacterId != ownerCharacterId || playedCard.CurrentFace != CardFace.FaceUp) return false;
            if (!CheckFlipConditions(playedCard, FlipTriggerTiming.OnPlay, triggerSource: playedCardId)) return false;

            CardFace oldFace = playedCard.CurrentFace;
            playedCard.SetFace(CardFace.FaceDown);
            evt = new CardFlippedEvent
            {
                CardInstanceId = playedCard.InstanceId,
                OwnerCharacterId = playedCard.OwnerCharacterId,
                OldFace = oldFace,
                NewFace = CardFace.FaceDown
            };
            return true;
        }

        /// <summary>
        /// 玩家右键弃置一张正面卡：不触发正面效果，获得资源，然后翻面。
        /// 返回弃置结果供外部结算资源。
        /// </summary>
        public async UniTask<DiscardResult> TryDiscardForRewardAsync(int cardInstanceId)
        {
            if (!_allCards.TryGetValue(cardInstanceId, out var card))
                return DiscardResult.Failed;

            if (card.CurrentFace != CardFace.FaceUp)
                return DiscardResult.Failed;

            var reward = card.BurstReward;
            if (reward == null || !card.Template.IsDiscardable)
                return DiscardResult.Failed;

            // 弃置校验留空：TurnStateMachine.RequestDiscardCard 负责回合阶段限制。
            // 若将来需要额外条件（如"仅玩家回合可弃"、"每回合最多弃1次"），在此处添加。

            var queue = new ActionQueue();
            var effectContext = new ActionCardContext
            {
                SourceCharacterId = card.OwnerCharacterId,
                TargetEntityId = card.OwnerCharacterId,
                GameContext = _gameContext,
                BoardQuery = null,
                BoardCommand = null,
                ActionQueue = queue
            };

            if (reward.bonusEffects != null)
            {
                foreach (var effectData in reward.bonusEffects)
                {
                    var effect = effectData.CreateRuntime();
                    CharacterActionCardEffect queuedEffect = effect;
                    queue.EnqueueBack(new DelegateActionQueueAction(
                        $"burst-effect:{queuedEffect?.Description ?? "missing"}",
                        async _ =>
                        {
                            if (queuedEffect == null || !queuedEffect.CanExecute(effectContext))
                                return ActionQueueActionResult.Completed;
                            await queuedEffect.ExecuteAsync(effectContext);
                            return ActionQueueActionResult.Completed;
                        }));
                }
            }

            queue.EnqueueBack(new DelegateActionQueueAction(
                "burst-flip-and-publish",
                _ =>
                {
                    DoFlip(card);
                    EventBus.Publish(new CardDiscardedEvent
                    {
                        CardInstanceId = card.InstanceId,
                        OwnerCharacterId = card.OwnerCharacterId,
                        CurrencyReward = reward.currencyReward,
                        TimePointReward = reward.timePointReward
                    });
                    return UniTask.FromResult(ActionQueueActionResult.Completed);
                }));

            if (await _queueRunner.RunAsync(queue) != ActionQueueStatus.Completed)
                return DiscardResult.Failed;

            return new DiscardResult
            {
                Success = true,
                CardInstanceId = card.InstanceId,
                OwnerCharacterId = card.OwnerCharacterId,
                CurrencyReward = reward.currencyReward,
                TimePointReward = reward.timePointReward
            };
        }

        // ─── 事件回调 ───

        private void OnCardFlipped(CardFlippedEvent evt)
        {
            // 其他卡翻面 → 检查有没有联动恢复/翻面
            foreach (var card in _allCards.Values)
            {
                if (card.InstanceId == evt.CardInstanceId) continue; // 跳过自己

                if (card.CurrentFace == CardFace.FaceDown)
                {
                    if (CheckRestoreConditions(card, FlipTriggerTiming.OnOtherCardFlipped, evt.CardInstanceId))
                        DoRestore(card);
                }
            }
        }

        private void OnCardRestored(CardRestoredEvent evt)
        {
            foreach (var card in _allCards.Values)
            {
                if (card.InstanceId == evt.CardInstanceId) continue;

                // 例如："当恢复两张其他卡时，恢复自身"
                if (card.CurrentFace == CardFace.FaceDown)
                {
                    if (CheckRestoreConditions(card, FlipTriggerTiming.OnOtherCardRestored, evt.CardInstanceId))
                        DoRestore(card);
                }
            }
        }

        private void OnCardDiscarded(CardDiscardedEvent evt)
        {
            // 其他卡被弃置 → 检查有没有 OnOtherCardDiscarded 联动
            foreach (var card in _allCards.Values)
            {
                if (card.InstanceId == evt.CardInstanceId) continue;

                if (card.CurrentFace == CardFace.FaceDown)
                {
                    if (CheckRestoreConditions(card, FlipTriggerTiming.OnOtherCardDiscarded, evt.CardInstanceId))
                        DoRestore(card);
                }
                else
                {
                    if (CheckFlipConditions(card, FlipTriggerTiming.OnOtherCardDiscarded, evt.CardInstanceId))
                        DoFlip(card);
                }
            }
        }

        // ─── 内部工具 ───

        private bool CheckFlipConditions(CharacterActionCardInstance characterActionCard, FlipTriggerTiming timing, int? triggerSource)
        {
            var context = BuildContext(characterActionCard, triggerSource);
            return CardConditionRules.AllMatchingConditionsPass(
                characterActionCard.FlipConditions,
                timing,
                condition => condition.Timing,
                condition => condition.Evaluate(context));
        }

        private bool CheckRestoreConditions(CharacterActionCardInstance characterActionCard, FlipTriggerTiming timing, int? triggerSource)
        {
            var context = BuildContext(characterActionCard, triggerSource);
            return CardConditionRules.AllMatchingConditionsPass(
                characterActionCard.RestoreConditions,
                timing,
                condition => condition.Timing,
                condition => condition.Evaluate(context));
        }

        private void DoFlip(CharacterActionCardInstance characterActionCard)
        {
            var oldFace = characterActionCard.CurrentFace;
            characterActionCard.SetFace(CardFace.FaceDown);
            EventBus.Publish(new CardFlippedEvent
            {
                CardInstanceId = characterActionCard.InstanceId,
                OwnerCharacterId = characterActionCard.OwnerCharacterId,
                OldFace = oldFace,
                NewFace = CardFace.FaceDown
            });
        }

        private void DoRestore(CharacterActionCardInstance characterActionCard)
        {
            characterActionCard.SetFace(CardFace.FaceUp);
            EventBus.Publish(new CardRestoredEvent
            {
                CardInstanceId = characterActionCard.InstanceId,
                OwnerCharacterId = characterActionCard.OwnerCharacterId
            });
        }

        private FlipConditionContext BuildContext(CharacterActionCardInstance characterActionCard, int? triggerSource)
        {
            return new FlipConditionContext
            {
                CardInstanceId = characterActionCard.InstanceId,
                OwnerCharacterId = characterActionCard.OwnerCharacterId,
                GameContext = _gameContext,
                TriggerSourceCardId = triggerSource
            };
        }
    }

    // ═══════════════════════════════════════════
    // 弃置结果
    // ═══════════════════════════════════════════

    /// <summary>弃置操作的结果，供 GameManager 结算资源用</summary>
    public struct DiscardResult
    {
        public bool Success;
        public int CardInstanceId;
        public int OwnerCharacterId;
        public int CurrencyReward;
        public int TimePointReward;

        public static readonly DiscardResult Failed = new() { Success = false };
    }
}
