using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    /// <summary>让临时桌面对象独立持有相机取景请求，禁用或销毁时必定释放。</summary>
    [DisallowMultipleComponent]
    public sealed class TabletopCameraFrameLease : MonoBehaviour
    {
        public void Acquire(Bounds bounds, int priority) => TabletopCameraFraming.Request(this, bounds, priority);

        public void Release() => TabletopCameraFraming.Release(this);

        private void OnDisable() => Release();

        private void OnDestroy() => Release();
    }
}
