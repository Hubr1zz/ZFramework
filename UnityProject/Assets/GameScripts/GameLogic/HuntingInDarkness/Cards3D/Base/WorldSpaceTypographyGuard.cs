using TMPro;
using UnityEngine;

namespace Cards3D
{
    /// <summary>为世界空间桌面根提供统一的最低可读字号，并覆盖运行时后续生成的面板条目。</summary>
    [DisallowMultipleComponent]
    public sealed class WorldSpaceTypographyGuard : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float minimumTitleFontSize = 3f;
        [SerializeField, Min(0.1f)] private float minimumBodyFontSize = 2.7f;
        [SerializeField, Min(0f)] private float verticalPadding = 0.04f;
        private bool dirty = true;
        private bool applying;

        public void Configure(float titleFontSize, float bodyFontSize)
        {
            minimumTitleFontSize = Mathf.Max(0.1f, titleFontSize);
            minimumBodyFontSize = Mathf.Max(0.1f, bodyFontSize);
            ApplyNow();
        }

        public void ApplyNow()
        {
            applying = true;
            foreach (TextMeshPro text in GetComponentsInChildren<TextMeshPro>(true))
            {
                if (text.GetComponentInParent<CardView3D>() != null)
                    continue;
                if (text.GetComponentInParent<WorldSpaceViewPanel>() != null)
                    continue;

                float minimum = IsTitle(text.gameObject.name) ? minimumTitleFontSize : minimumBodyFontSize;
                text.fontSize = Mathf.Max(text.fontSize, minimum);
                ExpandHeightToFit(text);
            }
            applying = false;
            dirty = false;
        }

        private void OnEnable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            dirty = true;
        }

        private void OnDisable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

        private void OnTransformChildrenChanged() => dirty = true;

        private void LateUpdate()
        {
            if (dirty)
                ApplyNow();
        }

        private void OnTextChanged(Object changedObject)
        {
            if (applying || changedObject is not TextMeshPro text || !text.transform.IsChildOf(transform))
                return;

            dirty = true;
        }

        private void ExpandHeightToFit(TextMeshPro text)
        {
            RectTransform rectTransform = text.rectTransform;
            Vector2 size = rectTransform.sizeDelta;
            float availableWidth = Mathf.Max(0.01f, size.x);
            float requiredHeight = text.GetPreferredValues(text.text, availableWidth, 0f).y + verticalPadding;
            if (requiredHeight <= size.y)
                return;

            rectTransform.sizeDelta = new Vector2(size.x, requiredHeight);
        }

        private static bool IsTitle(string objectName) => objectName == "Title" || objectName == "Name" || objectName == "GridLabel";
    }
}
