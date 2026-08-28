using UnityEngine;
using UnityEngine.EventSystems;

namespace HuntingInDarkness.Hunt
{
    /// <summary>挂在每个地块 GameObject 上，接收鼠标点击事件</summary>
    public class TileClickHandler : MonoBehaviour
    {
        public Vector2Int        Coord;
        public HuntMapVisualizer Visualizer;

        private void OnMouseDown()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            HandleResolvedPointerClick();
        }

        /// <summary>统一接收鼠标、触摸或控制器对实体地块的点击；只转发玩法意图。</summary>
        public void HandleResolvedPointerClick()
        {
            Visualizer?.HandleTileClicked(Coord);
        }
    }
}
