using System;
using UnityEngine;

namespace HuntingInDarkness.Combat
{
    public interface IPlayableActionCardCommandSink
    {
        void OnRestoreCard(int cardInstanceId);
        void OnDiscardCard(int cardInstanceId);
    }

    public sealed class PlayableActionCardInteraction : MonoBehaviour
    {
        private Action secondaryClick;

        public void BindSecondaryClick(Action command) => secondaryClick = command;

        private void OnMouseOver()
        {
            if (Input.GetMouseButtonDown(1))
                secondaryClick?.Invoke();
        }
    }
}
