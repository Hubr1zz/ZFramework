using System.Collections.Generic;
using UnityEngine;

namespace InteractionSystem.Runtime
{
    /// <summary>
    /// Unified container interface for both 3D (InteractableObject) and UI (InteractableUIElement) targets.
    /// EasyInteractive operates exclusively on this interface — source (Physics vs EventSystem) is irrelevant to dispatch.
    /// </summary>
    public interface IInteractableTarget
    {
        GameObject gameObject { get; }
        List<InteractableBehaviourBase> interactableBehaviours { get; }
        List<IClickable>                clickableBehaviours    { get; }
        List<IDraggableBase>            draggableBehaviours    { get; }
        List<IFocusableBase>            focusableBehaviours    { get; }
    }
}