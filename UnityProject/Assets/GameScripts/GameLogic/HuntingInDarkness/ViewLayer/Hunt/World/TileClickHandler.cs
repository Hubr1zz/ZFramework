using UnityEngine;
using UnityEngine.EventSystems;

namespace HuntingInDarkness.Hunt
{
    /// <summary>挂在每个地块 GameObject 上，接收鼠标点击事件</summary>
    public class TileClickHandler : MonoBehaviour
    {
        public Vector2Int        Coord;
        [System.NonSerialized] public HuntManager HuntMgr;
        public HuntMapVisualizer Visualizer;

        private void OnMouseDown()
        {
            // 不穿透 UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            HuntMgr?.OnTileClicked(Coord);
        }
    }
}
