using Cysharp.Threading.Tasks;
using HuntingInDarkness.GameCore.Hunt;
using TMPro;
using UnityEngine;

namespace UI.Hunt
{
    /// <summary>狩猎资源点的实体翻牌。表现层只显示 ActionQueue 已提交的逐卡结果。</summary>
    public sealed class HuntHarvestCard3D : Cards3D.CardView3D
    {
        private static readonly Color FaceDownColor = new(0.12f, 0.18f, 0.22f);
        private static readonly Color ActiveColor = new(0.24f, 0.42f, 0.50f);
        private static readonly Color HitColor = new(0.30f, 0.52f, 0.30f);
        private static readonly Color MissColor = new(0.42f, 0.20f, 0.18f);

        private int cardIndex;
        private string resourceName;
        private TextMeshPro titleText;
        private TextMeshPro resultText;
        private bool isActive;
        private bool isRevealed;
        private bool isFlipping;
        private bool revealedHit;

        public int CardIndex => cardIndex;
        public bool IsRevealed => isRevealed;
        public System.Action<int> RevealRequested;
        public override string DisplayName => isRevealed ? resultText?.text ?? resourceName : $"采集牌 {cardIndex + 1}";

        public static HuntHarvestCard3D Create(int cardIndex, string resourceName, Transform parent, Vector3 localPosition)
        {
            var gameObject = new GameObject($"HarvestCard_{cardIndex}");
            gameObject.transform.SetParent(parent, false);
            var card = gameObject.AddComponent<HuntHarvestCard3D>();
            card.cardIndex = cardIndex;
            card.resourceName = string.IsNullOrWhiteSpace(resourceName) ? "资源" : resourceName;
            card.InitView(localPosition);
            card.transform.localPosition = localPosition;
            return card;
        }

        public void SetActiveCard(bool value)
        {
            isActive = value && !isRevealed && !isFlipping;
            ApplyVisuals();
        }

        public async UniTask RevealAsync(HarvestCardResult result)
        {
            if (isRevealed || result.CardIndex != cardIndex) return;
            isActive = false;
            isFlipping = true;
            var cancellationToken = this.GetCancellationTokenOnDestroy();
            const float halfDuration = 0.14f;
            try
            {
                for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
                {
                    transform.localEulerAngles = new Vector3(Mathf.Lerp(0f, 90f, elapsed / halfDuration), 0f, 0f);
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
                isRevealed = true;
                revealedHit = result.IsHit;
                string materialName = string.IsNullOrWhiteSpace(result.MaterialName) ? resourceName : result.MaterialName;
                resultText.text = result.IsHit ? $"获得\n{materialName}" : $"{materialName}\n落空";
                ApplyVisuals();
                for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
                {
                    transform.localEulerAngles = new Vector3(Mathf.Lerp(-90f, 0f, elapsed / halfDuration), 0f, 0f);
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
            finally
            {
                transform.localEulerAngles = Vector3.zero;
                isFlipping = false;
                ApplyVisuals();
            }
        }

        protected override Cards3D.CardCategory GetDefaultCategory() => Cards3D.CardCategory.Resource;

        protected override bool CanHover() => isActive && !isFlipping;

        protected override void BuildTextFields()
        {
            float textHeight = Depth * 0.5f + 0.003f;
            titleText = MakeText("Title", new Vector3(0f, textHeight, Height * 0.30f), 0.075f, TextAlignmentOptions.Center, new Vector2(Width - 0.08f, 0.18f));
            resultText = MakeText("Result", new Vector3(0f, textHeight, -Height * 0.02f), 0.095f, TextAlignmentOptions.Center, new Vector2(Width - 0.08f, 0.46f));
        }

        protected override void ApplyVisuals()
        {
            if (_bodyRenderer == null) return;
            _bodyRenderer.material.color = isRevealed ? revealedHit ? HitColor : MissColor : isActive || IsHovered ? ActiveColor : FaceDownColor;
            if (titleText == null) return;
            titleText.text = isRevealed ? $"第 {cardIndex + 1} 张" : "素材池";
            titleText.color = new Color(0.78f, 0.84f, 0.86f);
            if (!isRevealed)
                resultText.text = isActive ? "点击翻开" : "?";
            resultText.color = Color.white;
        }

        protected override void OnMouseDown()
        {
            if (!isActive || isFlipping || isRevealed) return;
            RevealRequested?.Invoke(cardIndex);
        }
    }
}
