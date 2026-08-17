using System.Collections.Generic;
using Core;
using GameplayBase;
using UnityEngine;

using Cards3D;

namespace UI
{
    /// <summary>
    /// 单个角色的卡牌展台。
    /// Setup() 后默认隐藏，点击角色时由 CardDisplayManager 调用 Show()。
    /// 订阅翻面/恢复/弃置事件，自动刷新对应卡牌视觉。
    /// </summary>
    public class CharacterCardTable : MonoBehaviour
    {
        [Header("布局")]
        [SerializeField] private float cardSpacing = 0.90f;

        private int _characterId;
        private IGameContext _gameContext;

        private readonly List<CharacterActionCard> _cardViews = new();

        public int OwnerId => _characterId;

        /// <summary>卡牌被点击时通知外部（由 CardDisplayManager 注入）</summary>
        public System.Action<int> OnCardPlayRequested;

        // ═══════════════════════════════════════════
        // 初始化
        // ═══════════════════════════════════════════

        public void Setup(int characterId, IGameContext gameContext)
        {
            _characterId = characterId;
            _gameContext  = gameContext;

            EventBus.Subscribe<CardFlippedEvent>(OnCardFlipped);
            EventBus.Subscribe<CardRestoredEvent>(OnCardRestored);
            EventBus.Subscribe<CardDiscardedEvent>(OnCardDiscarded);

            BuildCards();
            gameObject.SetActive(false);
        }

        // ═══════════════════════════════════════════
        // 显示 / 隐藏
        // ═══════════════════════════════════════════

        public void Show()
        {
            gameObject.SetActive(true);
            RefreshAll();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // ═══════════════════════════════════════════
        // 卡牌构建
        // ═══════════════════════════════════════════

        private void BuildCards()
        {
            foreach (var v in _cardViews)
                if (v != null) Destroy(v.gameObject);
            _cardViews.Clear();

            var cards = _gameContext.GetCardsOf(_characterId);
            float startX = -(cards.Count - 1) * cardSpacing * 0.5f;

            for (int i = 0; i < cards.Count; i++)
            {
                var localPos = new Vector3(startX + i * cardSpacing, 0f, 0f);
                var view = CharacterActionCard.Create(cards[i], transform, localPos);
                view.OnClicked += OnCardClicked;
                _cardViews.Add(view);
            }
        }

        private void RefreshAll()
        {
            var cards = _gameContext.GetCardsOf(_characterId);
            for (int i = 0; i < _cardViews.Count && i < cards.Count; i++)
                _cardViews[i].Refresh(cards[i]);
        }

        // ─── 卡牌点击 ───

        private void OnCardClicked(CardView3D view)
        {
            if (view is not CharacterActionCard actionCard) return;
            var cardState = _gameContext.GetCard(actionCard.CardInstanceId);
            if (cardState == null) return;

            if (cardState.CurrentFace == CardFace.FaceUp)
                OnCardPlayRequested?.Invoke(cardState.InstanceId);
        }

        // ═══════════════════════════════════════════
        // EventBus 回调
        // ═══════════════════════════════════════════

        private void OnCardFlipped(CardFlippedEvent evt)
        {
            if (evt.OwnerCharacterId != _characterId) return;
            RefreshCard(evt.CardInstanceId);
        }

        private void OnCardRestored(CardRestoredEvent evt)
        {
            if (evt.OwnerCharacterId != _characterId) return;
            RefreshCard(evt.CardInstanceId);
        }

        private void OnCardDiscarded(CardDiscardedEvent evt)
        {
            if (evt.OwnerCharacterId != _characterId) return;
            RefreshCard(evt.CardInstanceId);
        }

        private void RefreshCard(int cardInstanceId)
        {
            var view = _cardViews.Find(v => v.CardInstanceId == cardInstanceId);
            if (view == null) return;
            var state = _gameContext.GetCard(cardInstanceId);
            if (state != null) view.Refresh(state);
        }

        // ═══════════════════════════════════════════
        // 清理
        // ═══════════════════════════════════════════

        private void OnDestroy()
        {
            EventBus.Unsubscribe<CardFlippedEvent>(OnCardFlipped);
            EventBus.Unsubscribe<CardRestoredEvent>(OnCardRestored);
            EventBus.Unsubscribe<CardDiscardedEvent>(OnCardDiscarded);
        }
    }
}
