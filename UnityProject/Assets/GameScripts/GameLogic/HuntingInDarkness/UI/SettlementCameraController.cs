using UnityEngine;

namespace UI
{
    /// <summary>
    /// 营地阶段相机控制器。
    /// OnEnable 时平滑过渡到固定等距俯视位置，支持滚轮沿视线方向缩放。
    /// 挂在 Main Camera 上，由 GameCameraManager 统一启用/禁用。
    /// </summary>
    [AddComponentMenu("CardTactics/Settlement Camera Controller")]
    [DisallowMultipleComponent]
    public class SettlementCameraController : MonoBehaviour
    {
        [Header("默认位置 / 角度")]
        [SerializeField] private Vector3 defaultPosition = new Vector3(0f, 12f, -4f);
        [SerializeField] private Vector3 defaultEulerAngles = new Vector3(65f, 0f, 0f);

        [Header("过渡速度")]
        [SerializeField] private float transitionSpeed = 5f;

        [Header("滚轮缩放（沿视线方向）")]
        [SerializeField] private bool  enableScrollZoom = true;
        [SerializeField] private float zoomSpeed        = 3f;
        [SerializeField] private float minHeight        = 5f;
        [SerializeField] private float maxHeight        = 22f;

        // 当前插值目标
        private Vector3    _targetPos;
        private Quaternion _targetRot;

        // 视线方向（由 defaultEulerAngles 决定，OnEnable 时计算一次）
        private Vector3 _viewForward;

        private void OnEnable()
        {
            // 进入营地阶段时，重置到配置的默认位置
            _targetPos    = defaultPosition;
            _targetRot    = Quaternion.Euler(defaultEulerAngles);
            _viewForward  = _targetRot * Vector3.forward;
        }

        private void Update()
        {
            HandleScrollZoom();

            transform.position = Vector3.Lerp(
                transform.position, _targetPos, Time.deltaTime * transitionSpeed);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, _targetRot, Time.deltaTime * transitionSpeed);
        }

        private void HandleScrollZoom()
        {
            if (!enableScrollZoom) return;

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) < 0.001f) return;

            // 沿视线方向缩放（前推 / 后拉），同时限制高度区间
            Vector3 candidate = _targetPos + _viewForward * (scroll * zoomSpeed * 10f);
            if (candidate.y >= minHeight && candidate.y <= maxHeight)
                _targetPos = candidate;
        }
    }
}
