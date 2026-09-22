using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.CombatSystem;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.ActionFlow.Combat;
using SO.Boss.ActionCard;
using SO.Boss.HitLocation;
using UnityEngine;

namespace Core
{
    // ═══════════════════════════════════════════
    // Boss 行为控制器
    // ═══════════════════════════════════════════

    /// <summary>
    /// Boss 行为控制器。职责：
    /// 1. 管理Boss卡池
    /// 2. 按权重随机抽卡
    /// 3. 执行待执行行动卡
    /// 4. 累积战利品
    /// </summary>
    public class BossController
    {
        private readonly BossRuntimeData                    _bossData;
        private readonly List<BossActionCardData>           _cardPool;
        private readonly List<HitLocationRuntimeState>      _hitLocationRuntimeStates = new();

        private readonly IGameContext _gameContext;
        private readonly IBoardQuery  _boardQuery;
        private readonly IRandomSource _random;

        private readonly Dictionary<int, BossActionCardData> _cardRegistry = new();
        private int _nextCardId = 10000;
        private bool disposed;

        public BossActionCardData[] LastRevealedCards { get; private set; } = System.Array.Empty<BossActionCardData>();

        // ─── 战利品累积 ──────────────────────────────────────────────
        private readonly List<LootEntry>         _killLoot;
        private readonly Dictionary<string, int> _accumulatedLoot = new();

        public BossController(
            BossRuntimeData           bossData,
            List<BossActionCardData>  cardPool,
            List<HitLocationCardData> hitLocationCardPool,
            IGameContext              gameContext,
            IBoardQuery               boardQuery,
            List<LootEntry>           killLoot = null,
            IRandomSource             random = null)
        {
            _bossData    = bossData;
            _cardPool    = cardPool;
            _gameContext = gameContext;
            _boardQuery  = boardQuery;
            _killLoot    = killLoot ?? new List<LootEntry>();
            _random      = random ?? new SystemRandomSource();

            foreach (var data in hitLocationCardPool)
                if (data != null)
                    _hitLocationRuntimeStates.Add(new HitLocationRuntimeState(data));

            EventBus.Subscribe<HitLocationFlippedFaceUpEvent>(OnHitLocationFlippedFaceUp);
            EventBus.Subscribe<HitLocationDestroyedEvent>(OnHitLocationDestroyed);
            EventBus.Subscribe<ResourceDroppedEvent>(OnResourceDropped);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            EventBus.Unsubscribe<HitLocationFlippedFaceUpEvent>(OnHitLocationFlippedFaceUp);
            EventBus.Unsubscribe<HitLocationDestroyedEvent>(OnHitLocationDestroyed);
            EventBus.Unsubscribe<ResourceDroppedEvent>(OnResourceDropped);
        }

        public List<HitLocationRuntimeState> GetHitLocationRuntimeStates()
            => _hitLocationRuntimeStates;

        public List<BossActionRequest> GetPendingActionRequests()
        {
            var requests = new List<BossActionRequest>();
            if (disposed) return requests;
            foreach (int cardId in _bossData.PendingActionCardIds)
                if (_cardRegistry.TryGetValue(cardId, out BossActionCardData card) && card != null)
                    requests.Add(new BossActionRequest(cardId, card));
            return requests;
        }

        // ═══════════════════════════════════════════
        // 执行行动卡
        // ═══════════════════════════════════════════

        public async UniTask ExecutePendingActionsAsync()
        {
            foreach (var cardId in _bossData.PendingActionCardIds)
            {
                if (disposed) return;
                if (!_cardRegistry.TryGetValue(cardId, out var cardData)) continue;

                var context = new ActionCardContext
                {
                    SourceCharacterId = _bossData.Id,
                    TargetEntityId    = -1,
                    GameContext       = _gameContext,
                    BoardQuery        = _boardQuery
                };

                foreach (var effectData in cardData.effects)
                {
                    if (disposed) return;
                    var effect = effectData.CreateRuntime();
                    if (effect.CanExecute(context))
                        await effect.ExecuteAsync(context);
                    if (disposed) return;
                }

                EventBus.Publish(new BossActionExecutedEvent { ActionCardId = cardId });
                Debug.Log($"[Boss] 执行行动卡: {cardData.actionName} (时点消耗: {cardData.timePointCost})");
            }
        }

        // ═══════════════════════════════════════════
        // 抽卡
        // ═══════════════════════════════════════════

        public int DrawAndRevealNextActions(int drawCount = 1)
        {
            _bossData.PromoteRevealedToPending();

            var drawnIds   = new List<int>();
            var drawnCards = new List<BossActionCardData>();
            int totalTPCost = 0;

            for (int i = 0; i < drawCount; i++)
            {
                var card = DrawFromPool();
                if (card == null) break;

                int id = _nextCardId++;
                _cardRegistry[id] = card;
                drawnIds.Add(id);
                drawnCards.Add(card);
                totalTPCost += card.timePointCost;
            }

            _bossData.SetRevealedNext(drawnIds);
            LastRevealedCards = drawnCards.ToArray();

            EventBus.Publish(new BossDrawEvent { DrawnCards = LastRevealedCards });

            return totalTPCost > 0 ? totalTPCost : 1;
        }

        private BossActionCardData DrawFromPool()
        {
            var available = _cardPool.FindAll(c => c.alwaysAvailable);
            if (available.Count == 0) return null;

            List<BossActionCardData> drawn = WeightedSelection.DrawWithoutReplacement(
                available, 1, card => card.drawWeight, _random);
            return drawn.Count > 0 ? drawn[0] : null;
        }

        // ═══════════════════════════════════════════
        // 战利品系统
        // ═══════════════════════════════════════════

        public Dictionary<string, int> GetAndClearLoot()
        {
            var result = new Dictionary<string, int>(_accumulatedLoot);
            _accumulatedLoot.Clear();
            return result;
        }

        public void AccumulateDefeatLoot() => AccumulateLoot(_killLoot);

        private void AccumulateLoot(List<LootEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.resourceName)) continue;
                if (_random.NextDouble() > entry.chance) continue;
                _accumulatedLoot.TryGetValue(entry.resourceName, out int current);
                _accumulatedLoot[entry.resourceName] = current + entry.count;
            }
        }

        // ─── 事件处理器 ──────────────────────────────────────────────

        private void OnHitLocationFlippedFaceUp(HitLocationFlippedFaceUpEvent evt)
        {
            if (evt.CardData?.onHitLoot == null) return;
            AccumulateLoot(evt.CardData.onHitLoot);
        }

        private void OnHitLocationDestroyed(HitLocationDestroyedEvent evt)
        {
            if (evt.CardData?.onDestroyLoot == null) return;
            AccumulateLoot(evt.CardData.onDestroyLoot);
        }

        /// <summary>监听 DropResourceEffect 发布的资源掉落事件，直接累积</summary>
        private void OnResourceDropped(ResourceDroppedEvent evt)
        {
            if (string.IsNullOrEmpty(evt.ResourceName)) return;
            _accumulatedLoot.TryGetValue(evt.ResourceName, out int current);
            _accumulatedLoot[evt.ResourceName] = current + evt.Count;
            Debug.Log($"[BossController] 累积掉落: {evt.ResourceName} ×{evt.Count}" +
                      $"  来源: {evt.SourceHitLocationName}");
        }

    }
}
