using UnityEngine;
using UnityEngine.EventSystems;

namespace HuntingInDarkness.Hunt
{
    /// <summary>把世界空间资源标记的点击转换为 HuntManager 命令。</summary>
    public sealed class ResourceMarkerClickHandler : MonoBehaviour
    {
        private HuntManager manager;
        private Vector2Int tileCoordinate;
        private int pointIndex = -1;

        public void Initialize(HuntManager huntManager, Vector2Int coordinate)
        {
            manager = huntManager;
            tileCoordinate = coordinate;
            pointIndex = -1;
        }

        public void Initialize(HuntManager huntManager, Vector2Int coordinate, int resourcePointIndex)
        {
            manager = huntManager;
            tileCoordinate = coordinate;
            pointIndex = resourcePointIndex;
        }

        private void OnMouseDown()
        {
            if (manager == null) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (GetComponentInParent<PlayableHexTileCard3D>()?.IsFlipping == true) return;

            var tile = manager.GetTile(tileCoordinate);
            if (tile == null) return;

            int resolvedPointIndex = pointIndex >= 0 ? pointIndex : tile.ResourcePoints.FindIndex(point => !point.IsExhausted);
            if (resolvedPointIndex < 0) return;
            manager.OnResourcePointSelected(tileCoordinate, resolvedPointIndex);
        }
    }
}
