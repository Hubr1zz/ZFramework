using UnityEngine;

namespace Cards3D
{
    /// <summary>
    /// 极简可点击代理。挂在带 Collider 的 3D 物体上，点击时回调 OnClick。
    /// 用于面板上的「关闭」按钮等轻量交互。
    /// </summary>
    public class ClickProxy : MonoBehaviour
    {
        public System.Action OnClick;
        private void OnMouseDown() => OnClick?.Invoke();
    }
}
