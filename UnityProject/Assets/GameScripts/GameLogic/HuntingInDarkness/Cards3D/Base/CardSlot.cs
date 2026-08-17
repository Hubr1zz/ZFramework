using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UI;
using UnityEngine;

namespace Cards3D
{
    /// <summary>卡堆中多张卡的偏移方向。</summary>
    public enum StackDirection
    {
        StackUp,     // 往上：卡堆变厚（Y 轴微抬 + 厚度 ghost）
        Right,  // 往右：每张新卡沿 +X 偏移（offset 可配置）
        Down,   // 往下：每张新卡沿 -Z 偏移（offset 可配置，俯视下即屏幕下方）
    }

    /// <summary>
    /// 统一卡槽。整合了原 CardSlot / ResizableCardSlot / StackableCardSlot 的全部功能。
    ///
    /// 非堆叠模式（Stackable = false）：
    ///   - 可容纳一张卡牌；等比缩放卡牌以适配槽尺寸
    ///   - 卡牌被拖走时自动清空；类别过滤
    ///
    /// 堆叠模式（Stackable = true）：
    ///   - 持有一叠卡牌；点击顶牌触发抽卡动画
    ///   - TargetSlot：抽出后飞向该槽（单目标）
    ///   - TargetGrid：抽出后飞向网格首个空槽（自动扩展）
    ///   - 无目标时飞向 TargetTransform 所在位置
    ///
    /// 运行时在 Inspector 修改 SlotW / SlotH 可即时预览边框变化（Odin OnValueChanged）。
    ///
    /// 重要：不要依赖 Awake 自动 Build。请始终通过工厂方法或在赋完属性后显式调用 Build()。
    /// </summary>
    public class CardSlot : MonoBehaviour
    {
        public const float DefaultStackRandomRotation = 0f;

        // ─── 尺寸（Odin OnValueChanged 运行时即时预览）───────────────────────

        [BoxGroup("尺寸"), LabelText("宽"), OnValueChanged("RebuildVisual")]
        public float SlotW = CardView3D.CW + 0.06f;

        [BoxGroup("尺寸"), LabelText("高"), OnValueChanged("RebuildVisual")]
        public float SlotH = CardView3D.CH + 0.06f;

        // ─── 类型过滤 ────────────────────────────────────────────────────────

        [BoxGroup("过滤"), LabelText("允许类别")]
        public CardCategory[] AcceptedCategories = { CardCategory.Any };

        // ─── 堆叠模式 ────────────────────────────────────────────────────────

        [BoxGroup("堆叠"), LabelText("可堆叠"), OnValueChanged("RebuildVisual")]
        public bool Stackable = false;

        [BoxGroup("堆叠"), LabelText("目标槽（单目标）"), ShowIf("Stackable")]
        public CardSlot TargetSlot;

        [BoxGroup("堆叠"), LabelText("目标网格（自动扩展）"), ShowIf("Stackable")]
        public SlotGrid TargetGrid;

        [BoxGroup("堆叠"), LabelText("目标位置（无槽/网格时）"), ShowIf("Stackable")]
        public Transform TargetTransform;

        [BoxGroup("堆叠"), LabelText("飞行时长（秒）"), ShowIf("Stackable")]
        public float AnimDuration = 0.35f;

        // ─── 堆叠方向 ────────────────────────────────────────────────────────

        [BoxGroup("堆叠"), LabelText("堆叠方向"), ShowIf("Stackable"),
         OnValueChanged("RebuildStackLayout")]
        public StackDirection StackDir = StackDirection.StackUp;

        [BoxGroup("堆叠"), LabelText("堆叠偏移（右/下方向）"),
         ShowIf("@Stackable && StackDir != StackDirection.StackUp"),
         OnValueChanged("RebuildStackLayout")]
        public float StackOffset = 0.30f;

        [BoxGroup("堆叠"), LabelText("厚度层随机旋转（角度）"),
         ShowIf("@Stackable && StackDir == StackDirection.StackUp"), Range(0f, 180f)]
        [SerializeField]
        private float _stackRandomRotation = DefaultStackRandomRotation;

        /// <summary>厚度 ghost 的完整随机旋转跨度；180 表示每层在 -90° 到 +90° 之间旋转。</summary>
        public float StackRandomRotation => Mathf.Clamp(_stackRandomRotation, 0f, 180f);

        // ─── 预览（Shift + 悬停展开 world space canvas）──────────────────────

        [BoxGroup("堆叠"), LabelText("可预览"), ShowIf("Stackable"),
         InfoBox("可预览：Shift+悬停列出堆内所有卡；不可预览：仅显示「无法查看」占位。")]
        public bool Previewable = true;

        [BoxGroup("堆叠"), LabelText("堆叠上限"), ShowIf("Stackable"), MinValue(1)]
        public int MaxStack = 99;

        [BoxGroup("堆叠"), LabelText("可拖出顶部卡"), ShowIf("Stackable")]
        public bool AllowDragOut = true;

        // 运行时状态（动态卡堆由合并生成）：
        //   IsDynamic=true 的卡堆减到 1 张会变回松散卡并销毁本槽；
        //   场景里配置的卡槽 IsDynamic=false，永不消失。
        [System.NonSerialized] public bool   IsDynamic = false;
        // 仅接受 StackKey 相同的卡（同种资源同堆）；null = 仅按类别过滤。
        [System.NonSerialized] public string RequiredStackKey = null;

        /// <summary>是否配置了抽卡目标（决定顶部卡是"点击抽取"还是"拖出"）。</summary>
        private bool HasDrawTarget => TargetSlot != null || TargetGrid != null || TargetTransform != null;

        // ─── 全局注册（用于 ResourceCard 拖拽检测）──────────────────────────
        public static readonly List<CardSlot> AllSlots = new();

        // ─── 颜色 ────────────────────────────────────────────────────────────
        static readonly Color ColNormal    = new(0.60f, 0.60f, 0.62f);
        static readonly Color ColHighlight = new(0.95f, 0.85f, 0.15f);
        static readonly Color ColOccupied  = new(0.40f, 0.55f, 0.40f);

        static MaterialPropertyBlock _mpb;
        static MaterialPropertyBlock Mpb => _mpb ??= new MaterialPropertyBlock();

        // ─── 工具 ────────────────────────────────────────────────────────────

        static void SafeDestroy(UnityEngine.Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        // ─── 事件 ────────────────────────────────────────────────────────────

        /// <summary>堆叠模式：每次成功抽出一张卡后触发。</summary>
        public event System.Action<CardView3D> OnCardDrawn;

        /// <summary>堆叠模式：每次有卡压入牌堆后触发（用于工坊素材槽判定配方）。</summary>
        public event System.Action<CardView3D> OnCardPushed;

        /// <summary>堆内所有卡（顶→底枚举），只读。供工坊配方判定使用。</summary>
        public IReadOnlyCollection<CardView3D> StackCards => _deck;

        // ─── 状态 ────────────────────────────────────────────────────────────

        /// <summary>非堆叠模式下当前占据此槽的卡牌。</summary>
        public CardView3D OccupantCard { get; private set; }

        readonly Stack<CardView3D> _deck   = new();
        readonly List<Renderer>    _ghosts = new();
        System.Random              _ghostRotationRandom;

        // 仅堆叠模式：当前订阅在顶部卡上的抽卡委托（用于精确 -= 解绑，避免覆盖外部订阅者）
        System.Action<CardView3D> _drawHandler;
        // 用于射线检测吸附的触发碰撞体（由 Build() 创建/更新）
        BoxCollider _triggerCol;
        public int StackCount => _deck.Count;

        Renderer[] _edges;
        bool       _built;
        bool       _animating;

        // ─── 预览状态 ────────────────────────────────────────────────────────
        StackPreviewPanel _preview;
        bool              _previewShown;
        // 复用列表，避免每帧 GC
        readonly List<string> _previewLines = new();

        // ─── 全局生命周期 ────────────────────────────────────────────────────

        private void Awake()
        {
            AllSlots.Add(this);
            // ⚠ 不在此处 Build()：AddComponent 会立即调用 Awake，
            // 此时外部属性（SlotW/SlotH 等）尚未赋值。
            // 请在赋完属性后显式调用 Build()，或使用 Create() 工厂方法。
        }

        private void OnDestroy()
        {
            AllSlots.Remove(this);
            // 预览面板挂在 root 上，需随卡槽一并清理
            if (_preview != null) Destroy(_preview.gameObject);
        }

        // ─── 工厂方法 ────────────────────────────────────────────────────────

        /// <summary>最简工厂（兼容旧 CardTableTestSetup 调用）：使用默认尺寸，非堆叠，接受所有类别。</summary>
        public static CardSlot Create(Transform parent, Vector3 worldPos)
            => Create(parent, worldPos, CardView3D.CW + 0.06f, CardView3D.CH + 0.06f,
                      false, CardCategory.Any);

        /// <summary>完整工厂方法。</summary>
        public static CardSlot Create(
            Transform parent, Vector3 worldPos,
            float slotW, float slotH,
            bool stackable = false,
            params CardCategory[] accepted)
        {
            var go = new GameObject(stackable ? "StackCardSlot" : "CardSlot");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;

            var slot = go.AddComponent<CardSlot>();
            // Awake 已执行（注册到 AllSlots），现在安全赋值
            slot.SlotW              = slotW;
            slot.SlotH              = slotH;
            slot.Stackable          = stackable;
            slot.AcceptedCategories = accepted.Length > 0 ? accepted : new[] { CardCategory.Any };
            slot.Build(); // 属性齐全后才构建视觉
            return slot;
        }

        // ─── 构建 & 重建 ─────────────────────────────────────────────────────

        /// <summary>（首次或强制）构建视觉。若已构建则先销毁旧边框。</summary>
        [Button("重建视觉"), BoxGroup("调试")]
        public void Build()
        {
            DestroyEdges();
            _built = true;
            _edges = CreateEdges();
            RebuildTrigger();
            SetEdgeColor(ColNormal);
        }

        private void RebuildTrigger()
        {
            if (_triggerCol == null)
                _triggerCol = gameObject.AddComponent<BoxCollider>();
            // Y 范围覆盖拖拽高度（BeginDrag 抬起约 0.15f）
            _triggerCol.size      = new Vector3(SlotW, 0.5f, SlotH);
            _triggerCol.center    = new Vector3(0f, 0.2f, 0f);
            _triggerCol.isTrigger = true;
        }

        /// <summary>Odin OnValueChanged 钩子：仅在运行时已构建时重建，避免初次 Inspector 绑定误触发。</summary>
        private void RebuildVisual()
        {
            if (!_built) return; // 尚未初始化时不自动重建
            Build();
        }

        private void DestroyEdges()
        {
            if (_edges == null) return;
            foreach (var r in _edges)
                if (r != null) SafeDestroy(r.gameObject);
            _edges = null;
        }

        private Renderer[] CreateEdges()
        {
            const float T = 0.028f;
            const float D = 0.010f;
            float y  = D * 0.5f + 0.001f;
            float hw = SlotW * 0.5f;
            float hh = SlotH * 0.5f;

            var r = new Renderer[4];
            r[0] = MakeEdge("EdgeTop",    new Vector3(0,    y,  hh), new Vector3(SlotW + T, D, T));
            r[1] = MakeEdge("EdgeBottom", new Vector3(0,    y, -hh), new Vector3(SlotW + T, D, T));
            r[2] = MakeEdge("EdgeLeft",   new Vector3(-hw,  y,   0), new Vector3(T, D, SlotH));
            r[3] = MakeEdge("EdgeRight",  new Vector3( hw,  y,   0), new Vector3(T, D, SlotH));
            return r;
        }

        private Renderer MakeEdge(string n, Vector3 lp, Vector3 sc)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = n;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = lp;
            go.transform.localScale    = sc;
            SafeDestroy(go.GetComponent<Collider>()); // 去掉碰撞体
            return go.GetComponent<Renderer>();
            // 颜色通过 MaterialPropertyBlock 设置，不实例化材质
        }

        private void SetEdgeColor(Color c)
        {
            if (_edges == null) return;
            // MaterialPropertyBlock：不实例化材质，编辑模式和运行时均安全
            Mpb.SetColor("_Color", c);
            Mpb.SetColor("_BaseColor", c); // URP 兼容
            foreach (var r in _edges)
                if (r != null) r.SetPropertyBlock(Mpb);
        }

        // ─── 非堆叠 API ──────────────────────────────────────────────────────

        /// <summary>
        /// 当前槽是否可接受此卡牌（空槽 + 类别匹配）。
        /// 堆叠模式下同样进行类别检查，但不检查是否满。
        /// </summary>
        public bool CanAccept(CardView3D card)
        {
            if (!Stackable && OccupantCard != null) return false;
            if (Stackable && _deck.Count >= MaxStack) return false;            // 堆叠上限
            if (Stackable && RequiredStackKey != null &&
                card.StackKey != RequiredStackKey) return false;               // 同种限制
            if (card.Category == CardCategory.Any) return true;
            foreach (var cat in AcceptedCategories)
                if (cat == CardCategory.Any || cat == card.Category) return true;
            return false;
        }

        /// <summary>
        /// 放置卡牌。
        /// 非堆叠：缩放卡牌以适配槽，位置对齐槽中心；订阅 DragStarted 事件。
        /// 堆叠：等同 PushCard。
        /// cardParent：用于计算卡牌本地坐标（null = 使用 card.transform.parent）。
        /// </summary>
        public void PlaceCard(CardView3D card, Transform cardParent = null)
        {
            if (Stackable) { PushCard(card); return; }

            OccupantCard     = card;
            card.CurrentSlot = this;
            card.EnableDrag  = true;
            card.DragStarted += OnCardDragStarted;

            // 等比缩放卡牌以填满槽（按该卡自身尺寸计算）
            float scale = Mathf.Min(SlotW / card.Width, SlotH / card.Height);
            card.transform.localScale = Vector3.one * scale;

            // 定位卡牌
            var parent = cardParent ?? card.transform.parent;
            if (parent != null)
            {
                var worldTarget = transform.position + Vector3.up * 0.013f;
                card.MoveTo(parent.InverseTransformPoint(worldTarget));
            }
            SetEdgeColor(ColOccupied);
        }

        /// <summary>清空槽（不修改卡牌状态）。</summary>
        public void ClearCard()
        {
            if (OccupantCard != null)
            {
                OccupantCard.DragStarted -= OnCardDragStarted;
                if (OccupantCard.CurrentSlot == this) OccupantCard.CurrentSlot = null;
                OccupantCard = null;
            }
            SetEdgeColor(ColNormal);
        }

        public void SetHighlight(bool on)
        {
            if (!Stackable && OccupantCard != null) return; // 有卡时不改色
            SetEdgeColor(on ? ColHighlight : ColNormal);
        }

        private void OnCardDragStarted(CardView3D card)
        {
            card.DragStarted -= OnCardDragStarted;
            if (card.CurrentSlot == this) card.CurrentSlot = null;
            OccupantCard = null;
            SetEdgeColor(ColNormal);
        }

        // ─── 堆叠 API ────────────────────────────────────────────────────────

        /// <summary>将卡牌压入牌堆顶部。</summary>
        public void PushCard(CardView3D card)
        {
            DisableTop(); // 旧顶降级为普通层

            card.transform.SetParent(transform, worldPositionStays: false);
            card.transform.localPosition = StackSlotLocalPos(_deck.Count);
            card.transform.localRotation = StackSlotRotation(_deck.Count);
            card.transform.localScale    = Vector3.one;

            _deck.Push(card);
            card.CurrentSlot = this;
            EnableTop();  // 新顶按 抽取/拖出 配置
            UpdateGhosts();
            OnCardPushed?.Invoke(card);
        }

        /// <summary>
        /// 调整之后新生成的厚度 ghost 的随机旋转跨度，不重随机已有 ghost。
        /// </summary>
        public void SetStackRandomRotation(float degrees)
        {
            _stackRandomRotation = Mathf.Clamp(degrees, 0f, 180f);
        }

        // ─── 顶部卡配置：抽取（有目标）或 拖出（无目标 + AllowDragOut）─────────

        private void DisableTop()
        {
            if (_deck.Count == 0) return;
            var t = _deck.Peek();
            if (_drawHandler != null)
            {
                t.OnClicked  -= _drawHandler;
                _drawHandler  = null;
            }
            t.EnableDrag  = false;
            t.DragStarted -= OnTopCardDragStarted;
        }

        private void EnableTop()
        {
            if (_deck.Count == 0) return;
            var t = _deck.Peek();
            if (HasDrawTarget)
            {
                _drawHandler  = _ => TryDrawTop();
                t.OnClicked  += _drawHandler;
                t.EnableDrag  = false;
            }
            else if (AllowDragOut)
            {
                _drawHandler  = null;
                t.EnableDrag  = true;
                t.DragStarted -= OnTopCardDragStarted; // 防重复订阅
                t.DragStarted += OnTopCardDragStarted;
            }
            else
            {
                _drawHandler  = null;
                t.EnableDrag  = false;
            }
        }

        /// <summary>顶部卡被拖出卡堆：弹出、转为松散卡，必要时回退动态卡堆。</summary>
        private void OnTopCardDragStarted(CardView3D card)
        {
            if (_deck.Count == 0 || _deck.Peek() != card) return;
            // 仅解绑事件；不要动 card.EnableDrag——它正处于拖拽中
            card.DragStarted -= OnTopCardDragStarted;
            _deck.Pop();
            card.CurrentSlot = null;
            card.SetDragHome(transform.parent != null ? transform.parent : transform.root);
            EnableTop();           // 配置新顶（不影响已弹出的 card）
            UpdateGhosts();
            CheckRevert();
        }

        /// <summary>动态卡堆减到 1 张时变回松散卡并销毁本槽；减到 0 直接销毁。</summary>
        private void CheckRevert()
        {
            if (!IsDynamic) return;

            if (_deck.Count == 1)
            {
                DisableTop();
                var last = _deck.Pop();
                last.CurrentSlot = null;
                var home = transform.parent != null ? transform.parent : transform.root;
                last.transform.SetParent(home, worldPositionStays: true);
                last.EnableDrag = true; // 变回可拖动/可再合并的松散卡
                Destroy(gameObject);
            }
            else if (_deck.Count == 0)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 以一张松散卡为基准创建动态卡堆（尺寸取该卡尺寸）。
        /// 用于两张同种松散卡拖拽合并。
        /// </summary>
        public static CardSlot CreateDynamicStack(CardView3D baseCard)
        {
            var parent = baseCard.transform.parent != null
                ? baseCard.transform.parent : baseCard.transform.root;
            var slot = Create(parent, baseCard.transform.position,
                baseCard.Width + 0.06f, baseCard.Height + 0.06f,
                stackable: true, baseCard.Category);
            slot.IsDynamic        = true;
            slot.RequiredStackKey = baseCard.StackKey;
            slot.AllowDragOut     = true;
            slot.PushCard(baseCard);
            return slot;
        }

        /// <summary>根据堆叠方向计算第 index 张卡（从 0 起）的本地坐标。</summary>
        private Vector3 StackSlotLocalPos(int index)
        {
            float step = StackDir == StackDirection.StackUp
                ? EntityCreator.DefaultCardThickness
                : StackOffset;

            float offset = index * step;

            return StackDir switch
            {
                StackDirection.Right => new Vector3(offset, 0f, index * 0.0005f),
                //StackDirection.Left  => new Vector3(-offset, 0f, index * 0.0005f),
                StackDirection.Down  => new Vector3(0f, 0f, -offset),
                //StackDirection.Up    => new Vector3(0f, 0f, offset),
                StackDirection.StackUp => new Vector3(0f, offset, 0f),
                _ => new Vector3(0f, offset, 0f),
            };
        }

        private Quaternion StackSlotRotation(int index)
        {
            const float angleStep = 2f;

            if (index == 0) return Quaternion.identity;
            return StackDir switch
            {
                StackDirection.Right => Quaternion.Euler(0f, index * angleStep, 0f),
                //StackDirection.Left  => Quaternion.Euler(0f, -index * angleStep, 0f),
                StackDirection.Down  => Quaternion.Euler(-index * angleStep, 0f, 0f),
                //StackDirection.Up    => Quaternion.Euler(index * angleStep, 0f, 0f),
                StackDirection.StackUp => Quaternion.identity,
                _ => Quaternion.identity,
            };
        }

        /// <summary>按当前堆叠方向重排已有卡（运行时改方向/偏移时调用）。</summary>
        private void RelayoutStack()
        {
            int idx = _deck.Count - 1; // 枚举顶→底，顶为最大 index
            foreach (var card in _deck)
            {
                if (card != null)
                {
                    card.transform.localPosition = StackSlotLocalPos(idx);
                    card.transform.localRotation = StackSlotRotation(idx);
                }
                idx--;
            }
        }

        private void RebuildStackLayout()
        {
            if (!Application.isPlaying) return;
            RelayoutStack();
            UpdateGhosts();
        }

        /// <summary>弹出并返回堆内全部卡（顶→底）。清空牌堆与厚度视觉。</summary>
        public List<CardView3D> TakeAll()
        {
            DisableTop(); // 已解绑顶部卡的 _drawHandler
            var list = new List<CardView3D>(_deck);
            foreach (var c in list)
                if (c != null) c.CurrentSlot = null;
            _deck.Clear();
            UpdateGhosts();
            return list;
        }

        /// <summary>取出顶部卡牌（不触发动画，直接返回）。</summary>
        public CardView3D DrawTop()
        {
            if (_deck.Count == 0) return null;
            DisableTop();
            var card = _deck.Pop();
            card.CurrentSlot = null;
            EnableTop();
            UpdateGhosts();
            return card;
        }

        private void TryDrawTop()
        {
            if (_animating || _deck.Count == 0) return;
            var card = DrawTop();
            DrawAndFlyAsync(card).Forget();
        }

        private async UniTaskVoid DrawAndFlyAsync(CardView3D card)
        {
            _animating = true;
            try
            {
                var cancellationToken = this.GetCancellationTokenOnDestroy();

                var cardRoot = transform.root; // 桌面根节点
                card.transform.SetParent(cardRoot, worldPositionStays: true);
                card.transform.localScale = Vector3.one;

            Vector3 start = card.transform.position + Vector3.up * 0.12f;
            card.transform.position = start;

            // 解析目标槽
            CardSlot targetSlot = null;
            if (TargetSlot != null)
                targetSlot = TargetSlot;
            else if (TargetGrid != null)
                targetSlot = TargetGrid.GetFirstEmptySlot();

            if (targetSlot != null)
            {
                Vector3 end  = targetSlot.transform.position + Vector3.up * 0.2f;
                Vector3 peak = (start + end) * 0.5f + Vector3.up * 0.35f;

                for (float t = 0; t < AnimDuration; t += Time.deltaTime)
                {
                    float s = t / AnimDuration;
                    card.transform.position = Vector3.Lerp(
                        Vector3.Lerp(start, peak, s),
                        Vector3.Lerp(peak,  end,  s), s);
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
                targetSlot.PlaceCard(card, cardRoot);
            }
            else if (TargetTransform != null)
            {
                Vector3 end = TargetTransform.position + Vector3.up * 0.02f;
                for (float t = 0; t < AnimDuration; t += Time.deltaTime)
                {
                    card.transform.position = Vector3.Lerp(start, end, t / AnimDuration);
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
                card.MoveTo(cardRoot.InverseTransformPoint(end));
            }

                OnCardDrawn?.Invoke(card);
            }
            finally
            {
                _animating = false;
            }
        }

        // ─── 堆叠厚度感视觉 ──────────────────────────────────────────────────

        private void UpdateGhosts()
        {
            // 仅"往上"方向用 ghost 表现厚度；右/下方向卡牌本身已铺开，无需 ghost
            int want = StackDir == StackDirection.StackUp
                ? Mathf.Clamp(_deck.Count - 1, 0, 8)
                : 0;
            // 移除多余
            while (_ghosts.Count > want)
            {
                int last = _ghosts.Count - 1;
                if (_ghosts[last] != null) Destroy(_ghosts[last].gameObject);
                _ghosts.RemoveAt(last);
            }
            // 补充不足
            while (_ghosts.Count < want)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.name = $"Ghost_{_ghosts.Count}";
                g.transform.SetParent(transform, false);
                int gi = _ghosts.Count;
                g.transform.localScale    = new Vector3(
                    CardView3D.CW * 0.98f, CardView3D.CD, CardView3D.CH * 0.98f);
                g.transform.localPosition = new Vector3(0f, -(gi + 1) * 0.004f, 0f);
                g.transform.localRotation = Quaternion.Euler(0f, NextGhostRotation(), 0f);
                Destroy(g.GetComponent<Collider>());
                var r = g.GetComponent<Renderer>();
                r.material = new Material(r.sharedMaterial);
                r.material.color = new Color(0.28f, 0.26f, 0.22f);
                _ghosts.Add(r);
            }
        }

        private float NextGhostRotation()
        {
            float rotationRange = StackRandomRotation;
            if (rotationRange <= 0f) return 0f;

            int entityHash;
#if UNITY_6000_5_OR_NEWER
            entityHash = GetEntityId().GetHashCode();
#else
            entityHash = GetInstanceID();
#endif
            _ghostRotationRandom ??= new System.Random(unchecked(entityHash * 397 ^ System.Environment.TickCount));
            return ((float)_ghostRotationRandom.NextDouble() - 0.5f) * rotationRange;
        }

        // ─── 卡堆预览（Shift + 悬停）─────────────────────────────────────────

        private void Update()
        {
            if (!Stackable) return;

            bool want = _deck.Count > 0
                        && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                        && IsMouseOverStack();

            if (want && !_previewShown)       ShowPreview();
            else if (!want && _previewShown)  HidePreview();
        }

        /// <summary>射线检测鼠标是否悬停在本卡堆（堆内任一张卡的碰撞体）上。</summary>
        private bool IsMouseOverStack()
        {
            var cam = Camera.main;
            if (cam == null) return false;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            return Physics.Raycast(ray, out var hit, 200f)
                   && hit.transform != null
                   && hit.transform.IsChildOf(transform);
        }

        private void ShowPreview()
        {
            _previewShown = true;
            if (_preview == null)
                _preview = StackPreviewPanel.Create(transform.root);

            _previewLines.Clear();
            if (Previewable)
            {
                // _deck 枚举顺序为 顶→底
                foreach (var card in _deck)
                    if (card != null) _previewLines.Add(card.DisplayName);
            }
            else
            {
                // TODO: 替换为「无法查看」特殊卡的 3D 表现；目前用占位文字保留逻辑
                _previewLines.Add("无法查看");
            }

            string  title = Previewable ? $"卡堆 ({_deck.Count})" : "卡堆";
            Vector3 top   = transform.position + Vector3.up * (0.12f + _deck.Count * 0.01f);
            _preview.Show(title, _previewLines, top);
        }

        private void HidePreview()
        {
            _previewShown = false;
            _preview?.Hide();
        }
    }
}
