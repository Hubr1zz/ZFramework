using System.Collections.Generic;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    public readonly struct TabletopEventChoicePresentation
    {
        public TabletopEventChoicePresentation(string title, string body, bool interactable, string status, System.Action selected)
        {
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
            Interactable = interactable;
            Status = status ?? string.Empty;
            Selected = selected;
        }

        public string Title { get; }
        public string Body { get; }
        public bool Interactable { get; }
        public string Status { get; }
        public System.Action Selected { get; }
    }

    /// <summary>营地与狩猎共用的世界空间事件桌面。</summary>
    public sealed class TabletopEventPanel3D : MonoBehaviour
    {
        private readonly List<TabletopEventChoiceCard3D> choiceCards = new();
        private TabletopEventPrimaryCard3D primaryCard;

        public bool IsOpen => gameObject.activeSelf;
        public int ChoiceCardCount => choiceCards.Count;
        public int InteractableChoiceCount { get; private set; }

        public static TabletopEventPanel3D Create(Transform parent)
        {
            var gameObject = new GameObject("TabletopEventPanel3D");
            gameObject.transform.SetParent(parent, false);
            var panel = gameObject.AddComponent<TabletopEventPanel3D>();
            gameObject.SetActive(false);
            return panel;
        }

        public void Present(Vector3 worldPosition, string title, string body, string footer, TabletopEventPrimaryTone tone, IReadOnlyList<TabletopEventChoicePresentation> choices)
        {
            ClearCards();
            transform.position = worldPosition;
            transform.rotation = Quaternion.identity;
            gameObject.SetActive(true);
            primaryCard = TabletopEventPrimaryCard3D.Create(transform);
            primaryCard.Present(title, body, footer, tone);
            int count = choices?.Count ?? 0;
            for (int index = 0; index < count; index++)
            {
                TabletopEventChoicePresentation choice = choices[index];
                TabletopEventChoiceCard3D card = TabletopEventChoiceCard3D.Create(transform, TabletopEventLayout.GetChoiceLocalPosition(index, count));
                card.Present(choice.Title, choice.Body, choice.Interactable, choice.Status, choice.Selected);
                choiceCards.Add(card);
                if (choice.Interactable)
                    InteractableChoiceCount++;
            }
        }

        public void Close()
        {
            ClearCards();
            gameObject.SetActive(false);
        }

        private void ClearCards()
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            primaryCard = null;
            choiceCards.Clear();
            InteractableChoiceCount = 0;
        }

        private void OnDestroy()
        {
            choiceCards.Clear();
            primaryCard = null;
        }
    }
}
