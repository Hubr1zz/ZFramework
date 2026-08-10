using System;
using UnityEngine;

namespace InteractionSystem.Runtime
{
    public interface IInteractable
    {
        [Flags]
        public enum InteractionState
        {
            EnableFocus = 1 << 0,
            EnableClick = 1 << 1,
            EnableDrag  = 1 << 2,
            None        = 0,
            All = EnableFocus | EnableClick | EnableDrag,
        }
        public bool EnableFocusLocal      { get; set; }
        public bool EnableClickLocal      { get; set; }
        public bool EnableDragLocal       { get; set; }
        public bool BehaviourEnabledLocal { get; set; }
    }

    // -------------------------------------------------------------------------
    // Focus
    // -------------------------------------------------------------------------

    public interface IFocusableBase : IInteractable
    {
        void OnMouseOutWithoutTarget();
        void OnMouseEnterWithoutTarget();
        void OnMouseStayWithoutTarget();
    }

    /// <summary>
    /// Focus triggered only when a matching IDraggableBase is being dragged.
    /// Only triggers when dragged object matches target
    /// </summary>
    public interface IFocusable<T> : IFocusableBase
    {
        void OnDraggedObjectEnter(T target);
        void OnDraggedObjectStay(T target);
        void OnDraggedObjectReleased(T target);
        void OnDraggedObjectLeave(T target);
    }

    /// <summary>
    /// Focus with no draggable context.
    /// Always triggered
    /// </summary>
    public interface IFocusable : IFocusableBase
    {
        
    }

    // -------------------------------------------------------------------------
    // Click
    // -------------------------------------------------------------------------

    public interface IClickable : IInteractable
    {
        void OnBeginClick();
        void OnPressing();
        void OnClickReleasedInside();
        void OnClickReleasedOutside();
        void OnMouseOutWhilePressing();
        void OnMouseEnterWhilePressing();
    }

    // -------------------------------------------------------------------------
    // Drag
    // Always triggered
    // -------------------------------------------------------------------------

    public interface IDraggableBase : IInteractable
    {
        /// <summary>
        /// Always called to begin drag regardless of plain or generic.
        /// </summary>
        void OnBeginDrag();
        /// <summary>
        /// Called when dragged with no Interactable target
        /// </summary>
        void OnDraggingWithoutTarget(RaycastHit? hit);
        /// <summary>
        /// Called when drag ends or interrupted without Interactable target.
        /// If not generic, always trigger;If generic and has valid target, will not trigger.
        /// </summary>
        void OnDragReleaseWithoutTarget(RaycastHit? hit);
        
        //Focus related during dragging
        //Mouse exit when dragged
        void OnMouseOutWhileDragging();
        //Mouse enter when dragged
        void OnMouseInWhileDragging();
        //Mouse enter when dragged
        void OnMouseStayWhileDragging();
    }

    /// <summary>
    /// Plain drag with no focused target context.
    /// </summary>
    public interface IDraggable : IDraggableBase
    {
    }

    /// <summary>
    /// Drag that receives the focused IFocusableBase as context on end.
    /// T must match the IFocusableBase on the target object.
    /// </summary>
    public interface IDraggable<T> : IDraggableBase
    {
        void OnDragEnterTarget(T target, RaycastHit hit);
        void OnDragStayOnTarget(T target, RaycastHit hit);
        /// <summary>
        /// target is null if mouse released over invalid target.
        /// </summary>
        void OnDragReleasedOnTarget(T target, RaycastHit hit);
        void OnDragLeaveTarget(T target, RaycastHit hit);
    }
}