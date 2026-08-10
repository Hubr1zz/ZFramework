using System.Collections.Generic;
using System.Linq;
using Cards3D;
using Core;
using GameplayBase;
using GameplayBase.CombatSystem;
using SO.Boss.ActionCard;
using SO.Boss.HitLocation;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Boss 3D 战斗面板（替换 BossCardTable）。
    ///
    /// 接口与 BossCardTable 完全兼容：相同构造参数 + Dispose()。
    ///
    /// 受击部位卡：维持原始手动布局（支持 HitLocationShuffleStartedEvent 随机洗牌）。
    /// 行动卡：使用 SlotGrid 自动排列（支持动态增减）。
    /// 所有卡牌均启用 EnableDrag，可鼠标悬停高亮。
    /// </summary>
    public class BossCombatPanel3D
    {
        // ─── 部位卡布局参数（与原 BossCardTable 一致）────────────────────────
        const float BodySpacing = 0.88f;
        const float CardY       = 0.035f;

        // ─── 行动卡槽参数 ─────────────────────────────────────────────────────
        const float ActionSlotW = CardView3D.CW + 0.06f;
        const float ActionSlotH = CardView3D.CH + 0.06f;
        const float ActionGap   = 0.10f;
        const float ActionZOffset = -(CardView3D.CH + 0.30f); // 在部位卡前方（世界Z负方向）

        // ─── 状态 ─────────────────────────────────────────────────────────────

        readonly Transform                           _root;
        readonly IBossState                          _bossState;
        readonly IReadOnlyList<HitLocationRuntimeState> _hitLocationStates;

        readonly List<BossHitLocationCard> _partViews   = new();
        readonly List<BossActionCard>      _actionViews = new();

        SlotGrid _actionGrid;

        // ─── 构造 ─────────────────────────────────────────────────────────────

        public BossCombatPanel3D(
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
            BuildActionGrid();

            EventBus.Subscribe<BossDrawEvent>(OnBossDraw);
            EventBus.Subscribe<HitLocationShuffleStartedEvent>(OnShuffleStarted);
            EventBus.Subscribe<HitLocationFlippedFaceUpEvent>(OnHitLocationFlippedFaceUp);
            EventBus.Subscribe<HitLocationFlippedFaceDownEvent>(OnHitLocationFlippedFaceDown);
            EventBus.Subscribe<HitLocationDestroyedEvent>(OnHitLocationDestroyed);

            // GameManager.Start() 可能在此之前已发布过 BossDrawEvent，补渲染
            if (initialRevealedCards != null && initialRevealedCards.Count > 0)
                SpawnActionCards(initialRevealedCards.ToArray());
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<BossDrawEvent>(OnBossDraw);
            EventBus.Unsubscribe<HitLocationShuffleStartedEvent>(OnShuffleStarted);
            EventBus.Unsubscribe<HitLocationFlippedFaceUpEvent>(OnHitLocationFlippedFaceUp);
            EventBus.Unsubscribe<HitLocationFlippedFaceDownEvent>(OnHitLocationFlippedFaceDown);
            EventBus.Unsubscribe<HitLocationDestroyedEvent>(OnHitLocationDestroyed);
        }

        // ─── 构建 ─────────────────────────────────────────────────────────────

        private void BuildTableSurface()
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "BossTablePlane";
            plane.transform.SetParent(_root, false);
            plane.transform.localScale = new Vector3(0.6f, 1f, 0.35f);
            Object.Destroy(plane.GetComponent<Collider>());
            var r = plane.GetComponent<Renderer>();
            r.material       = new Material(r.sharedMaterial);
            r.material.color = new Color(0.35f, 0.06f, 0.06f);
        }

        private void SpawnHitLocationCards()
        {
            if (_hitLocationStates == null || _hitLocationStates.Count == 0) return;

            int   count  = _hitLocationStates.Count;
            float startX = -(count - 1) * BodySpacing * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var pos  = new Vector3(startX + i * BodySpacing, CardY, 0f);
                var view = BossHitLocationCard.Create(_hitLocationStates[i], _root, pos);
                view.EnableDrag = true; // 可悬停查看，不影响游戏逻辑
                _partViews.Add(view);
            }
        }

        private void BuildActionGrid()
        {
            // 默认创建 3 列 1 行（最多同时展示 3 张行动卡）
            _actionGrid = SlotGrid.Create(
                _root,
                new Vector3(0f, CardY, ActionZOffset),
                3, 1,
                ActionSlotW, ActionSlotH, ActionGap,
                false,
                CardCategory.BossAction);
        }

        private void SpawnActionCards(BossActionCardData[] cards)
        {
            // 清除旧行动卡并重置槽
            foreach (var v in _actionViews)
                if (v != null) Object.Destroy(v.gameObject);
            _actionViews.Clear();
            foreach (var slot in _actionGrid.Slots) slot.ClearCard();

            if (cards == null || cards.Length == 0) return;

            // 若行动卡数量超过当前列数，重建网格
            if (cards.Length > _actionGrid.Columns)
            {
                Object.Destroy(_actionGrid.gameObject);
                _actionGrid = SlotGrid.Create(
                    _root,
                    new Vector3(0f, CardY, ActionZOffset),
                    cards.Length, 1,
                    ActionSlotW, ActionSlotH, ActionGap,
                    false,
                    CardCategory.BossAction);
            }

            foreach (var data in cards)
            {
                if (data == null) continue;
                var view = BossActionCard.Create(data, _root, Vector3.zero);
                view.ForceCategory(CardCategory.BossAction);
                view.EnableDrag = true;
                _actionGrid.TryPlaceCard(view);
                _actionViews.Add(view);
            }
        }

        // ─── 部位卡洗牌（原逻辑：随机交换位置）──────────────────────────────

        private void ShuffleCardPositions()
        {
            var positions = new List<Vector3>(_partViews.Count);
            foreach (var v in _partViews) positions.Add(v.BaseLocalPos);

            for (int i = positions.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (positions[i], positions[j]) = (positions[j], positions[i]);
            }

            for (int i = 0; i < _partViews.Count; i++)
                _partViews[i].MoveTo(positions[i]);
        }

        // ─── EventBus 回调 ────────────────────────────────────────────────────

        private void OnBossDraw(BossDrawEvent evt)      => SpawnActionCards(evt.DrawnCards);
        private void OnShuffleStarted(HitLocationShuffleStartedEvent _) => ShuffleCardPositions();

        private void OnHitLocationFlippedFaceUp(HitLocationFlippedFaceUpEvent evt)
            => FindPartView(evt.CardData)?.Refresh();

        private void OnHitLocationFlippedFaceDown(HitLocationFlippedFaceDownEvent evt)
            => FindPartView(evt.CardData)?.Refresh();

        private void OnHitLocationDestroyed(HitLocationDestroyedEvent evt)
            => FindPartView(evt.CardData)?.Refresh();

        private BossHitLocationCard FindPartView(HitLocationCardData data)
            => _partViews.Find(v => v.CardData == data);
    }
}
