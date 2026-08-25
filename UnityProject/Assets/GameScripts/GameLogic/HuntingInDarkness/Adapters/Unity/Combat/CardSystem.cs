using System;
using System.Collections.Generic;
using System.Linq;
using GameplayBase;
using GameplayBase.Card.CharacterActionCard;
using GameplayBase.CombatSystem.Cards.FlipConditions;
using HuntingInDarkness.Combat;
using HuntingInDarkness.GameCore.Cards;

namespace Core
{
    // ═══════════════════════════════════════════
    // 翻面/恢复条件评估器
    // ═══════════════════════════════════════════

    /// <summary>
    /// 集中评估所有卡牌的翻面和恢复条件。
    /// EventBus 只发布事实；跨卡联动由战斗 ActionEnvironment 显式调度。
    /// </summary>
    public class FlipConditionEvaluator : IDisposable
    {
        private readonly IGameContext _gameContext;
        // 运行时卡牌注册表
        private readonly Dictionary<int, CharacterActionCardInstance> _allCards = new();

        public FlipConditionEvaluator(IGameContext gameContext)
        {
            _gameContext = gameContext;
        }

        public void Dispose()
        {
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

        public IReadOnlyList<CharacterActionCardInstance> GetRegisteredCardsInStableOrder()
        {
            var cards = new List<CharacterActionCardInstance>(_allCards.Values);
            cards.Sort((left, right) => left.InstanceId.CompareTo(right.InstanceId));
            return cards;
        }

        public bool IsLinkedTransitionCandidate(CharacterActionCardInstance card, FlipTriggerTiming timing)
        {
            if (card == null) return false;
            if (card.CurrentFace == CardFace.FaceDown)
                return card.RestoreConditions.Exists(condition => condition.Timing == timing);
            return timing == FlipTriggerTiming.OnOtherCardDiscarded && card.FlipConditions.Exists(condition => condition.Timing == timing);
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
            if (TryApplyFlipAsCost(cardInstanceId, out CardFlippedEvent evt))
                EventBus.Publish(evt);
        }

        public bool TryApplyFlipAsCost(int cardInstanceId, out CardFlippedEvent evt)
        {
            evt = default;
            if (!_allCards.TryGetValue(cardInstanceId, out CharacterActionCardInstance card) || card.CurrentFace != CardFace.FaceUp) return false;
            card.SetFace(CardFace.FaceDown);
            evt = new CardFlippedEvent
            {
                CardInstanceId = card.InstanceId,
                OwnerCharacterId = card.OwnerCharacterId,
                OldFace = CardFace.FaceUp,
                NewFace = CardFace.FaceDown
            };
            return true;
        }

        public void ResetPerTurnAvailability()
        {
            foreach (CharacterActionCardInstance card in _allCards.Values)
                card.ResetForNewTurn();
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

        public bool TryApplyLinkedTransition(CharacterActionCardInstance card, FlipTriggerTiming timing, int triggerSourceCardId, out CardFlippedEvent? flippedEvent, out CardRestoredEvent? restoredEvent)
        {
            flippedEvent = null;
            restoredEvent = null;
            if (!IsLinkedTransitionCandidate(card, timing) || card.InstanceId == triggerSourceCardId) return false;
            var context = BuildContext(card, triggerSourceCardId);
            if (card.CurrentFace == CardFace.FaceDown)
            {
                List<IFlipCondition> conditions = card.RestoreConditions.FindAll(condition => condition.Timing == timing);
                if (conditions.Exists(condition => !condition.Evaluate(context))) return false;
                foreach (IFlipCondition condition in conditions)
                    condition.Consume(context);
                card.SetFace(CardFace.FaceUp);
                restoredEvent = new CardRestoredEvent { CardInstanceId = card.InstanceId, OwnerCharacterId = card.OwnerCharacterId };
                return true;
            }

            List<IFlipCondition> flipConditions = card.FlipConditions.FindAll(condition => condition.Timing == timing);
            if (flipConditions.Exists(condition => !condition.Evaluate(context))) return false;
            foreach (IFlipCondition condition in flipConditions)
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
