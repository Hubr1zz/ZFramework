using HuntingInDarkness.ViewLayer.Tabletop;
using Cards3D;
using UnityEngine;

namespace UI
{
    /// <summary>营地额外物理区域的开合与局部取景控制。</summary>
    [DisallowMultipleComponent]
    public sealed class SettlementExpansionAreaController : MonoBehaviour
    {
        [SerializeField] private GameObject contentRoot;
        [SerializeField] private Renderer backgroundRenderer;
        [SerializeField] private Transform closeControl;
        [SerializeField] private TabletopCameraFrameLease cameraFrameLease;
        [SerializeField] private SlotGrid[] contentGrids;
        [SerializeField] private Transform[] additionalBoundsRoots;
        [SerializeField] private Transform headerControl;
        [SerializeField] private SettlementExpansionAreaController[] exclusivePeers;
        [SerializeField] private bool openOnStart;
        private bool warnedMissingLease;

        public bool IsOpen { get; private set; }
        public Transform CloseControl => closeControl;
        public GameObject ContentRoot => contentRoot;

        private void Awake()
        {
            ClickProxy proxy = closeControl?.GetComponent<ClickProxy>();
            if (proxy != null) proxy.OnClick = Close;
            SubscribeGridEvents();
            if (openOnStart) Open();
            else Close();
        }

        public void Configure(GameObject content, Renderer background, Transform close, TabletopCameraFrameLease lease)
        {
            contentRoot = content;
            backgroundRenderer = background;
            closeControl = close;
            cameraFrameLease = lease;
        }

        private void OnDisable()
        {
            if (contentRoot != null) contentRoot.SetActive(false);
            IsOpen = false;
            cameraFrameLease?.Release();
        }

        private void OnDestroy()
        {
            UnsubscribeGridEvents();
        }

        public void SetExclusivePeers(params SettlementExpansionAreaController[] peers) => exclusivePeers = peers;

        public void Toggle()
        {
            if (HasDraggingCard()) return;
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (HasDraggingCard()) return;
            foreach (SettlementExpansionAreaController peer in exclusivePeers ?? System.Array.Empty<SettlementExpansionAreaController>())
                if (peer != null && peer != this) peer.Close();
            if (contentRoot != null) contentRoot.SetActive(true);
            IsOpen = true;
            RefreshFrame();
        }

        public void Close()
        {
            if (HasDraggingCard()) return;
            if (contentRoot != null) contentRoot.SetActive(false);
            IsOpen = false;
            cameraFrameLease?.Release();
        }

        public void SetBackgroundSize(Vector2 size)
        {
            if (backgroundRenderer == null) return;
            backgroundRenderer.transform.localScale = new Vector3(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y), 1f);
            backgroundRenderer.transform.localPosition = new Vector3(backgroundRenderer.transform.localPosition.x, -0.03f, backgroundRenderer.transform.localPosition.z);
            if (IsOpen) RefreshFrame();
        }

        public void SetContentLayout(SlotGrid[] grids, Transform header, params Transform[] additionalRoots)
        {
            UnsubscribeGridEvents();
            contentGrids = grids;
            additionalBoundsRoots = additionalRoots;
            headerControl = header;
            SubscribeGridEvents();
        }

        public void RefreshFrame()
        {
            if (!IsOpen) return;
            RefreshContentLayout();
            AcquireFrame();
        }

        public bool TryGetWorldBounds(out Bounds bounds)
        {
            return TabletopCameraFraming.TryCalculateWorldBounds(gameObject.transform, out bounds);
        }

        private void AcquireFrame()
        {
            if (cameraFrameLease == null)
            {
                if (!warnedMissingLease)
                {
                    Debug.LogWarning($"[{nameof(SettlementExpansionAreaController)}] {name} 未绑定 cameraFrameLease，扩展区仍可显示但不会独立取景。", this);
                    warnedMissingLease = true;
                }
                return;
            }
            if (TryGetWorldBounds(out Bounds bounds)) cameraFrameLease.Acquire(bounds, 0);
        }

        private void SubscribeGridEvents()
        {
            foreach (SlotGrid grid in contentGrids ?? System.Array.Empty<SlotGrid>())
                if (grid != null) grid.LayoutChanged += OnGridLayoutChanged;
        }

        private void UnsubscribeGridEvents()
        {
            foreach (SlotGrid grid in contentGrids ?? System.Array.Empty<SlotGrid>())
                if (grid != null) grid.LayoutChanged -= OnGridLayoutChanged;
        }

        private void OnGridLayoutChanged() => RefreshFrame();

        private void RefreshContentLayout()
        {
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (SlotGrid grid in contentGrids ?? System.Array.Empty<SlotGrid>())
            {
                if (grid == null || !TabletopCameraFraming.TryCalculateWorldBounds(grid.transform, out Bounds gridBounds)) continue;
                if (!hasBounds)
                {
                    bounds = gridBounds;
                    hasBounds = true;
                    continue;
                }
                bounds.Encapsulate(gridBounds);
            }
            foreach (Transform root in additionalBoundsRoots ?? System.Array.Empty<Transform>())
            {
                if (root == null || !TabletopCameraFraming.TryCalculateWorldBounds(root, out Bounds rootBounds)) continue;
                if (!hasBounds)
                {
                    bounds = rootBounds;
                    hasBounds = true;
                    continue;
                }
                bounds.Encapsulate(rootBounds);
            }
            if (!hasBounds || contentRoot == null) return;

            Transform contentTransform = contentRoot.transform;
            Vector3 localCenter = contentTransform.InverseTransformPoint(bounds.center);
            Vector3 localExtents = contentTransform.InverseTransformVector(bounds.extents);
            float minZ = localCenter.z - Mathf.Abs(localExtents.z);
            float maxZ = localCenter.z + Mathf.Abs(localExtents.z);
            float minX = localCenter.x - Mathf.Abs(localExtents.x);
            float maxX = localCenter.x + Mathf.Abs(localExtents.x);
            float width = Mathf.Max(3.2f, maxX - minX + 0.6f);
            float depth = Mathf.Max(2f, maxZ - minZ + 1f);
            float headerZ = maxZ + 0.34f;
            if (backgroundRenderer != null)
            {
                backgroundRenderer.transform.localPosition = new Vector3((minX + maxX) * 0.5f, -0.03f, (minZ + maxZ + 0.65f) * 0.5f);
                backgroundRenderer.transform.localScale = new Vector3(width, depth, 1f);
            }
            if (headerControl != null)
                headerControl.localPosition = new Vector3((minX + maxX) * 0.5f, 0.03f, headerZ);
            if (closeControl != null)
            {
                Transform closeRoot = closeControl.parent != null ? closeControl.parent : closeControl;
                closeRoot.localPosition = new Vector3(maxX - 0.18f, 0.03f, headerZ);
                closeControl.localPosition = Vector3.zero;
            }
        }

        private static bool HasDraggingCard()
        {
            foreach (CardView3D card in CardView3D.AllCards)
                if (card != null && card.IsDragging) return true;
            return false;
        }
    }
}
