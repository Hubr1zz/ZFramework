using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using TMPro;

namespace HuntingInDarkness.ViewLayer.Presentation
{
    public sealed class PhysicalSelectionCard3D : MonoBehaviour
    {
        [SerializeField] private Collider cardCollider;
        [SerializeField] private Renderer cardRenderer;
        [SerializeField] private TMP_Text faceText;
        [SerializeField] private Material cardBackMaterial;
        [SerializeField] private Material cardFaceMaterial;
        [SerializeField] private Color cardBackTextColor = new(0.93f, 0.78f, 0.38f, 1f);
        [SerializeField] private Color cardFaceTextColor = new(0.20f, 0.12f, 0.07f, 1f);
        [SerializeField, Min(0.05f)] private float flipDuration = 0.25f;

        private int originalIndex;
        private bool selectable;
        private bool revealed;

        public int OriginalIndex => originalIndex;
        public bool IsSelectable => selectable && !revealed;
        public Action<PhysicalSelectionCard3D> Selected { get; set; }

        public void Configure(int index, bool canSelect)
        {
            ValidateReferences();
            originalIndex = index;
            selectable = canSelect;
            revealed = false;
            cardCollider.enabled = canSelect;
            faceText.text = "◆";
            faceText.color = cardBackTextColor;
            cardRenderer.sharedMaterial = cardBackMaterial;
        }

        public void SetSelectable(bool value)
        {
            selectable = value && !revealed;
            cardCollider.enabled = selectable;
        }

        public void Reveal(string resultText, string faceTitle = null)
        {
            ValidateReferences();
            selectable = false;
            revealed = true;
            cardCollider.enabled = false;
            faceText.text = string.IsNullOrWhiteSpace(faceTitle) ? resultText ?? string.Empty : faceTitle;
            faceText.color = cardFaceTextColor;
            cardRenderer.sharedMaterial = cardFaceMaterial;
        }

        public async UniTask RevealAsync(string resultText, CancellationToken cancellationToken, string faceTitle = null)
        {
            ValidateReferences();
            selectable = false;
            cardCollider.enabled = false;
            Quaternion originalRotation = transform.localRotation;
            revealed = true;
            float startedAt = Time.unscaledTime;
            bool faceVisible = false;
            try
            {
                while (Time.unscaledTime - startedAt < flipDuration)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    float progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / flipDuration);
                    float edgeAngle = progress < 0.5f ? Mathf.Lerp(0f, 90f, progress * 2f) : Mathf.Lerp(-90f, 0f, (progress - 0.5f) * 2f);
                    transform.localRotation = originalRotation * Quaternion.Euler(0f, 0f, edgeAngle);
                    if (!faceVisible && progress >= 0.5f)
                    {
                        faceText.text = string.IsNullOrWhiteSpace(faceTitle) ? resultText ?? string.Empty : faceTitle;
                        faceText.color = cardFaceTextColor;
                        cardRenderer.sharedMaterial = cardFaceMaterial;
                        faceVisible = true;
                    }
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
            finally
            {
                transform.localRotation = originalRotation;
                faceText.text = string.IsNullOrWhiteSpace(faceTitle) ? resultText ?? string.Empty : faceTitle;
                faceText.color = cardFaceTextColor;
                cardRenderer.sharedMaterial = cardFaceMaterial;
            }
        }

        public void SelectFromStagePointer()
        {
            if (IsSelectable) Selected?.Invoke(this);
        }

        private void ValidateReferences()
        {
            if (cardCollider == null || cardRenderer == null || faceText == null || cardBackMaterial == null || cardFaceMaterial == null)
                throw new MissingReferenceException($"[{nameof(PhysicalSelectionCard3D)}] 3D 卡牌引用未完整绑定。对象：{name}");
        }
    }
}

