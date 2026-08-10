using System.Collections.Generic;
using Cards3D;
using TMPro;
using UnityEngine;

namespace Cards3D
{
    /// <summary>
    /// 所有3D卡牌视图的抽象基类。卡牌平铺在 XZ 平面，从上方（Y+）俯视可读。
    ///
    /// 新增功能：
    ///  - EnableDrag：可被 ResizableCardSlot 或代码开启，使用 Unity 内置鼠标拖拽
    ///  - DragStarted / DragEnded 事件：供卡槽订阅以清理占位
    ///  - Category：供卡槽做类型过滤，子类可覆写
    ///  - OnBeginDrag / OnDragFrame / OnEndDrag：子类钩子
    /// </summary>
    public abstract class CardView3D : MonoBehaviour
    {
        // 标准卡牌尺寸（默认值常量；布局代码沿用这些静态常量）
        public const float CW = 0.75f;
        public const float CH = 1.05f;
        public const float CD = 0.025f;

        // ─── 实例尺寸（子类可覆写，使不同卡有不同大小）──────────────────────
        public virtual float Width  => CW;
        public virtual float Height => CH;
        public virtual float Depth  => CD;

        [SerializeField] protected Renderer _bodyRenderer;
        private Vector3 _baseLocalPos;
        private bool _isHovered;

        // ─── 拖拽状态 ──────────────────────────────────────────────────────
        private bool _dragReady;
        private Vector3 _mouseDownScreenPos;
        private bool _isDragging;
        protected Vector3 _preDragLocalPos;
        protected Transform _preDragParent;

        // ─── 类别（可被测试代码覆写） ───────────────────────────────────────
        private CardCategory? _forcedCategory;
        /// <summary>强制覆盖卡牌类别（测试用，优先级高于子类 Category 虚属性）</summary>
        public void ForceCategory(CardCategory cat) => _forcedCategory = cat;

        // ─── 公开属性 ──────────────────────────────────────────────────────
        public Vector3 BaseLocalPos => _baseLocalPos;

        /// <summary>卡牌被点击时触发（由展台连接到游戏逻辑）</summary>
        public event System.Action<CardView3D> OnClicked;

        /// <summary>拖拽开始时触发；ResizableCardSlot 订阅此事件以清理占位。</summary>
        public event System.Action<CardView3D> DragStarted;
        /// <summary>拖拽结束时触发。</summary>
        public event System.Action<CardView3D> DragEnded;

        /// <summary>是否允许拖拽（默认关闭；由 ResizableCardSlot.PlaceCard 自动开启）</summary>
        public bool EnableDrag = false;

        protected virtual float HoverLift => 0.12f;
        protected bool IsHovered => _isHovered;
        protected bool IsDraggingCard => _isDragging;

        /// <summary>卡牌类别，供卡槽过滤。子类覆写提供默认值；ForceCategory() 可在外部覆盖。</summary>
        public CardCategory Category => _forcedCategory ?? GetDefaultCategory();

        /// <summary>卡堆预览列表中显示的名称。子类可覆写为更友好的名称（默认用 GameObject 名）。</summary>
        public virtual string DisplayName => gameObject.name;

        /// <summary>
        /// 堆叠键。两张 StackKey 相同（且非空）的「松散卡」互相拖拽可合并成动态卡堆。
        /// null = 不参与动态合并（如猎人卡/发明卡）。
        /// </summary>
        public virtual string StackKey => null;

        /// <summary>当前所在卡槽；null 表示松散卡（未入槽）。由 CardSlot 维护。</summary>
        public CardSlot CurrentSlot { get; internal set; }

        /// <summary>全部存活卡牌（动态合并时用于查找邻近松散卡）。</summary>
        public static readonly List<CardView3D> AllCards = new();

        /// <summary>覆盖"拖拽松手后回归的父级"（卡牌被拖出卡堆时，家应改为桌面根）。</summary>
        public void SetDragHome(Transform home) => _preDragParent = home;

        /// <summary>子类覆写此方法返回默认类别（而非直接覆写 Category 属性）。</summary>
        protected virtual CardCategory GetDefaultCategory() => CardCategory.Any;

        // ─── 初始化 ────────────────────────────────────────────────────────

        protected virtual void Awake()    => AllCards.Add(this);
        protected virtual void OnDestroy() => AllCards.Remove(this);

        protected void InitView(Vector3 localPos)
        {
            _baseLocalPos = localPos;
            bool wasPrebuilt = _bodyRenderer != null;
            BuildBaseGeometry();
            // Prefab mode: body already exists, instance the material per-card
            if (wasPrebuilt && _bodyRenderer != null)
                _bodyRenderer.material = new Material(_bodyRenderer.sharedMaterial);
            BuildTextFields();
            ApplyVisuals();
        }

        private void BuildBaseGeometry()
        {
            if (_bodyRenderer != null) return; // prefab already has body

            var col = gameObject.AddComponent<BoxCollider>();
            col.size   = new Vector3(Width, HoverLift + Depth * 2f, Height);
            col.center = new Vector3(0f, HoverLift * 0.4f, 0f);

            var bodyGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyGo.name = "Body";
            bodyGo.transform.SetParent(transform, false);
            bodyGo.transform.localScale = new Vector3(Width, Depth, Height);
            Destroy(bodyGo.GetComponent<Collider>());
            _bodyRenderer = bodyGo.GetComponent<Renderer>();
            _bodyRenderer.material = new Material(_bodyRenderer.sharedMaterial);
        }

        /// <summary>
        /// 创建一个平铺（XZ 平面）的 TextMeshPro 子物体，从上方俯视可读。
        /// Euler(90,0,0) 让文字法线朝 +Y。
        /// </summary>
        protected TextMeshPro MakeText(
            string goName, Vector3 pos, float fontSize,
            TextAlignmentOptions align, Vector2 rectSize)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize  = fontSize;
            tmp.alignment = align;
            tmp.color     = new Color(0.08f, 0.08f, 0.08f);
            tmp.rectTransform.sizeDelta = rectSize;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        // ─── 子类实现 ──────────────────────────────────────────────────────

        protected abstract void BuildTextFields();
        protected abstract void ApplyVisuals();
        protected virtual bool CanHover() => true;

        // ─── 公共操作 ──────────────────────────────────────────────────────

        public void MoveTo(Vector3 newLocalPos)
        {
            _baseLocalPos = newLocalPos;
            transform.localPosition = newLocalPos;
        }

        /// <summary>将 _baseLocalPos 同步到当前 localPosition（拖拽落点后调用）</summary>
        protected void SyncBasePos()
        {
            _baseLocalPos = transform.localPosition;
        }

        // ─── 鼠标交互 ──────────────────────────────────────────────────────

        protected virtual void OnMouseEnter()
        {
            if (!CanHover() || _isDragging) return;
            _isHovered = true;
            transform.localPosition = _baseLocalPos + Vector3.up * HoverLift;
            ApplyVisuals();
        }

        protected virtual void OnMouseExit()
        {
            if (_isDragging) return;
            _isHovered = false;
            transform.localPosition = _baseLocalPos;
            ApplyVisuals();
        }

        protected virtual void OnMouseDown()
        {
            OnClicked?.Invoke(this);
            if (EnableDrag)
            {
                _mouseDownScreenPos = Input.mousePosition;
                _dragReady = true;
            }
        }

        private void OnMouseDrag()
        {
            if (!EnableDrag) return;

            // 移动超过 5px 才开始真正拖拽，与点击区分
            if (_dragReady && !_isDragging)
            {
                if (Vector2.Distance(Input.mousePosition, _mouseDownScreenPos) > 5f)
                {
                    _dragReady = false;
                    BeginDrag();
                }
            }
            if (_isDragging)
            {
                DragMove();
                OnDragFrame();
            }
        }

        private void OnMouseUp()
        {
            _dragReady = false;
            if (_isDragging) EndDrag();
        }

        // ─── 拖拽核心（私有实现） ──────────────────────────────────────────

        private void BeginDrag()
        {
            _isDragging = true;
            _isHovered  = false;
            _preDragLocalPos = _baseLocalPos;
            _preDragParent   = transform.parent;

            transform.SetParent(null, worldPositionStays: true);
            transform.localScale = Vector3.one; // 从卡槽中离开时重置缩放
            transform.position  += Vector3.up * 0.15f;

            OnBeginDrag();
            DragStarted?.Invoke(this);
            ApplyVisuals();
        }

        private void DragMove()
        {
            var ray   = Camera.main.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (plane.Raycast(ray, out float dist))
            {
                var hit = ray.GetPoint(dist);
                transform.position = new Vector3(hit.x, transform.position.y, hit.z);
            }
        }

        private void EndDrag()
        {
            _isDragging = false;
            OnEndDrag(); // 子类可在此重定向父级或吸附槽

            // 若子类没有重新挂父，则回到原父级并同步落点位置
            if (transform.parent == null)
            {
                transform.SetParent(_preDragParent, worldPositionStays: true);
                SyncBasePos();
            }
            DragEnded?.Invoke(this);
            ApplyVisuals();
        }

        // ─── 子类拖拽钩子 ──────────────────────────────────────────────────

        /// <summary>拖拽开始后（脱离父级后）调用。可在此清理卡槽占位。</summary>
        protected virtual void OnBeginDrag() { }

        /// <summary>每帧拖拽移动后调用。可在此做卡槽高亮检测。</summary>
        protected virtual void OnDragFrame() { }

        /// <summary>
        /// 拖拽结束时调用（DragEnded 事件之前）。
        /// 若在此完成了 SetParent，基类不会再重设父级。
        /// </summary>
        protected virtual void OnEndDrag() { }
    }
}
