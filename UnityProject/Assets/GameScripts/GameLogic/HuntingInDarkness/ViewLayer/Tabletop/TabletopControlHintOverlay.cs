using Cards3D;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    /// <summary>阶段相机启用时展示桌面通用操作；H 或右下角按钮切换提示条。</summary>
    [DisallowMultipleComponent]
    public sealed class TabletopControlHintOverlay : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float defaultDuration = 12f;
        [SerializeField] private KeyCode toggleKey = KeyCode.H;
        [SerializeField] private GameObject expandedRoot;
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private Button helpButton;

        private Object owner;
        private string hint = string.Empty;
        private float visibleUntil;

        public bool IsVisible => owner != null && expandedRoot != null && expandedRoot.activeSelf;
        public string CurrentHint => hint;

        public void Show(Object hintOwner, string message)
        {
            if (hintOwner == null || string.IsNullOrWhiteSpace(message)) return;
            owner = hintOwner;
            hint = message;
            visibleUntil = Time.unscaledTime + defaultDuration;
            if (hintLabel != null) hintLabel.text = message;
            SetHelpVisible(true);
            SetExpandedVisible(true);
            enabled = true;
        }

        public void Hide(Object hintOwner)
        {
            if (owner != hintOwner) return;
            owner = null;
            hint = string.Empty;
            visibleUntil = 0f;
            SetExpandedVisible(false);
            SetHelpVisible(false);
            enabled = false;
        }

        private void Awake()
        {
            helpButton?.onClick.AddListener(ToggleFromButton);
            SetExpandedVisible(false);
            SetHelpVisible(false);
        }

        private void Update()
        {
            if (owner == null)
            {
                SetExpandedVisible(false);
                SetHelpVisible(false);
                enabled = false;
                return;
            }

            if (IsVisible && Time.unscaledTime > visibleUntil)
                SetExpandedVisible(false);

            if (CardInspectionOverlay.BlocksWorldInput) return;
            if (Input.GetKeyDown(toggleKey)) Toggle();
        }

        private void ToggleFromButton()
        {
            if (CardInspectionOverlay.BlocksWorldInput) return;
            Toggle();
        }

        private void Toggle()
        {
            if (owner == null) return;
            if (IsVisible)
            {
                SetExpandedVisible(false);
                return;
            }

            visibleUntil = Time.unscaledTime + defaultDuration;
            SetExpandedVisible(true);
        }

        private void SetExpandedVisible(bool visible)
        {
            if (expandedRoot != null && expandedRoot.activeSelf != visible)
                expandedRoot.SetActive(visible);
        }

        private void SetHelpVisible(bool visible)
        {
            if (helpButton != null && helpButton.gameObject.activeSelf != visible)
                helpButton.gameObject.SetActive(visible);
        }

        private void OnDestroy()
        {
            helpButton?.onClick.RemoveListener(ToggleFromButton);
            owner = null;
            hint = string.Empty;
        }
    }
}
