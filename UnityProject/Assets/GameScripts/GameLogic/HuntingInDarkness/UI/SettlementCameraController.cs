using Cards3D;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.ViewLayer.Tabletop;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI
{
    /// <summary>
    /// 营地阶段相机控制器。
    /// OnEnable 时取景当前桌面，支持中键拖动、WASD 平移、滚轮缩放和 Home 复位。
    /// 挂在 Main Camera 上，由 PlayablePhaseCameraRig 按阶段启用或禁用。
    /// </summary>
    [AddComponentMenu("CardTactics/Settlement Camera Controller")]
    [DisallowMultipleComponent]
    public class SettlementCameraController : MonoBehaviour
    {
        [Header("默认位置 / 角度")]
        [SerializeField] private Vector3 defaultPosition = new Vector3(0f, 16f, -11f);
        [SerializeField] private Vector3 defaultEulerAngles = new Vector3(55f, 0f, 0f);
        [SerializeField, Range(25f, 65f)] private float perspectiveFieldOfView = 42f;
        [SerializeField, Min(1f)] private float framingPadding = 1.15f;
        [SerializeField, Min(1f)] private float minimumFramingDistance = 10f;

        [Header("桌面导航")]
        [SerializeField] private bool enablePan = true;
        [SerializeField, Min(0f)] private float keyboardPanSpeed = 5f;
        [SerializeField, Range(0, 2)] private int mousePanButton = 2;
        [SerializeField, Min(0f)] private float panBoundsPadding = 1.5f;
        [SerializeField] private bool enableScrollZoom = true;
        [SerializeField, Min(0f)] private float zoomSpeed = 1.5f;
        [SerializeField, Range(0.1f, 1f)] private float minimumZoomRatio = 0.45f;
        [SerializeField, Min(1f)] private float maximumZoomRatio = 1.65f;
        [SerializeField] private KeyCode resetViewKey = KeyCode.Home;
        [SerializeField] private string controlHint = "中键拖动 / WASD 移动 · 滚轮缩放 · Home 复位 · 悬停卡牌按 F 查看详情 · H 显示提示";

        // 当前插值目标
        private Vector3    _targetPos;
        private Quaternion _targetRot;
        private Vector3 _viewForward;
        private Vector3 focusPoint;
        private Vector3 homeFocusPoint;
        private float distance;
        private float homeDistance;
        private Bounds navigationBounds;
        private bool hasNavigationBounds;
        private bool navigationLocked;
        private bool isMousePanning;
        private Vector3 mousePanStart;
        private Camera controlledCamera;
        [SerializeField] private TabletopControlHintOverlay controlHintOverlay;
        private bool missingControlHintLogged;

        private void OnEnable()
        {
            controlledCamera = GetComponent<Camera>();
            if (controlledCamera == null) return;
            controlledCamera.orthographic = false;
            controlledCamera.fieldOfView = perspectiveFieldOfView;
            if (controlHintOverlay == null && !missingControlHintLogged)
            {
                Debug.LogWarning($"[{nameof(SettlementCameraController)}] 未绑定 {nameof(TabletopControlHintOverlay)}，跳过操作提示。", this);
                missingControlHintLogged = true;
            }
            controlHintOverlay?.Show(this, controlHint);
            TabletopCameraFraming.Changed += RefreshFraming;
            RefreshFraming();
            transform.SetPositionAndRotation(_targetPos, _targetRot);
        }

        private void OnDisable()
        {
            TabletopCameraFraming.Changed -= RefreshFraming;
            controlHintOverlay?.Hide(this);
            isMousePanning = false;
        }

        public void RefreshFraming()
        {
            controlledCamera ??= GetComponent<Camera>();
            if (controlledCamera != null && TabletopCameraFraming.TryGetActiveFrame(out Bounds bounds, out int priority))
            {
                TabletopCameraFraming.CalculatePerspectivePose(controlledCamera, bounds, defaultEulerAngles.x, defaultEulerAngles.y, framingPadding, minimumFramingDistance, out Vector3 framedPosition, out _targetRot);
                navigationBounds = bounds;
                hasNavigationBounds = true;
                navigationLocked = priority > 0;
                homeFocusPoint = bounds.center;
                focusPoint = homeFocusPoint;
                homeDistance = Vector3.Distance(framedPosition, homeFocusPoint);
                distance = homeDistance;
                _viewForward = _targetRot * Vector3.forward;
                UpdateTargetPose();
                return;
            }

            hasNavigationBounds = false;
            navigationLocked = false;
            _targetPos = defaultPosition;
            _targetRot = Quaternion.Euler(defaultEulerAngles);
            _viewForward = _targetRot * Vector3.forward;
            homeFocusPoint = _targetPos + _viewForward * minimumFramingDistance;
            focusPoint = homeFocusPoint;
            homeDistance = minimumFramingDistance;
            distance = homeDistance;
        }

        private void Update()
        {
            if (controlledCamera == null) return;
            if (!CanReceiveInput())
            {
                isMousePanning = false;
                transform.SetPositionAndRotation(_targetPos, _targetRot);
                return;
            }
            HandleReset();
            HandlePan();
            HandleScrollZoom();

            transform.SetPositionAndRotation(_targetPos, _targetRot);
        }

        private void HandleScrollZoom()
        {
            if (!enableScrollZoom || navigationLocked) return;

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.001f) return;
            float minimumDistance = Mathf.Max(1f, homeDistance * minimumZoomRatio);
            float maximumDistance = Mathf.Max(minimumDistance, homeDistance * maximumZoomRatio);
            distance = Mathf.Clamp(distance - scroll * zoomSpeed * GlobalGameSettings.CameraZoomSpeed, minimumDistance, maximumDistance);
            UpdateTargetPose();
        }

        private void HandleReset()
        {
            if (navigationLocked || !Input.GetKeyDown(resetViewKey)) return;
            focusPoint = homeFocusPoint;
            distance = homeDistance;
            UpdateTargetPose();
        }

        private void HandlePan()
        {
            if (!enablePan || navigationLocked) return;
            HandleKeyboardPan();
            HandleMousePan();
        }

        private void HandleKeyboardPan()
        {
            float horizontal = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float vertical = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            if (Mathf.Approximately(horizontal, 0f) && Mathf.Approximately(vertical, 0f)) return;

            Vector3 forward = Vector3.ProjectOnPlane(_targetRot * Vector3.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(_targetRot * Vector3.right, Vector3.up).normalized;
            Vector3 movement = right * horizontal + forward * vertical;
            if (movement.sqrMagnitude > 1f) movement.Normalize();
            focusPoint += movement * (keyboardPanSpeed * GlobalGameSettings.CameraPanSpeed * Time.unscaledDeltaTime);
            ClampFocusPoint();
            UpdateTargetPose();
        }

        private void HandleMousePan()
        {
            if (Input.GetMouseButtonDown(mousePanButton))
            {
                mousePanStart = Input.mousePosition;
                isMousePanning = true;
            }

            if (isMousePanning && Input.GetMouseButton(mousePanButton))
            {
                Vector3 pointerPosition = Input.mousePosition;
                Vector3 pointerDelta = pointerPosition - mousePanStart;
                mousePanStart = pointerPosition;
                Vector3 forward = Vector3.ProjectOnPlane(_targetRot * Vector3.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(_targetRot * Vector3.right, Vector3.up).normalized;
                float worldUnitsPerPixel = 2f * distance * Mathf.Tan(controlledCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, controlledCamera.pixelHeight);
                focusPoint -= (right * pointerDelta.x + forward * pointerDelta.y) * (worldUnitsPerPixel * GlobalGameSettings.CameraPanSpeed);
                ClampFocusPoint();
                UpdateTargetPose();
            }

            if (Input.GetMouseButtonUp(mousePanButton))
                isMousePanning = false;
        }

        private void ClampFocusPoint()
        {
            if (!hasNavigationBounds) return;
            focusPoint.x = Mathf.Clamp(focusPoint.x, navigationBounds.min.x - panBoundsPadding, navigationBounds.max.x + panBoundsPadding);
            focusPoint.z = Mathf.Clamp(focusPoint.z, navigationBounds.min.z - panBoundsPadding, navigationBounds.max.z + panBoundsPadding);
        }

        private void UpdateTargetPose()
        {
            _targetPos = focusPoint - _viewForward * distance;
        }

        private bool CanReceiveInput()
        {
            return Application.isFocused && controlledCamera.pixelRect.Contains(Input.mousePosition) && !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) && !CardInspectionOverlay.BlocksWorldInput && !navigationLocked && !PlayableHuntInputGuard.IsBlocked;
        }
    }
}
