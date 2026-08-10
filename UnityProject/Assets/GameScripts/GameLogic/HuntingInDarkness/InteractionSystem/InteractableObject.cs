using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace InteractionSystem.Runtime
{
    [DisallowMultipleComponent]
    public abstract class InteractableObject : MonoBehaviour, IInteractableTarget
    {
        [SerializeReference]
        [ListDrawerSettings(OnBeginListElementGUI = nameof(DrawElementScriptLink))]
        private List<InteractableThreeDBehaviour> _interactableBehaviours = new();

        // IInteractableTarget — base type list for unified dispatch in EasyInteractive
        public List<InteractableBehaviourBase> interactableBehaviours { get; } = new();

        [HideInInspector] public List<IClickable>     clickableBehaviours { get; } = new();
        [HideInInspector] public List<IDraggableBase> draggableBehaviours { get; } = new();
        [HideInInspector] public List<IFocusableBase> focusableBehaviours { get; } = new();

        private void InitBehaviour(InteractableThreeDBehaviour behaviour)
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

        public void AddBehaviour(InteractableThreeDBehaviour behaviour)
        {
            if (_interactableBehaviours.Contains(behaviour)) return;
            _interactableBehaviours.Add(behaviour);
            InitBehaviour(behaviour);
        }

        public virtual void Awake()
        {
            foreach (var behaviour in _interactableBehaviours)
                InitBehaviour(behaviour);
        }

        // ─── Editor ───────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        private Dictionary<Type, UnityEditor.MonoScript> _scriptCache = new();

        private void OnEnable()
        {
            UnityEditor.Compilation.CompilationPipeline.compilationFinished += OnCompilationFinished;
        }

        private void OnDisable()
        {
            UnityEditor.Compilation.CompilationPipeline.compilationFinished -= OnCompilationFinished;
        }

        private void OnCompilationFinished(object obj) => _scriptCache.Clear();
    
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
            var behaviour = _interactableBehaviours[index];
            if (behaviour == null) return;
            var script = GetScript(behaviour.GetType());
            if (script == null) return;
            if (GUILayout.Button($"⬡ {behaviour.GetType().Name}", UnityEditor.EditorStyles.miniLabel))
                UnityEditor.EditorGUIUtility.PingObject(script);
        }
#endif
    
    
    }
}
