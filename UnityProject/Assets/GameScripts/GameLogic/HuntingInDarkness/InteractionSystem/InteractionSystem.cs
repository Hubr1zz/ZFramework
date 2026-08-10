using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InteractionSystem.Runtime
{
    public class InteractionSystem : MonoBehaviour
    {
        // ─── Fields ───────────────────────────────────────────────────────────────
        #region Fields

        [SerializeField] private float dragThreshold         = 5f;
        [SerializeField] private float rayRadius             = 0.02f;
        [SerializeField] private bool  triggerLeaveOnRelease  = true;
        [SerializeField] private bool  triggerStayOnEnterFrame = true;
        [SerializeField] private int   maxHitNumber          = 10;
        private bool inUpdate;
        
        private static InteractionSystem _instance;

        [SerializeField][ReadOnly]
        private Context context = new Context();
        private IInteractableTarget currentInteractableTarget;
        private IInteractableTarget possibleClickObject;
        private IInteractableTarget possibleDragObject;

        [ShowInInspector, ReadOnly]
        private GameObject          newFocusedObject;  // raw GameObject — may or may not have IInteractableTarget
        private IInteractableTarget newFocusedTarget;  // non-null only when newFocusedObject has IInteractableTarget

        private Vector3      _mouseDownPosition = Vector3.negativeInfinity;
        

        private RaycastHit   _hitInfo;
        private RaycastHit[] hitResults;
        private Ray          ray;
        private readonly List<RaycastResult> _uiRaycastResults = new List<RaycastResult>();
        private readonly PointerEventData _pointerEventData = new PointerEventData(EventSystem.current);
        
        private bool shouldClearDrag;
        private bool shouldClearFocus, newFocus;
        private bool shouldClearClick;

        enum State { None, Stay, Out, InFromNull, SwitchToNew }
        private State _draggedObjectViewState = State.None;
        private State _focusedObjectState     = State.None;

        // behaviourType -> global enabled — shared by InteractableBehaviour AND InteractableUIBehaviour
        [ShowInInspector][TableList, DictionaryDrawerSettings(IsReadOnly = true)]
        private Dictionary<Type, bool> _behaviourState = new();

        private struct MethodPair : IEquatable<MethodPair>
        {
            public Type       targetType;
            public MethodInfo method;
            public bool       needsHitInfo;

            public bool Equals(MethodPair other) => targetType == other.targetType && method == other.method;
            public override bool Equals(object obj) => obj is MethodPair o && Equals(o);
            //public override int  GetHashCode() => HashCode.Combine(targetType, method);
            public MethodPair(Type t, MethodInfo m) 
            { 
                targetType   = t; 
                method       = m;
                needsHitInfo = m.GetParameters().Length == 2;
            }
        }

        // Generic dispatch caches
        private Dictionary<Type, List<MethodPair>> _dragReleasedOnTargetAsDraggedCache  = new();
        private Dictionary<Type, List<MethodPair>> _dragReleasedOnTargetAsReceiverCache = new();
        private Dictionary<Type, List<MethodPair>> _dragEnterTargetAsReceiverCache      = new();
        private Dictionary<Type, List<MethodPair>> _dragEnterTargetAsDraggedCache       = new();
        private Dictionary<Type, List<MethodPair>> _dragStayOnTargetAsReceiverCache     = new();
        private Dictionary<Type, List<MethodPair>> _dragStayOnTargetAsDraggedCache      = new();
        private Dictionary<Type, List<MethodPair>> _dragLeaveTargetAsReceiverCache      = new();
        private Dictionary<Type, List<MethodPair>> _dragLeaveTargetAsDraggedCache       = new();

        static readonly Type behaviourBaseDefinition = typeof(InteractableBehaviourBase);
        static readonly Type draggableGenericDef     = typeof(IDraggable<>);
        static readonly Type focusableGenericDef     = typeof(IFocusable<>);

        #endregion

        // ─── Properties ───────────────────────────────────────────────────────────
        #region Properties

        public static InteractionSystem Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<InteractionSystem>();
                return _instance;
            }
        }
        
        public float RayRadius => rayRadius;
        public bool  InUpdate  { get => inUpdate; set => inUpdate = value; }

        public GameObject CurrentFocusedObject
        {
            get => context.currentFocused;
            set => context.currentFocused = value;
        }
        public IInteractableTarget CurrentClickedObject
        {
            get => context.currentClicked;
            set => context.currentClicked = value;
        }
        public IInteractableTarget CurrentDraggedObject
        {
            get => context.currentDragged;
            set => context.currentDragged = value;
        }

        #endregion

        // ─── Init ─────────────────────────────────────────────────────────────────
        public void Awake()
        {
            _instance  = this;
            hitResults = new RaycastHit[maxHitNumber];
            RegisterAllBehaviourTypes();
        }

        // ─── Registration ─────────────────────────────────────────────────────────
        #region Registration

        private void RegisterAllBehaviourTypes()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (!type.IsClass || type.IsAbstract) continue;
                    if (!behaviourBaseDefinition.IsAssignableFrom(type)) continue;

                    _behaviourState.TryAdd(type, true);

                    foreach (var iface in type.GetInterfaces())
                    {
                        if (!iface.IsGenericType) continue;
                        Type def  = iface.GetGenericTypeDefinition();
                        Type tArg = iface.GetGenericArguments()[0];

                        if (def == draggableGenericDef)
                        {
                            RegisterCache(type, tArg, GetConcreteMethod(type, iface, "OnDragReleasedOnTarget"), _dragReleasedOnTargetAsDraggedCache);
                            RegisterCache(type, tArg, GetConcreteMethod(type, iface, "OnDragEnterTarget"),      _dragEnterTargetAsDraggedCache);
                            RegisterCache(type, tArg, GetConcreteMethod(type, iface, "OnDragStayOnTarget"),     _dragStayOnTargetAsDraggedCache);
                            RegisterCache(type, tArg, GetConcreteMethod(type, iface, "OnDragLeaveTarget"),      _dragLeaveTargetAsDraggedCache);
                        }
                        else if (def == focusableGenericDef)
                        {
                            RegisterCache(type, tArg, GetConcreteMethod(type, iface, "OnDraggedObjectReleased"), _dragReleasedOnTargetAsReceiverCache);
                            RegisterCache(type, tArg, GetConcreteMethod(type, iface, "OnDraggedObjectEnter"),    _dragEnterTargetAsReceiverCache);
                            RegisterCache(type, tArg, GetConcreteMethod(type, iface, "OnDraggedObjectStay"),     _dragStayOnTargetAsReceiverCache);
                            RegisterCache(type, tArg, GetConcreteMethod(type, iface, "OnDraggedObjectLeave"),    _dragLeaveTargetAsReceiverCache);
                        }
                    }
                }
            }
        }

        private MethodInfo GetConcreteMethod(Type concreteType, Type iface, string methodName)
        {
            var map = concreteType.GetInterfaceMap(iface);
            var ifaceMethod = iface.GetMethod(methodName);
            int idx = Array.IndexOf(map.InterfaceMethods, ifaceMethod);
            return idx >= 0 ? map.TargetMethods[idx] : null;
        }
        
        private void RegisterCache(Type owner, Type paramType, MethodInfo method,
            Dictionary<Type, List<MethodPair>> dict)
        {
            if (!dict.TryGetValue(owner, out var set) || set == null)
                dict[owner] = set = new List<MethodPair>();
            set.Add(new MethodPair(paramType, method));
        }

        #endregion

        // ─── Update ───────────────────────────────────────────────────────────────
        public void Update()
        {
            if (IsPointerOverUIOnly())
            {
                possibleClickObject = null;
                possibleDragObject  = null;

                // 和 3D 一样：根据 newFocusedObject 推进 _focusedObjectState
                if      (CurrentFocusedObject && newFocusedObject != CurrentFocusedObject)
                    _focusedObjectState = State.SwitchToNew;
                else if (!CurrentFocusedObject && newFocusedObject != null)
                    _focusedObjectState = State.InFromNull;
                else if (newFocusedObject != null)
                    _focusedObjectState = _focusedObjectState == State.None ? State.InFromNull : State.Stay;
                else
                    _focusedObjectState = _focusedObjectState == State.Stay ? State.Out : State.None;

                HandleFocus();
                if (CurrentDraggedObject is InteractableObject) TickDragNoHit();
                if (CurrentClickedObject is InteractableObject) TickCurrentClick();
                DelayedUpdate();
                return;
            }

            if (Camera.main == null) return;

            ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            bool hit = GetRayHits(ray, out RaycastHit info, out IInteractableTarget hitTarget);

            if (!hit)
            {
                _hitInfo            = default;
                newFocusedTarget    = null;
                possibleClickObject = null;
                possibleDragObject  = null;
                ClearCurrentFocused();
                HandleFocus();
                HandleClick();
                HandleDrag();
            }
            else
            {
                _hitInfo = info;
                Debug.DrawLine(Camera.main.transform.position, _hitInfo.point, Color.red);
                if (hitTarget != null)
                    UpdateInfo(hitTarget);
                else
                {
                    currentInteractableTarget = null;
                    possibleClickObject       = null;
                    possibleDragObject        = null;
                }
                HandleFocus();
                HandleClick();
                HandleDrag();
            }

            DelayedUpdate();
        }

        private void DelayedUpdate()
        {
            if (shouldClearDrag)
            {
                CurrentDraggedObject = null;
                shouldClearDrag      = false;
            }
            if (shouldClearFocus)
            {
                CurrentFocusedObject = null;
                shouldClearFocus     = false;
            }
            else if (newFocus)
            {
                CurrentFocusedObject = newFocusedObject;
                newFocus             = false;
            }
            if (shouldClearClick)
            {
                CurrentClickedObject = null;
                shouldClearClick     = false;
            }
        }

        private void UpdateInfo(IInteractableTarget hitObj)
        {
            if (hitObj == currentInteractableTarget) return;
            currentInteractableTarget = hitObj;

            bool shouldHandleDrag  = CurrentDraggedObject == null && possibleDragObject  != hitObj;
            bool shouldHandleClick = CurrentClickedObject == null && possibleClickObject != hitObj;

            foreach (var behaviour in hitObj.interactableBehaviours)
            {
                if (!IsBehaviourEnabledBase(behaviour, false)) continue;
                if (shouldHandleDrag  && behaviour is IDraggableBase && behaviour.EnableDragLocal)
                    possibleDragObject = hitObj;
                if (shouldHandleClick && behaviour is IClickable && behaviour.EnableClickLocal)
                    possibleClickObject = hitObj;
            }
        }

        private void TickDragNoHit()
        {
            if (CurrentDraggedObject == null) return;
            foreach (var draggableBase in CurrentDraggedObject.draggableBehaviours)
            {
                if (draggableBase is not InteractableBehaviourBase b || !IsBehaviourEnabledBase(b)) continue;
                if (HasInput3D(draggableBase, 1))      TickCurrentDrag(draggableBase);
                else if (HasInput3D(draggableBase, 2)) DragRelease(draggableBase);
            }
        }

        // ─── Focus ────────────────────────────────────────────────────────────────
        #region Focus

        private void HandleFocus()
        {
            if (CurrentFocusedObject)
            {
                if (newFocusedObject && newFocusedObject != CurrentFocusedObject)
                {
                    ClearCurrentFocused(false);
                    StartNewFocus();
                }
                TickCurrentFocus();
            }
            else if (newFocusedObject)
            {
                StartNewFocus();
                TickCurrentFocus();
            }
            else
            {
                ClearCurrentFocused();
            }
        }

        private void StartNewFocus()
        {
            newFocus = true;
            if (newFocusedTarget == null) return; // no IInteractableTarget, nothing to dispatch

            foreach (var behaviour in newFocusedTarget.focusableBehaviours)
            {
                if (behaviour is not InteractableBehaviourBase b || !IsBehaviourEnabledBase(b) || !b.EnableFocusLocal) continue;

                if (behaviour is IFocusable plain)
                    plain.OnMouseEnterWithoutTarget();
                else
                {
                    if (!TriggerConditionalFocus(behaviour, _dragEnterTargetAsReceiverCache))
                        behaviour.OnMouseEnterWithoutTarget();
                }
            }
        }

        private void TickCurrentFocus()
        {
            if (!CurrentFocusedObject) return;
            // Only dispatch if the focused object has an IInteractableTarget
            if (CurrentFocusedObject.TryGetComponent<InteractableObject>(out var io3d))
            {
                DispatchFocusTick(io3d);
            }
            else if (CurrentFocusedObject.TryGetComponent<InteractableUIElement>(out var uie))
            {
                DispatchFocusTick(uie);
            }
        }

        private void DispatchFocusTick(IInteractableTarget target)
        {
            foreach (var behaviour in target.focusableBehaviours)
            {
                if (behaviour is not InteractableBehaviourBase b || !IsBehaviourEnabledBase(b) || !b.EnableFocusLocal) continue;

                if (behaviour is IFocusable plain)
                    plain.OnMouseStayWithoutTarget();
                else
                {
                    if (_focusedObjectState == State.Stay ||
                        (_focusedObjectState == State.InFromNull && triggerStayOnEnterFrame))
                        TriggerConditionalFocus(behaviour, _dragStayOnTargetAsReceiverCache);
                }
            }
        }

        private void ClearCurrentFocused(bool setToNull = true)
        {
            if (!CurrentFocusedObject) return;

            // Only dispatch if the focused object has an IInteractableTarget
            IInteractableTarget target = CurrentFocusedObject.GetComponent<InteractableObject>() as IInteractableTarget
                                      ?? CurrentFocusedObject.GetComponent<InteractableUIElement>();
            if (target != null)
            {
                foreach (var focusableBase in target.focusableBehaviours)
                {
                    if (focusableBase is not InteractableBehaviourBase b || !IsBehaviourEnabledBase(b)) continue;
                    if (focusableBase is IFocusable plain)
                        plain.OnMouseOutWithoutTarget();
                    else
                    {
                        bool leaveTriggered = false;
                        if (triggerLeaveOnRelease)
                            leaveTriggered = TriggerConditionalFocus(focusableBase, _dragLeaveTargetAsReceiverCache);
                        if (!leaveTriggered)
                            focusableBase.OnMouseOutWithoutTarget();
                    }
                }
            }
            shouldClearFocus = setToNull;
        }

        #endregion

        // ─── Click ────────────────────────────────────────────────────────────────
        #region Click

        private void HandleClick()
        {
            if (CurrentClickedObject != null) { TickCurrentClick(); return; }
            if (possibleClickObject  != null) StartNewClicked(possibleClickObject);
            TickCurrentClick();
        }

        private void TickCurrentClick()
        {
            if (CurrentClickedObject == null) return;
            foreach (var clickable in CurrentClickedObject.clickableBehaviours)
            {
                if (clickable is not InteractableBehaviourBase b || !IsBehaviourEnabledBase(b)) continue;
                if (HasInput3D(clickable, 1))
                {
                    clickable.OnPressing();
                    switch (_focusedObjectState)
                    {
                        case State.Out:
                            if (CurrentFocusedObject != CurrentClickedObject.gameObject)
                                clickable.OnMouseOutWhilePressing();
                            break;
                        case State.InFromNull:
                            if (newFocusedObject == CurrentClickedObject.gameObject)
                                clickable.OnMouseEnterWhilePressing();
                            break;
                        case State.Stay:
                        case State.None:
                            break;
                        case State.SwitchToNew:
                            if (newFocusedObject == CurrentClickedObject.gameObject)
                                clickable.OnMouseEnterWhilePressing();
                            else if (CurrentFocusedObject != CurrentClickedObject.gameObject)
                                clickable.OnMouseOutWhilePressing();
                            break;
                    }
                }
                if (HasInput3D(clickable, 2))
                {
                    if (CurrentFocusedObject && CurrentFocusedObject == CurrentClickedObject.gameObject)
                        clickable.OnClickReleasedInside();
                    else
                        clickable.OnClickReleasedOutside();
                    CurrentClickedObject = null;
                }
            }
        }

        private void StartNewClicked(IInteractableTarget obj)
        {
            if (obj == null) return;
            bool ended = false;
            foreach (var clickable in obj.clickableBehaviours)
            {
                if (clickable is not InteractableBehaviourBase b || !IsBehaviourEnabledBase(b)) continue;
                if (HasInput3D(clickable, 0))
                {
                    if (!ended) { ClearCurrentClicked(); ended = true; CurrentClickedObject = obj; }
                    clickable.OnBeginClick();
                }
            }
        }

        public void ClearCurrentClicked()
        {
            if (CurrentClickedObject == null) return;
            foreach (var clickable in CurrentClickedObject.clickableBehaviours)
                clickable.OnClickReleasedOutside();
            shouldClearClick = true;
        }

        #endregion

        // ─── Drag ─────────────────────────────────────────────────────────────────
        #region Drag

        private bool IsCurrentDraggedObj(GameObject go)
        {
            if (CurrentDraggedObject == null || !go) return false;
            // Compare by gameObject reference
            return (CurrentDraggedObject as MonoBehaviour)?.gameObject == go;
        }

        private void HandleDrag()
        {
            if (CurrentDraggedObject != null)
            {
                foreach (var draggableBase in CurrentDraggedObject.draggableBehaviours)
                {
                    if (draggableBase is not InteractableBehaviourBase b || !IsBehaviourEnabledBase(b) || !b.EnableDragLocal) continue;
                    if (HasInput3D(draggableBase, 1))      TickCurrentDrag(draggableBase);
                    else if (HasInput3D(draggableBase, 2)) DragRelease(draggableBase);
                }
            }
            if (possibleDragObject != null) TryStartNewDrag();
        }

        public void ClearCurrentDragged()
        {
            if (CurrentDraggedObject == null || shouldClearDrag) return;
            foreach (var draggableBase in CurrentDraggedObject.draggableBehaviours)
                DragRelease(draggableBase);
            shouldClearDrag = true;
        }

        private void TryStartNewDrag()
        {
            bool mouseDown = false, clearPrevious = false;
            foreach (var draggableBase in possibleDragObject.draggableBehaviours)
            {
                if (draggableBase is not InteractableBehaviourBase b || !IsBehaviourEnabledBase(b) || !b.EnableDragLocal) continue;
                if (HasInput3D(draggableBase, 0) && !mouseDown)
                {
                    mouseDown = true;
                    _mouseDownPosition = Input.mousePosition;
                }
                else if (HasInput3D(draggableBase, 1))
                {
                    if (Vector3.SqrMagnitude(_mouseDownPosition - Input.mousePosition) <=
                        dragThreshold * dragThreshold) continue;
                    if (!clearPrevious)
                    {
                        ClearCurrentDragged();
                        clearPrevious        = true;
                        CurrentDraggedObject = possibleDragObject;
                    }
                    draggableBase.OnBeginDrag();
                }
            }
        }

        private void TickCurrentDrag(IDraggableBase draggableBase)
        {
            switch (_draggedObjectViewState)
            {
                case State.InFromNull: draggableBase.OnMouseInWhileDragging();   break;
                case State.Out:        draggableBase.OnMouseOutWhileDragging();  break;
                case State.Stay:       draggableBase.OnMouseStayWhileDragging(); break;
            }

            if (draggableBase is IDraggable)
            {
                draggableBase.OnDraggingWithoutTarget(_hitInfo);
            }
            else
            {
                switch (_focusedObjectState)
                {
                    case State.InFromNull:
                        TriggerConditionalDrag(draggableBase, newFocusedObject, _dragEnterTargetAsDraggedCache);
                        break;
                    case State.Out:
                        TriggerConditionalDrag(draggableBase, CurrentFocusedObject, _dragLeaveTargetAsDraggedCache);
                        break;
                    case State.SwitchToNew:
                        TriggerConditionalDrag(draggableBase, newFocusedObject,     _dragEnterTargetAsDraggedCache);
                        TriggerConditionalDrag(draggableBase, CurrentFocusedObject, _dragLeaveTargetAsDraggedCache);
                        break;
                    case State.Stay:
                        if (!TriggerConditionalDrag(draggableBase, CurrentFocusedObject, _dragStayOnTargetAsDraggedCache))
                            draggableBase.OnDraggingWithoutTarget(_hitInfo);
                        break;
                    case State.None:
                        draggableBase.OnDraggingWithoutTarget(_hitInfo);
                        break;
                }
            }
        }

        private void DragRelease(IDraggableBase draggableBase)
        {
            _mouseDownPosition = Vector3.negativeInfinity;
            if (draggableBase is IDraggable)
            {
                draggableBase.OnDragReleaseWithoutTarget(_hitInfo);
            }
            else
            {
                if (!TriggerConditionalDrag(draggableBase, CurrentFocusedObject, _dragReleasedOnTargetAsDraggedCache))
                    draggableBase.OnDragReleaseWithoutTarget(_hitInfo);
                TriggerResponsiveFocus(draggableBase, _dragReleasedOnTargetAsReceiverCache);
            }
            shouldClearDrag = true;
        }

        #endregion

        // ─── UI EventSystem Bridge ────────────────────────────────────────────────
        #region UI Bridge
        
        public void OnUIPointerEnter(InteractableUIElement element)
        {
            newFocusedObject = element.gameObject;
            newFocusedTarget = element;
        }

        public void OnUIPointerExit(InteractableUIElement element)
        {
            newFocusedObject = null;
            newFocusedTarget = null;
        }

        public void OnUIPointerDown(InteractableUIElement element)
        {
            if (CurrentClickedObject != null) ClearCurrentClicked();
            foreach (var clickable in element.clickableBehaviours)
            {
                if (clickable is not InteractableBehaviourBase b || !IsBehaviourEnabledBase(b) || !b.EnableClickLocal) continue;
                CurrentClickedObject = element;
                clickable.OnBeginClick();
            }
            DelayedUpdate();
        }

        public void OnUIPointerUp(InteractableUIElement element)
        {
            if (CurrentClickedObject != element) return;
            foreach (var clickable in element.clickableBehaviours)
            {
                if (clickable is not InteractableBehaviourBase b || !IsBehaviourEnabledBase(b)) continue;
                if (CurrentFocusedObject == element.gameObject) clickable.OnClickReleasedInside();
                else                                             clickable.OnClickReleasedOutside();
            }
            shouldClearClick = true;
            DelayedUpdate();
        }

        public void OnUIBeginDrag(InteractableUIElement element)
        {
            ClearCurrentDragged();
            CurrentDraggedObject    = element;
            _draggedObjectViewState = State.InFromNull;
            foreach (var draggableBase in element.draggableBehaviours)
            {
                if (draggableBase is not InteractableBehaviourBase b || !IsBehaviourEnabledBase(b) || !b.EnableDragLocal) continue;
                draggableBase.OnBeginDrag();
            }
            DelayedUpdate();
        }

        public void OnUIDrag(InteractableUIElement element)
        {
            if (CurrentDraggedObject != element) return;
            foreach (var draggableBase in element.draggableBehaviours)
            {
                if (draggableBase is not InteractableBehaviourBase b || !IsBehaviourEnabledBase(b) || !b.EnableDragLocal) continue;
                TickCurrentDrag(draggableBase);
            }
            TickCurrentFocus();
            DelayedUpdate();
        }

        public void OnUIEndDrag(InteractableUIElement element)
        {
            if (CurrentDraggedObject != element) return;
            foreach (var draggableBase in element.draggableBehaviours)
            {
                if (draggableBase is not InteractableBehaviourBase b || !IsBehaviourEnabledBase(b) || !b.EnableDragLocal) continue;
                DragRelease(draggableBase);
            }
            DelayedUpdate();
        }

        #endregion

        // ─── Helpers ──────────────────────────────────────────────────────────────
        #region Helpers

        /// <summary>Unified enabled check — works for any InteractableBehaviourBase subclass.</summary>
        private bool IsBehaviourEnabledBase(InteractableBehaviourBase b, bool log = true)
        {
            if (!b.BehaviourEnabledLocal)
            {
                if (log) Debug.LogWarning(b.GetType() + " disabled on " + b.Owner?.name);
                return false;
            }

            if (_behaviourState.TryGetValue(b.GetType(), out bool globalEnabled))
            {
                if (!globalEnabled && log) Debug.LogWarning(b.GetType() + " disabled globally");
                return globalEnabled;
            }

            if (log) Debug.LogWarning($"[EasyInteractive] {b.GetType()} on {b.Owner?.name} not registered.");
            return false;
        }

        /// <summary>Input polling — only meaningful for 3D behaviours; returns false for UI.</summary>
        private bool HasInput3D(object behaviour, int type)
        {
            if (behaviour is not InteractableThreeDBehaviour b3d) return false;
            return HasInput(b3d.InputSettings, type);
        }

        private bool HasInput(List<InteractableThreeDBehaviour.InputSetting> settings, int type)
        {
            foreach (var t in settings)
                switch (type)
                {
                    case 0: if (t.IsTriggered(InteractableThreeDBehaviour.InputSetting.TriggerType.Pressed))  return true; break;
                    case 1: if (t.IsTriggered(InteractableThreeDBehaviour.InputSetting.TriggerType.Held))     return true; break;
                    case 2: if (t.IsTriggered(InteractableThreeDBehaviour.InputSetting.TriggerType.Released)) return true; break;
                }
            return false;
        }

        // ─── Generic dispatch ─────────────────────────────────────────────────────

        private bool TriggerResponsiveFocus(IDraggableBase draggableBase,
            Dictionary<Type, List<MethodPair>> cache)
        {
            if (!CurrentFocusedObject) return false;
            IInteractableTarget focusedTarget = CurrentFocusedObject.GetComponent<InteractableObject>() as IInteractableTarget
                                             ?? CurrentFocusedObject.GetComponent<InteractableUIElement>();
            if (focusedTarget == null) return false;

            bool triggered = false;
            foreach (var focusableBase in focusedTarget.focusableBehaviours)
            {
                if (focusableBase is not InteractableBehaviourBase b || focusableBase is IFocusable || !IsBehaviourEnabledBase(b)) continue;

                if (!cache.TryGetValue(b.GetType(), out var set)) continue;

                foreach (var pair in set)
                {
                    if (!pair.targetType.IsInstanceOfType(draggableBase)) continue;
                    pair.method.Invoke(focusableBase, new object[] { draggableBase });
                    triggered = true;
                }
            }
            return triggered;
        }

        private bool TriggerConditionalFocus(IFocusableBase focusableBase,
            Dictionary<Type, List<MethodPair>> cache)
        {
            if (CurrentDraggedObject == null || focusableBase is IFocusable) return false;
            if (focusableBase is not InteractableBehaviourBase b || !IsBehaviourEnabledBase(b)) return false;

            return TryInvokeGeneric(b.GetType(), focusableBase, CurrentDraggedObject.gameObject, cache);
        }

        private bool TriggerConditionalDrag(IDraggableBase draggableBase,
            GameObject focusedObject, Dictionary<Type, List<MethodPair>> cache)
        {
            if (!focusedObject || draggableBase is IDraggable) return false;
            if (draggableBase is not InteractableBehaviourBase b || !IsBehaviourEnabledBase(b)) return false;

            return TryInvokeGeneric(b.GetType(), draggableBase, focusedObject, cache);
        }

        /// <summary>
        /// Core generic invocation.
        /// Searches both InteractableObject and InteractableUIElement on targetObj for matching T.
        /// </summary>
        private bool TryInvokeGeneric(Type behaviourType, object behaviour, GameObject targetObj,
            Dictionary<Type, List<MethodPair>> cache)
        {
            if (!targetObj) return false;
            if (!cache.TryGetValue(behaviourType, out var set)) return false;

            bool found = false;
            foreach (var pair in set)
            {
                if (typeof(InteractableBehaviourBase).IsAssignableFrom(pair.targetType))
                {
                    // Search all behaviours from any IInteractableTarget on the object
                    var target = targetObj.GetComponent<InteractableObject>() as IInteractableTarget
                              ?? targetObj.GetComponent<InteractableUIElement>();
                    if (target == null) continue;

                    foreach (var m in target.interactableBehaviours.Where(b => pair.targetType.IsInstanceOfType(b)))
                    {
                        pair.method.Invoke(behaviour, pair.needsHitInfo
                            ? new object[] { m, _hitInfo }
                            : new object[] { m });
                        found = true;
                    }
                }
                else if (typeof(Component).IsAssignableFrom(pair.targetType))
                {
                    var component = targetObj.GetComponent(pair.targetType);
                    if (component == null) continue;
                    pair.method.Invoke(behaviour, pair.needsHitInfo
                        ? new object[] { component, _hitInfo }
                        : new object[] { component });
                    found = true;
                }
                else
                {
                    Debug.LogWarning("Unsupported target type: " + pair.targetType);
                }
            }
            return found;
        }

        #endregion

        // ─── Raycast ──────────────────────────────────────────────────────────────
        #region Raycast

        private bool IsPointerOverUIOnly()
        {
            _pointerEventData.position = Input.mousePosition;
            _uiRaycastResults.Clear();
            EventSystem.current.RaycastAll(_pointerEventData, _uiRaycastResults);

            for (int i = 0; i < _uiRaycastResults.Count; i++)
            {
                var go = _uiRaycastResults[i].gameObject;
                if (!go.GetComponent<RectTransform>()) continue;

                var canvas = _uiRaycastResults[i].module?.transform.GetComponentInParent<Canvas>();
                if (!canvas) continue;

                if (canvas.renderMode != RenderMode.WorldSpace) return true;

                // World Space: only block if InteractableUIElement is present
                if (go.GetComponentInParent<InteractableUIElement>()) return true;
            }
            return false;
        }
        
        private bool GetRayHits(Ray ray, out RaycastHit closestHit, out IInteractableTarget hitObj)
        {
            closestHit       = default;
            hitObj           = null;
            newFocusedObject = null;
            newFocusedTarget = null;

            int hitNum = Physics.RaycastNonAlloc(ray, hitResults);
            if (hitNum == 0)
                hitNum = Physics.SphereCastNonAlloc(ray, rayRadius, hitResults);

            return handle(hitNum, out closestHit, out hitObj);

            bool handle(int number, out RaycastHit ch, out IInteractableTarget focusTarget)
            {
                ch = default; focusTarget = null;
                if (number == 0)
                {
                    outState(ref _draggedObjectViewState);
                    outState(ref _focusedObjectState);
                    return false;
                }

                Array.Sort(hitResults, 0, number,
                    Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance)));

                bool draggedIsFirst = IsCurrentDraggedObj(hitResults[0].transform.gameObject);
                if (draggedIsFirst) inState(ref _draggedObjectViewState);
                else                outState(ref _draggedObjectViewState);

                int idx = draggedIsFirst ? 1 : 0;
                if (idx >= number)
                {
                    newFocusedObject = null;
                    newFocusedTarget = null;
                    outState(ref _focusedObjectState);
                    return false;
                }

                ch = hitResults[idx];
                var go = ch.transform.gameObject;
                // Always set newFocusedObject — supports plain GameObjects too
                newFocusedObject = go;
                // Only set newFocusedTarget if it implements IInteractableTarget
                focusTarget = go.GetComponent<InteractableObject>() as IInteractableTarget ?? go.GetComponent<InteractableUIElement>();
                newFocusedTarget = focusTarget;

                // State comparison uses CurrentFocusedObject (GameObject) — consistent with plain object support
                if      (CurrentFocusedObject && newFocusedObject != CurrentFocusedObject)
                    _focusedObjectState = State.SwitchToNew;
                else if (!CurrentFocusedObject)
                    _focusedObjectState = State.InFromNull;
                else
                    inState(ref _focusedObjectState);

                return true;
            }

            void outState(ref State s) => s = s == State.Stay ? State.Out : State.None;
            void inState(ref State s)  => s = s == State.None ? State.InFromNull : State.Stay;
        }

        #endregion

        // ─── Public API ───────────────────────────────────────────────────────────

        public void Reset()
        {
            _hitInfo            = default;
            newFocusedObject    = null;
            newFocusedTarget    = null;
            possibleClickObject = null;
            possibleDragObject  = null;
            ClearCurrentClicked();
            ClearCurrentDragged();
            ClearCurrentFocused();
        }

        public void EnableBehaviourType(Type type)
        {
            if (_behaviourState.ContainsKey(type)) _behaviourState[type] = true;
            else Debug.LogWarning(type + " not registered.");
        }

        public void DisableBehaviourType(Type type)
        {
            if (_behaviourState.ContainsKey(type)) _behaviourState[type] = false;
            else Debug.LogWarning(type + " not registered.");
        }

        public void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            Gizmos.color = Color.red;
            Vector3 end = ray.origin + ray.direction * 999;
            Gizmos.DrawLine(ray.origin, end);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(ray.origin, rayRadius);
            Gizmos.DrawWireSphere(end,        rayRadius);
            for (int i = 0; i < 8; i++)
            {
                float   a   = i * 45f * Mathf.Deg2Rad;
                Vector3 off = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * rayRadius;
                Gizmos.DrawLine(ray.origin + off, end + off);
            }
        }
    }

    [Serializable]
    public class Context
    {
        [ShowInInspector, ReadOnly]
        public IInteractableTarget currentDragged = null;
        [ShowInInspector, ReadOnly]
        public IInteractableTarget currentClicked = null;
        public GameObject          currentFocused = null;
    }
}