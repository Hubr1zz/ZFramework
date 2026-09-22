using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    /// <summary>阶段相机启用时短暂展示桌面通用操作；H 可随时重新显示或固定提示。</summary>
    [DisallowMultipleComponent]
    public sealed class TabletopControlHintOverlay : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float defaultDuration = 12f;
        [SerializeField] private KeyCode toggleKey = KeyCode.H;
        private Object owner;
        private string hint;
        private float visibleUntil;
        private bool pinned;
        private GUIStyle hintStyle;

        public bool IsVisible => owner != null && (pinned || Time.unscaledTime <= visibleUntil);
        public string CurrentHint => hint ?? string.Empty;

        public void Show(Object hintOwner, string message)
        {
            if (hintOwner == null || string.IsNullOrWhiteSpace(message)) return;
            owner = hintOwner;
            hint = message;
            visibleUntil = Time.unscaledTime + defaultDuration;
            pinned = false;
            enabled = true;
        }

        public void Hide(Object hintOwner)
        {
            if (owner != hintOwner) return;
            owner = null;
            hint = string.Empty;
            pinned = false;
            enabled = false;
        }

        private void Update()
        {
            if (owner == null)
            {
                enabled = false;
                return;
            }

            if (!Input.GetKeyDown(toggleKey)) return;
            pinned = !IsVisible || !pinned;
            visibleUntil = Time.unscaledTime + defaultDuration;
        }

        private void OnGUI()
        {
            if (!IsVisible) return;
            hintStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 15, wordWrap = true };
            float width = Mathf.Min(760f, Screen.width - 32f);
            float height = 38f;
            GUI.Box(new Rect((Screen.width - width) * 0.5f, Screen.height - height - 18f, width, height), hint, hintStyle);
        }
    }
}
