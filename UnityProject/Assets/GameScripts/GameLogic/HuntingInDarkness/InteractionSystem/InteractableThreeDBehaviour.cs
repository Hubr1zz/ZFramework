using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace InteractionSystem.Runtime
{
    /// <summary>
    /// Base class for 3D interactable behaviours.
    /// Extends InteractableBehaviourBase with InputSettings for polling-based input.
    /// </summary>
    public abstract class InteractableThreeDBehaviour : InteractableBehaviourBase
    {
        [HideReferenceObjectPicker]
        [InlineProperty]
        public sealed class InputSetting
        {
            public enum TriggerType
            {
                Pressed,
                Released,
                Held,
            }

            [SerializeField, LabelText("Key")]
            internal KeyCode key = KeyCode.Mouse0;

            [SerializeField][HideReferenceObjectPicker]
            private ReferencedInputTrigger triggers = new ReferencedInputTrigger();

            public bool IsTriggered(TriggerType type)
            {
                return triggers.IsTriggered(key, type);
            }
        }

        [FoldoutGroup("Behaviour Settings")]
        [HideReferenceObjectPicker]
        [ListDrawerSettings(ShowItemCount = true, DraggableItems = true)]
        [PropertyOrder(-13)]
        [ValidateInput("@$value != null && $value.Count > 0", "InputSettings is null!", InfoMessageType.Warning)]
        public List<InputSetting> InputSettings = new List<InputSetting>();

        public override void Initialize()
        {
            // Legacy Unity input is polled by ReferencedInputTrigger. TEngine owns
            // module lifecycle; this view-side input object has nothing to register.
        }

        [PropertyOrder(-10)]
        [OnInspectorGUI]
        private void DrawBottomInfo()
        {
#if ODIN_INSPECTOR
            Sirenix.Utilities.Editor.SirenixEditorGUI.HorizontalLineSeparator(Color.white, 1);
#endif
        }
    }
}
