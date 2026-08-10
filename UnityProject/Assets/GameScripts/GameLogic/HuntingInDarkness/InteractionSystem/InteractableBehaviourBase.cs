using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace InteractionSystem.Runtime
{
    /// <summary>
    /// Shared base for all interactable behaviours — both 3D (InteractableThreeDBehaviour)
    /// and UI (InteractableUIBehaviour). Contains everything except input polling,
    /// which only makes sense for 3D.
    /// </summary>
    public abstract class InteractableBehaviourBase : IInteractable
    {
        // ─── Enable Settings ──────────────────────────────────────────────────────
        [FoldoutGroup("Behaviour Settings"), LabelWidth(160)]
        [OdinSerialize] [PropertyOrder(-12)]
        public bool BehaviourEnabledLocal { get; set; } = true;

        // ─── Interaction State ────────────────────────────────────────────────────
        [FoldoutGroup("Behaviour Settings")]
        [OdinSerialize]
        [EnumToggleButtons]
        [PropertyOrder(-11)]
        private IInteractable.InteractionState interactionState = IInteractable.InteractionState.All;

        public bool EnableFocusLocal
        {
            get => interactionState.HasFlag(IInteractable.InteractionState.EnableFocus);
            set
            {
                if (value) interactionState |= IInteractable.InteractionState.EnableFocus;
                else       interactionState &= ~IInteractable.InteractionState.EnableFocus;
            }
        }

        public bool EnableClickLocal
        {
            get => interactionState.HasFlag(IInteractable.InteractionState.EnableClick);
            set
            {
                if (value) interactionState |= IInteractable.InteractionState.EnableClick;
                else       interactionState &= ~IInteractable.InteractionState.EnableClick;
            }
        }

        public bool EnableDragLocal
        {
            get => interactionState.HasFlag(IInteractable.InteractionState.EnableDrag);
            set
            {
                if (value) interactionState |= IInteractable.InteractionState.EnableDrag;
                else       interactionState &= ~IInteractable.InteractionState.EnableDrag;
            }
        }

        [HideInInspector] public bool       initialized = false;
        [HideInInspector] public GameObject Owner;

        public virtual void Initialize() { }
    }
}