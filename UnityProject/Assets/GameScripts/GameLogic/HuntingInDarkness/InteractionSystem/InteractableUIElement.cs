using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InteractionSystem.Runtime
{
    /// <summary>
    /// UI-side counterpart to InteractableObject.
    /// Implements IInteractableTarget so EasyInteractive can treat it identically to 3D objects.
    /// Input is driven by Unity's EventSystem instead of Physics raycasts.
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractableUIElement : MonoBehaviour, IInteractableTarget,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        // ─── Behaviours ───────────────────────────────────────────────────────────
        [SerializeReference]
        [ListDrawerSettings(OnBeginListElementGUI = nameof(DrawElementScriptLink))]
        public List<InteractableUIBehaviour> uiBehaviours = new();

        // ─── IInteractableTarget ─────────────────────────────────────────────────
        // interactableBehaviours holds UI behaviours as base type for unified dispatch
        public List<InteractableBehaviourBase> interactableBehaviours { get; } = new();
        [HideInInspector] public List<IClickable>     clickableBehaviours { get; } = new();
        [HideInInspector] public List<IDraggableBase> draggableBehaviours { get; } = new();
        [HideInInspector] public List<IFocusableBase> focusableBehaviours { get; } = new();

        // ─── Lifecycle ────────────────────────────────────────────────────────────
        private void Awake()
        {
            foreach (var behaviour in uiBehaviours)
                InitBehaviour(behaviour);
        }

        private void InitBehaviour(InteractableUIBehaviour behaviour)
        {
            if (behaviour.initialized) return;

            behaviour.Owner       = gameObject;
            behaviour.Initialize();
            behaviour.initialized = true;

            interactableBehaviours.Add(behaviour);

            if (behaviour is IClickable clickable)
                clickableBehaviours.Add(clickable);
            if (behaviour is IDraggableBase draggable)
                draggableBehaviours.Add(draggable);
            if (behaviour is IFocusableBase focusable)
                focusableBehaviours.Add(focusable);
        }

        public void AddBehaviour(InteractableUIBehaviour behaviour)
        {
            if (uiBehaviours.Contains(behaviour)) return;
            uiBehaviours.Add(behaviour);
            InitBehaviour(behaviour);
        }

        // ─── EventSystem → EasyInteractive bridge ─────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData) => InteractionSystem.Instance.OnUIPointerEnter(this);
        public void OnPointerExit(PointerEventData eventData)  => InteractionSystem.Instance.OnUIPointerExit(this);
        public void OnPointerDown(PointerEventData eventData)  => InteractionSystem.Instance.OnUIPointerDown(this);
        public void OnPointerUp(PointerEventData eventData)    => InteractionSystem.Instance.OnUIPointerUp(this);
        public void OnBeginDrag(PointerEventData eventData)    => InteractionSystem.Instance.OnUIBeginDrag(this);
        public void OnDrag(PointerEventData eventData)         => InteractionSystem.Instance.OnUIDrag(this);
        public void OnEndDrag(PointerEventData eventData)      => InteractionSystem.Instance.OnUIEndDrag(this);

        // ─── Editor ───────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        private Dictionary<Type, UnityEditor.MonoScript> _scriptCache = new();

        private UnityEditor.MonoScript GetScript(Type type)
        {
            if (_scriptCache.TryGetValue(type, out var cached)) return cached;
            var guids = UnityEditor.AssetDatabase.FindAssets($"{type.Name} t:MonoScript");
            if (guids.Length == 0) { _scriptCache[type] = null; return null; }
            var path   = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            var script = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(path);
            _scriptCache[type] = script;
            return script;
        }

        private void DrawElementScriptLink(int index)
        {
            var behaviour = uiBehaviours[index];
            if (behaviour == null) return;
            var script = GetScript(behaviour.GetType());
            if (script == null) return;
            if (GUILayout.Button($"⬡ {behaviour.GetType().Name}", UnityEditor.EditorStyles.miniLabel))
                UnityEditor.EditorGUIUtility.PingObject(script);
        }
#endif
    }
}
