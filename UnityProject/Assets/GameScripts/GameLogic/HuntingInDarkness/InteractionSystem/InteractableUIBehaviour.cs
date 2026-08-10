namespace InteractionSystem.Runtime
{
    /// <summary>
    /// Base class for UI-side behaviours.
    /// Extends InteractableBehaviourBase — no InputSettings, input is driven by
    /// Unity's EventSystem callbacks on InteractableUIElement instead.
    /// </summary>
    public abstract class InteractableUIBehaviour : InteractableBehaviourBase
    {
        // No additional members — all shared logic lives in InteractableBehaviourBase.
        // UI-specific behaviour subclasses extend this class directly.
    }
}