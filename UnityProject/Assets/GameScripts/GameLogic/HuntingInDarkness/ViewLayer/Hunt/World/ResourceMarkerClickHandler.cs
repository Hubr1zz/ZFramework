using UnityEngine;
using UnityEngine.EventSystems;
using Cysharp.Threading.Tasks;

namespace HuntingInDarkness.Hunt
{
    /// <summary>把世界空间资源标记的点击转换为 HuntManager 命令。</summary>
    public sealed class ResourceMarkerClickHandler : MonoBehaviour
    {
        private IHuntExplorationPort explorationPort;
        private Vector2Int tileCoordinate;
        private int pointIndex = -1;

        public void Initialize(HuntManager huntManager, Vector2Int coordinate)
        {
            explorationPort = null;
            tileCoordinate = coordinate;
            pointIndex = -1;
        }

        public void Initialize(HuntManager huntManager, Vector2Int coordinate, int resourcePointIndex)
        {
            explorationPort = null;
            tileCoordinate = coordinate;
            pointIndex = resourcePointIndex;
        }

        public void Initialize(HuntManager huntManager, IHuntExplorationPort port, Vector2Int coordinate, int resourcePointIndex)
        {
            explorationPort = port;
            tileCoordinate = coordinate;
            pointIndex = resourcePointIndex;
        }

        private void OnMouseDown()
        {
            if (explorationPort == null) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (GetComponentInParent<PlayableHexTileCard3D>()?.IsFlipping == true) return;

            if (!explorationPort.TryCreateSnapshot(tileCoordinate, pointIndex, out HuntExplorationSnapshot snapshot)) return;
            SubmitAsync(snapshot).Forget();
        }

        private async UniTaskVoid SubmitAsync(HuntExplorationSnapshot snapshot)
        {
            await explorationPort.SubmitResourcePointAsync(snapshot);
        }
    }
}
