using UnityEngine;
using UnityEngine.EventSystems;
using Cysharp.Threading.Tasks;

namespace HuntingInDarkness.Hunt
{
    /// <summary>挂在每个地块 GameObject 上，接收鼠标点击事件</summary>
    public class TileClickHandler : MonoBehaviour
    {
        public Vector2Int        Coord;
        [System.NonSerialized] public IHuntExplorationPort ExplorationPort;
        public HuntMapVisualizer Visualizer;

        private void OnMouseDown()
        {
            // 不穿透 UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (ExplorationPort == null || !ExplorationPort.TryCreateSnapshot(Coord, -1, out HuntExplorationSnapshot snapshot)) return;
            SubmitAsync(snapshot).Forget();
        }

        private async UniTaskVoid SubmitAsync(HuntExplorationSnapshot snapshot)
        {
            await ExplorationPort.SubmitTileAsync(snapshot);
        }
    }
}
