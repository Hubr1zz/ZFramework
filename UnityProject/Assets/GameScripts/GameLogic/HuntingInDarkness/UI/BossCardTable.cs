using System.Collections.Generic;
using Core;
using GameplayBase;
using GameplayBase.CombatSystem;
using SO.Boss.ActionCard;
using SO.Boss.HitLocation;
using UnityEngine;

using Cards3D;

namespace UI
{
    /// <summary>
    /// Boss 受击部位卡展台。纯 C# 类，由 CardDisplayManager 构造并持有。
    /// 游戏开始时将所有受击部位卡背面朝上平铺；攻击时响应洗牌/翻牌事件更新视觉；
    /// 部位摧毁后永久正面朝上。
    /// </summary>
    public class BossCardTable
    {
        private readonly Transform                          _root;
        private readonly IBossState                         _bossState;
        private readonly IReadOnlyList<HitLocationRuntimeState> _hitLocationStates;

        private readonly List<BossHitLocationCard> _hitLocationCardViews = new();
        private readonly List<BossActionCard>      _actionCardViews      = new();

        public BossCardTable(
            Transform root,
            IBossState bossState,
            IReadOnlyList<HitLocationRuntimeState> hitLocationStates,
            IReadOnlyList<BossActionCardData> initialRevealedCards = null)
        {
            _root              = root;
            _bossState         = bossState;
            _hitLocationStates = hitLocationStates;

            BuildTableSurface();
            SpawnHitLocationCards();

            EventBus.Subscribe<BossDrawEvent>(OnBossDraw);
            EventBus.Subscribe<HitLocationShuffleStartedEvent>(OnShuffleStarted);
            EventBus.Subscribe<HitLocationFlippedFaceUpEvent>(OnHitLocationFlippedFaceUp);
            EventBus.Subscribe<HitLocationFlippedFaceDownEvent>(OnHitLocationFlippedFaceDown);
            EventBus.Subscribe<HitLocationDestroyedEvent>(OnHitLocationDestroyed);

            if (initialRevealedCards != null && initialRevealedCards.Count > 0)
            {
                var arr = new BossActionCardData[initialRevealedCards.Count];
                for (int i = 0; i < arr.Length; i++) arr[i] = initialRevealedCards[i];
                SpawnActionCards(arr);
            }
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<BossDrawEvent>(OnBossDraw);
            EventBus.Unsubscribe<HitLocationShuffleStartedEvent>(OnShuffleStarted);
            EventBus.Unsubscribe<HitLocationFlippedFaceUpEvent>(OnHitLocationFlippedFaceUp);
            EventBus.Unsubscribe<HitLocationFlippedFaceDownEvent>(OnHitLocationFlippedFaceDown);
            EventBus.Unsubscribe<HitLocationDestroyedEvent>(OnHitLocationDestroyed);
        }

        // ─── 构建 ──────────────────────────────────────────────────

        private void BuildTableSurface()
        {
            var planeGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            planeGo.name = "BossTablePlane";
            planeGo.transform.SetParent(_root, false);
            planeGo.transform.localScale = new Vector3(0.6f, 1f, 0.35f);
            UnityEngine.Object.Destroy(planeGo.GetComponent<Collider>());

            var rend = planeGo.GetComponent<Renderer>();
            rend.material = new Material(rend.sharedMaterial)
            {
                color = new Color(0.35f, 0.06f, 0.06f, 1f)
            };
        }

        private void SpawnHitLocationCards()
        {
            if (_hitLocationStates == null || _hitLocationStates.Count == 0) return;

            int   count   = _hitLocationStates.Count;
            float spacing = 0.88f;
            float startX  = -(count - 1) * spacing * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float x    = startX + i * spacing;
                var   view = BossHitLocationCard.Create(
                    _hitLocationStates[i], _root, new Vector3(x, 0.035f, 0f));
                _hitLocationCardViews.Add(view);
            }
        }

        // ─── 事件处理 ──────────────────────────────────────────────

        private void OnBossDraw(BossDrawEvent evt) => SpawnActionCards(evt.DrawnCards);

        private void SpawnActionCards(BossActionCardData[] cards)
        {
            foreach (var v in _actionCardViews)
                if (v != null) UnityEngine.Object.Destroy(v.gameObject);
            _actionCardViews.Clear();

            if (cards == null || cards.Length == 0) return;

            float spacing    = 0.88f;
            float totalWidth = (cards.Length - 1) * spacing;
            float startX     = -totalWidth * 0.5f;

            for (int i = 0; i < cards.Length; i++)
            {
                float x    = startX + i * spacing;
                var   view = BossActionCard.Create(cards[i], _root, new Vector3(x, 0.035f, 1.5f));
                _actionCardViews.Add(view);
            }
        }

        private void OnShuffleStarted(HitLocationShuffleStartedEvent evt) => ShuffleCardPositions();

        private void ShuffleCardPositions()
        {
            var positions = new List<Vector3>(_hitLocationCardViews.Count);
            foreach (var v in _hitLocationCardViews) positions.Add(v.BaseLocalPos);

            for (int i = positions.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (positions[i], positions[j]) = (positions[j], positions[i]);
            }

            for (int i = 0; i < _hitLocationCardViews.Count; i++)
                _hitLocationCardViews[i].MoveTo(positions[i]);
        }

        private void OnHitLocationFlippedFaceUp(HitLocationFlippedFaceUpEvent evt)
            => FindView(evt.CardData)?.Refresh();

        private void OnHitLocationFlippedFaceDown(HitLocationFlippedFaceDownEvent evt)
            => FindView(evt.CardData)?.Refresh();

        private void OnHitLocationDestroyed(HitLocationDestroyedEvent evt)
            => FindView(evt.CardData)?.Refresh();

        private BossHitLocationCard FindView(HitLocationCardData cardData)
            => _hitLocationCardViews.Find(v => v.CardData == cardData);
    }
}
