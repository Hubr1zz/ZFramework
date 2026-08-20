using HuntingInDarkness.Data;
using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    /// <summary>六边形地形卡的正反面与翻面表现；只投影已提交的地块状态。</summary>
    [DisallowMultipleComponent]
    public sealed class PlayableHexTileCard3D : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float flipDuration = 0.42f;

        private TextMesh frontLabel;
        private TextMesh backLabel;
        private Collider tileCollider;
        private Quaternion flipStart;
        private Quaternion flipTarget;
        private TileState currentState;
        private float flipElapsed;
        private bool hasState;
        private bool isFlipping;

        public TileState CurrentState => currentState;
        public bool IsFaceUp => hasState && currentState == TileState.Revealed && !isFlipping;
        public bool IsFlipping => isFlipping;
        public float FlipProgress => !isFlipping || flipDuration <= 0f ? 1f : Mathf.Clamp01(flipElapsed / flipDuration);

        public void Initialize(float radius, float thickness)
        {
            tileCollider = GetComponent<Collider>();
            frontLabel = CreateLabel("Front Label", new Vector3(0f, thickness * 0.5f + 0.025f, 0f), Quaternion.Euler(90f, 0f, 0f), radius);
            backLabel = CreateLabel("Back Label", new Vector3(0f, -thickness * 0.5f - 0.025f, 0f), Quaternion.Euler(-90f, 0f, 180f), radius);
            backLabel.color = new Color(0.82f, 0.92f, 1f);
        }

        public void Present(HexTileInstance tile, TileState state)
        {
            bool wasRevealed = hasState && currentState == TileState.Revealed;
            currentState = state;
            UpdateLabels(tile, state);
            if (!hasState)
            {
                hasState = true;
                SetFace(state == TileState.Revealed);
                return;
            }
            if (wasRevealed == (state == TileState.Revealed))
                return;
            BeginFlip(state == TileState.Revealed);
        }

        private void Update()
        {
            if (!isFlipping)
                return;
            flipElapsed += Time.deltaTime;
            float progress = flipDuration <= 0f ? 1f : Mathf.Clamp01(flipElapsed / flipDuration);
            float easedProgress = progress * progress * (3f - 2f * progress);
            transform.localRotation = Quaternion.Slerp(flipStart, flipTarget, easedProgress);
            if (progress < 1f)
                return;
            transform.localRotation = flipTarget;
            isFlipping = false;
            SetColliderEnabled(true);
        }

        private void BeginFlip(bool faceUp)
        {
            flipElapsed = 0f;
            flipStart = transform.localRotation;
            flipTarget = ResolveRotation(faceUp);
            if (flipDuration <= 0f)
            {
                transform.localRotation = flipTarget;
                isFlipping = false;
                SetColliderEnabled(true);
                return;
            }
            isFlipping = true;
            SetColliderEnabled(false);
        }

        private void SetFace(bool faceUp)
        {
            isFlipping = false;
            flipElapsed = flipDuration;
            transform.localRotation = ResolveRotation(faceUp);
            SetColliderEnabled(true);
        }

        private void UpdateLabels(HexTileInstance tile, TileState state)
        {
            if (frontLabel != null)
            {
                frontLabel.text = string.Empty;
                if (state == TileState.Revealed)
                    frontLabel.text = tile?.Config != null ? tile.Config.tileName : "未知地块";
                frontLabel.color = Color.white;
            }
            if (backLabel != null)
                backLabel.text = state == TileState.Interactable ? "可探索" : string.Empty;
        }

        private static Quaternion ResolveRotation(bool faceUp) => faceUp ? Quaternion.identity : Quaternion.Euler(180f, 0f, 0f);

        private void SetColliderEnabled(bool enabled)
        {
            if (tileCollider != null)
                tileCollider.enabled = enabled;
        }

        private TextMesh CreateLabel(string labelName, Vector3 localPosition, Quaternion localRotation, float radius)
        {
            GameObject labelObject = new(labelName);
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = localPosition;
            labelObject.transform.localRotation = localRotation;
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 48;
            label.fontStyle = FontStyle.Bold;
            label.characterSize = radius * 0.025f;
            label.color = Color.white;
            return label;
        }

        private void OnDestroy()
        {
            Mesh mesh = GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh != null)
                Destroy(mesh);
            Material material = GetComponent<Renderer>()?.sharedMaterial;
            if (material != null)
                Destroy(material);
        }
    }
}
