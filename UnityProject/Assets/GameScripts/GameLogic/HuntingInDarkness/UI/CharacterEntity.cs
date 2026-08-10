using System;
using System.Collections.Generic;
using Cards3D;
using Core;
using GameplayBase;
using GameplayBase.Board;
using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 角色实体（Prefab 根脚本）。把原先程序化生成的「胶囊 + TP 标签 + 三区域面板」
    /// 收敛到一个可在编辑器里拖引用、调位置/尺寸的 Prefab 上。
    ///
    /// 由 <see cref="EntityCreator.CreateCharacterEntity"/> 实例化：
    ///   - Prefab 路径：手动连好下面所有 [SerializeField] 引用；
    ///   - 程序化回退：EntityCreator 代码建好占位件后赋值这些引用。
    ///
    /// 行为（订阅 EventBus 自刷新）：TP 变化 → 刷新标签；实体移动 → 跟随；翻牌/恢复/弃置 → 刷新卡面。
    /// 点击 → 回调 GameManager.OnSelectCharacter（保留 PlayerTurnState 校验，不直接发事件）。
    /// </summary>
    public class CharacterEntity : MonoBehaviour
    {
        [Header("视觉锚点")]
        [Tooltip("头部锚点：信息面板/标签可挂其下，按头部尺寸预调")]
        [SerializeField] private Transform headAnchor;
        [Tooltip("时点显示标签")]
        [SerializeField] private TextMeshProUGUI timePointLabel;
        [SerializeField] private Transform mindViewPoint;
        [SerializeField] private Transform infoViewPoint;
        
        [Header("信息面板")]
        [Tooltip("信息面板根，选中时显隐")]
        [SerializeField] private GameObject panelRoot;
        [Tooltip("思想区域（动态战斗资源，如战斗灵感）—— 资源系统未建模，先占位")]
        [SerializeField] private SlotGrid mindGrid;
        [Tooltip("行动区域（角色行动卡）")]
        [SerializeField] private SlotGrid actionGrid;
        [Tooltip("装备区域 —— 装备卡视图未建模，先占位")]
        [SerializeField] private SlotGrid equipGrid;

        [Header("交互")]
        [Tooltip("点击体；为空则取根上的 Collider")]
        [SerializeField] private Collider clickCollider;
        [SerializeField] private Collider headPanelTrigger;
        [SerializeField] private Collider infoPanelTrigger;
        // ─── 运行时 ───────────────────────────────────────────────────────────
        private int _entityId;
        private IGameContext _ctx;
        private Func<int, int> _getCurrentTP;
        private Func<int, int> _getTPLimit;
        private Collider _activeDetailTrigger;

        private readonly Dictionary<int, CharacterActionCard> _cardViews = new();

        private void Update()
        {
            if (_activeDetailTrigger != null &&
                (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)))
            {
                ExitDetailFocus();
            }
        }

        /// <summary>点击实体 → 选中（由 GameManager 注入 OnSelectCharacter）</summary>
        public Action<int> OnClicked;
        /// <summary>点击正面行动卡 → 打出（由 GameManager 注入 OnPlayCard）</summary>
        public Action<int> OnCardPlayRequested;

        public int EntityId => _entityId;
        public Vector3 PanelWorldPosition =>
            panelRoot != null ? panelRoot.transform.position :
            headAnchor != null ? headAnchor.position : transform.position;

        // 供 EntityCreator 程序化回退路径在 AddComponent 后注入引用
        public void BindReferences(
            Transform head, TextMeshProUGUI tpLabel, GameObject panel,
            SlotGrid thought, SlotGrid action, SlotGrid equip, Collider click)
        {
            headAnchor    = head;
            timePointLabel = tpLabel;
            panelRoot     = panel;
            mindGrid   = thought;
            actionGrid    = action;
            equipGrid     = equip;
            clickCollider = click;
        }

        // ─── 初始化 ───────────────────────────────────────────────────────────

        public void Init(int entityId, IGameContext ctx,
            Func<int, int> getCurrentTP, Func<int, int> getTPLimit)
        {
            _entityId     = entityId;
            _ctx          = ctx;
            _getCurrentTP = getCurrentTP;
            _getTPLimit   = getTPLimit;

            EnsureClickForwarder();
            EnsureDetailClickForwarders();

            EventBus.Subscribe<TimePointChangedEvent>(OnTimePointChanged);
            EventBus.Subscribe<EntityMovedEvent>(OnEntityMoved);
            EventBus.Subscribe<CardFlippedEvent>(OnCardFlipped);
            EventBus.Subscribe<CardRestoredEvent>(OnCardRestored);
            EventBus.Subscribe<CardDiscardedEvent>(OnCardDiscarded);

            FillActionCards();
            SetDetailChildColliders(headPanelTrigger, false);
            SetDetailChildColliders(infoPanelTrigger, false);
            RefreshTimePoint();
            HidePanel();
        }

        private void EnsureClickForwarder()
        {
            var col = clickCollider != null ? clickCollider : GetComponent<Collider>();
            if (col == null) return;

            var handler = col.GetComponent<EntityClickHandler>();
            if (handler == null) handler = col.gameObject.AddComponent<EntityClickHandler>();
            handler.EntityId  = _entityId;
            handler.OnClicked = id => OnClicked?.Invoke(id);
        }

        private void EnsureDetailClickForwarders()
        {
            EnsureDetailClickForwarder(headPanelTrigger, mindViewPoint);
            EnsureDetailClickForwarder(infoPanelTrigger, infoViewPoint);
        }

        private void EnsureDetailClickForwarder(Collider trigger, Transform viewPoint)
        {
            if (trigger == null || viewPoint == null) return;

            var handler = trigger.GetComponent<EntityClickHandler>();
            if (handler == null) handler = trigger.gameObject.AddComponent<EntityClickHandler>();
            handler.OnClicked = _ => ToggleDetailFocus(trigger, viewPoint);
        }

        private void ToggleDetailFocus(Collider trigger, Transform viewPoint)
        {
            if (panelRoot == null || !panelRoot.activeInHierarchy) return;

            if (_activeDetailTrigger == trigger)
            {
                ExitDetailFocus();
                return;
            }

            SetDetailChildColliders(_activeDetailTrigger, false);
            _activeDetailTrigger = trigger;
            SetDetailChildColliders(_activeDetailTrigger, true);

            EventBus.Publish(new CharacterDetailFocusChangedEvent
            {
                HasFocus = true,
                CameraWorldPosition = viewPoint.position,
                CameraWorldRotation = viewPoint.rotation
            });
        }

        private void ExitDetailFocus()
        {
            if (_activeDetailTrigger == null) return;

            SetDetailChildColliders(_activeDetailTrigger, false);
            _activeDetailTrigger = null;
            EventBus.Publish(new CharacterDetailFocusChangedEvent { HasFocus = false });
        }

        private static void SetDetailChildColliders(Collider trigger, bool enabled)
        {
            if (trigger == null) return;

            var colliders = trigger.GetComponentsInChildren<Collider>(true);
            foreach (var childCollider in colliders)
            {
                if (childCollider != trigger)
                    childCollider.enabled = enabled;
            }
        }

        // ─── 显隐 ─────────────────────────────────────────────────────────────

        public void ShowPanel()
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            RefreshAllCardVisuals();
        }

        public void HidePanel()
        {
            ExitDetailFocus();
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        // ─── 行动卡填充 ─────────────────────────────────────────────────────────

        private void FillActionCards()
        {
            foreach (var v in _cardViews.Values)
                if (v != null) Destroy(v.gameObject);
            _cardViews.Clear();

            if (actionGrid == null || _ctx == null) return;

            var cards = _ctx.GetCardsOf(_entityId);

            // 行动区域：列数沿用 prefab 配置，行数随卡数动态扩展（无上限）
            int cols = Mathf.Max(1, actionGrid.Columns);
            int rows = Mathf.Max(1, Mathf.CeilToInt((float)cards.Count / cols));
            if (actionGrid.Rows != rows)
            {
                actionGrid.Rows = rows;
                actionGrid.Build();
            }

            foreach (var state in cards)
            {
                var view = CharacterActionCard.Create(state, actionGrid.transform, Vector3.zero);
                view.ForceCategory(CardCategory.HunterAction);
                view.EnableDrag = false;             // 战斗面板内卡牌不可拖拽
                view.OnClicked += OnCardViewClicked;

                actionGrid.TryPlaceCard(view);
                _cardViews[state.InstanceId] = view;
            }
        }

        private void OnCardViewClicked(CardView3D card)
        {
            if (card is not CharacterActionCard actionCard) return;
            var state = _ctx.GetCard(actionCard.CardInstanceId);
            if (state == null) return;
            if (state.CurrentFace == CardFace.FaceUp)
                OnCardPlayRequested?.Invoke(state.InstanceId);
        }

        private void RefreshAllCardVisuals()
        {
            foreach (var kv in _cardViews)
            {
                var state = _ctx.GetCard(kv.Key);
                if (state != null) kv.Value.Refresh(state);
            }
        }

        private void RefreshCard(int cardInstanceId)
        {
            if (!_cardViews.TryGetValue(cardInstanceId, out var view)) return;
            var state = _ctx.GetCard(cardInstanceId);
            if (state != null) view.Refresh(state);
        }

        // ─── 时点标签 ───────────────────────────────────────────────────────────

        public void RefreshTimePoint()
        {
            if (timePointLabel == null || _getCurrentTP == null || _getTPLimit == null) return;

            int current   = _getCurrentTP(_entityId);
            int limit     = _getTPLimit(_entityId);
            int remaining = limit - current;

            if (limit <= 0)
            {
                timePointLabel.text  = "--";
                timePointLabel.color = Color.white;
                return;
            }

            timePointLabel.text  = $"{remaining}/{limit}";
            timePointLabel.color = remaining > 0
                ? Color.white
                : remaining == 0
                    ? new Color(1f, 0.85f, 0.1f)
                    : new Color(1f, 0.3f, 0.3f);
        }

        // ─── EventBus 回调 ──────────────────────────────────────────────────────

        private void OnTimePointChanged(TimePointChangedEvent evt)
        {
            if (!evt.IsBoss && evt.EntityId == _entityId) RefreshTimePoint();
        }

        private void OnEntityMoved(EntityMovedEvent evt)
        {
            if (evt.EntityId == _entityId && _ctx != null)
                transform.position = _ctx.GetEntityWorldPosition(_entityId);
        }

        private void OnCardFlipped(CardFlippedEvent evt)
        {
            if (evt.OwnerCharacterId == _entityId) RefreshCard(evt.CardInstanceId);
        }

        private void OnCardRestored(CardRestoredEvent evt)
        {
            if (evt.OwnerCharacterId == _entityId) RefreshCard(evt.CardInstanceId);
        }

        private void OnCardDiscarded(CardDiscardedEvent evt)
        {
            if (evt.OwnerCharacterId == _entityId) RefreshCard(evt.CardInstanceId);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<TimePointChangedEvent>(OnTimePointChanged);
            EventBus.Unsubscribe<EntityMovedEvent>(OnEntityMoved);
            EventBus.Unsubscribe<CardFlippedEvent>(OnCardFlipped);
            EventBus.Unsubscribe<CardRestoredEvent>(OnCardRestored);
            EventBus.Unsubscribe<CardDiscardedEvent>(OnCardDiscarded);
        }
    }
}
