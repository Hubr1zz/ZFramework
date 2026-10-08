using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    public readonly struct TabletopEventChoicePresentation
    {
        public TabletopEventChoicePresentation(string title, string body, bool interactable, string status, Action selected)
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
        public Action Selected { get; }
    }

    public sealed class TabletopEventPanel3D : MonoBehaviour
    {
        [Header("Screen UI 引用")]
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private Image windowImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text footerText;
        [SerializeField] private ScrollRect bodyScrollRect;
        [SerializeField] private ScrollRect choicesScrollRect;
        [SerializeField] private RectTransform choiceContent;
        [SerializeField] private ScreenEventChoice choiceTemplate;

        private readonly List<ScreenEventChoice> pooledChoices = new();
        private readonly List<ScreenEventChoice> activeChoices = new();
        private IDisposable modalLease;
        private bool referencesValidated;

        public bool IsOpen => modalRoot != null && modalRoot.activeSelf;
        public int ChoiceCardCount => activeChoices.Count;
        public int InteractableChoiceCount { get; private set; }
        public string Title => titleText != null ? titleText.text : string.Empty;
        public string Body => bodyText != null ? bodyText.text : string.Empty;
        public string Footer => footerText != null ? footerText.text : string.Empty;
        public IReadOnlyList<ScreenEventChoice> Choices => activeChoices;

        public static TabletopEventPanel3D Create(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            TabletopEventPanel3D prefab = TabletopPresentationAssets.EventPanelPrefab;
            TabletopEventPanel3D panel = Instantiate(prefab, parent, false);
            panel.name = prefab.name;
            panel.gameObject.SetActive(true);
            panel.Close();
            return panel;
        }

        public void Present(Vector3 worldPosition, string title, string body, string footer, TabletopEventPrimaryTone tone, IReadOnlyList<TabletopEventChoicePresentation> choices)
        {
            ValidateReferences();
            if (!IsOpen)
            {
                modalRoot.SetActive(true);
                if (isActiveAndEnabled && modalRoot.activeInHierarchy)
                {
                    modalLease?.Dispose();
                    modalLease = ScreenModalInputGate.Acquire(this);
                }
            }

            titleText.text = title ?? string.Empty;
            bodyText.text = body ?? string.Empty;
            footerText.text = footer ?? string.Empty;
            ApplyTone(tone);
            RebuildChoices(choices);
            Canvas.ForceUpdateCanvases();
            bodyScrollRect.verticalNormalizedPosition = 1f;
            choicesScrollRect.verticalNormalizedPosition = 1f;
        }

        public void Close()
        {
            modalRoot?.SetActive(false);
            foreach (ScreenEventChoice choice in pooledChoices)
                if (choice != null)
                    choice.gameObject.SetActive(false);
            activeChoices.Clear();
            InteractableChoiceCount = 0;
            modalLease?.Dispose();
            modalLease = null;
        }

        private void Awake()
        {
            ValidateReferences();
        }

        private void OnEnable()
        {
            if (modalRoot == null || !modalRoot.activeSelf || !modalRoot.activeInHierarchy) return;
            ValidateReferences();
            modalLease?.Dispose();
            modalLease = ScreenModalInputGate.Acquire(this);
        }

        private void OnDisable()
        {
            modalLease?.Dispose();
            modalLease = null;
        }

        private void OnDestroy()
        {
            modalLease?.Dispose();
            modalLease = null;
        }

        private void ValidateReferences()
        {
            if (referencesValidated) return;
            if (rootCanvas == null || modalRoot == null || windowImage == null || titleText == null || bodyText == null || footerText == null || bodyScrollRect == null || choicesScrollRect == null || choiceContent == null || choiceTemplate == null)
                throw new MissingReferenceException($"[{nameof(TabletopEventPanel3D)}] 事件屏幕面板引用未完整绑定。\n对象：{name}");
            referencesValidated = true;
        }

        private void RebuildChoices(IReadOnlyList<TabletopEventChoicePresentation> choices)
        {
            foreach (ScreenEventChoice choice in activeChoices)
                if (choice != null)
                    choice.gameObject.SetActive(false);
            activeChoices.Clear();
            InteractableChoiceCount = 0;
            int count = choices?.Count ?? 0;
            for (int index = 0; index < count; index++)
            {
                TabletopEventChoicePresentation presentation = choices[index];
                ScreenEventChoice choice = GetChoice(index);
                choice.Configure(presentation.Title, presentation.Body, presentation.Interactable, presentation.Status, presentation.Selected);
                activeChoices.Add(choice);
                if (presentation.Interactable) InteractableChoiceCount++;
            }
        }

        private ScreenEventChoice GetChoice(int index)
        {
            while (pooledChoices.Count <= index)
            {
                ScreenEventChoice choice = Instantiate(choiceTemplate, choiceContent);
                choice.name = $"ScreenEventChoice_{pooledChoices.Count}";
                pooledChoices.Add(choice);
            }
            return pooledChoices[index];
        }

        private void ApplyTone(TabletopEventPrimaryTone tone)
        {
            windowImage.color = tone switch
            {
                TabletopEventPrimaryTone.Check => new Color(0.08f, 0.12f, 0.19f, 0.98f),
                TabletopEventPrimaryTone.Success => new Color(0.08f, 0.19f, 0.13f, 0.98f),
                TabletopEventPrimaryTone.Failure => new Color(0.22f, 0.08f, 0.09f, 0.98f),
                _ => new Color(0.07f, 0.08f, 0.13f, 0.98f)
            };
        }
    }

}
