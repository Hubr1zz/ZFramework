using UnityEngine;
using UnityEngine.EventSystems;

namespace HuntingInDarkness.Hunt
{
    /// <summary>把世界空间资源标记的点击转换为 HuntManager 命令。</summary>
    public sealed class ResourceMarkerClickHandler : MonoBehaviour
    {
        private HuntManager manager;
        private Vector2Int tileCoordinate;

        public void Initialize(HuntManager huntManager, Vector2Int coordinate)
        {
            manager = huntManager;
            tileCoordinate = coordinate;
        }

        private void OnMouseDown()
        {
            if (manager == null) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            var tile = manager.GetTile(tileCoordinate);
            if (tile == null) return;

            int pointIndex = tile.ResourcePoints.FindIndex(point => !point.IsExhausted);
            if (pointIndex < 0) return;
            manager.OnResourcePointSelected(tileCoordinate, pointIndex);
        }
    }
}
