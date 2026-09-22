using Cards3D;
using HuntingInDarkness.ViewLayer.Tabletop;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HuntingInDarkness.Hunt
{
    /// <summary>
    /// 狩猎地图相机控制器（MonoBehaviour）。
    /// 支持：鼠标中键拖拽平移、滚轮缩放。
    /// 挂载在场景相机上，或在 HuntRoot 激活时动态添加。
    /// </summary>
    public class HuntCameraController : MonoBehaviour
    {
        [Header("平移")]
        [SerializeField] private float panSpeed = 0.025f;
        [SerializeField] private float panBorder = 80f;
        [SerializeField] private bool enableEdgePan;

        [Header("缩放")]
        [SerializeField] private float zoomSpeed = 2.0f;
        [SerializeField] private float minY = 3f;
        [SerializeField] private float maxY = 20f;

        [Header("边界")]
        [SerializeField] private float mapExtent = 8f;

        [Header("初始位置")]
        [SerializeField] private Vector3 initialPosition = new Vector3(0f, 16f, -11f);
        [SerializeField] private float initialPitch = 55f;
        [SerializeField, Range(25f, 65f)] private float perspectiveFieldOfView = 42f;
        [SerializeField, Min(1f)] private float framingPadding = 1.15f;
        [SerializeField, Min(1f)] private float minimumFramingDistance = 10f;
        [SerializeField] private KeyCode resetViewKey = KeyCode.Home;
        [SerializeField] private string controlHint = "中键拖动 / WASD 移动 · 滚轮缩放 · Home 复位 · 悬停卡牌按 F 查看详情 · H 显示提示";

        private Vector3 _dragStartPos;
        private bool _isDragging;
        private Camera _cam;
        [SerializeField] private TabletopControlHintOverlay controlHintOverlay;
        private bool missingControlHintLogged;
        private static readonly Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        private Bounds navigationBounds;
        private bool hasNavigationBounds;
        private bool navigationLocked;
        private Vector3 homePosition;
        private Quaternion homeRotation;

        public bool NavigationLocked => navigationLocked;

        private void Awake()
        {
            _cam = GetComponent<Camera>() ?? Camera.main;
        }

        private void OnEnable()
        {
            if (_cam == null) return;
            _cam.orthographic = false;
            _cam.fieldOfView = perspectiveFieldOfView;
            if (controlHintOverlay == null && !missingControlHintLogged)
            {
                Debug.LogWarning($"[{nameof(HuntCameraController)}] 未绑定 {nameof(TabletopControlHintOverlay)}，跳过操作提示。", this);
                missingControlHintLogged = true;
            }
            controlHintOverlay?.Show(this, controlHint);
            TabletopCameraFraming.Changed += RefreshFraming;
            RefreshFraming();
            _isDragging = false;
        }

        private void OnDisable()
        {
            TabletopCameraFraming.Changed -= RefreshFraming;
            controlHintOverlay?.Hide(this);
            _isDragging = false;
        }

        public void RefreshFraming()
        {
            _cam ??= GetComponent<Camera>() ?? Camera.main;
            if (_cam == null) return;
            if (TabletopCameraFraming.TryGetActiveFrame(out Bounds bounds, out int priority))
            {
                TabletopCameraFraming.CalculatePerspectivePose(_cam, bounds, initialPitch, 0f, framingPadding, minimumFramingDistance, out Vector3 position, out Quaternion rotation);
                _cam.transform.SetPositionAndRotation(position, rotation);
                homePosition = position;
                homeRotation = rotation;
                navigationBounds = bounds;
                hasNavigationBounds = true;
                navigationLocked = priority > 0;
                return;
            }

            hasNavigationBounds = false;
            navigationLocked = false;
            homePosition = initialPosition;
            homeRotation = Quaternion.Euler(initialPitch, 0f, 0f);
            _cam.transform.SetPositionAndRotation(homePosition, homeRotation);
        }

        private void Update()
        {
            if (_cam == null) return;
            if (!CanReceiveInput())
            {
                _isDragging = false;
                return;
            }
            if (Input.GetKeyDown(resetViewKey))
            {
                _cam.transform.SetPositionAndRotation(homePosition, homeRotation);
                return;
            }
            HandlePan();
            HandleZoom();
            ClampPosition();
        }

        private void HandlePan()
        {
            if (Input.GetMouseButtonDown(2))
            {
                _dragStartPos = Input.mousePosition;
                _isDragging = true;
            }
            if (_isDragging && Input.GetMouseButton(2))
            {
                Vector3 pointerPosition = Input.mousePosition;
                Vector3 pointerDelta = pointerPosition - _dragStartPos;
                _dragStartPos = pointerPosition;
                Vector3 forward = Vector3.ProjectOnPlane(_cam.transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(_cam.transform.right, Vector3.up).normalized;
                float cameraToGroundDistance = TryGetViewFocusPoint(out Vector3 focusPoint) ? Vector3.Distance(_cam.transform.position, focusPoint) : Mathf.Abs(_cam.transform.position.y);
                float worldUnitsPerPixel = 2f * Mathf.Max(0.01f, cameraToGroundDistance) * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, _cam.pixelHeight);
                _cam.transform.position -= (right * pointerDelta.x + forward * pointerDelta.y) * (worldUnitsPerPixel * GlobalGameSettings.CameraPanSpeed);
            }
            if (Input.GetMouseButtonUp(2))
                _isDragging = false;

            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.z += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.z -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1f;
            if (move.sqrMagnitude > 0f)
            {
                Vector3 forward = Vector3.ProjectOnPlane(_cam.transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(_cam.transform.right, Vector3.up).normalized;
                Vector3 movement = right * move.x + forward * move.z;
                if (movement.sqrMagnitude > 1f) movement.Normalize();
                _cam.transform.position += movement * (panSpeed * GlobalGameSettings.CameraPanSpeed * _cam.transform.position.y * Time.unscaledDeltaTime * 60f);
            }

            if (!enableEdgePan || _isDragging) return;
            Rect pixelRect = _cam.pixelRect;
            Vector3 edgePointerPosition = Input.mousePosition;
            Vector3 edgeMovement = Vector3.zero;
            if (edgePointerPosition.x < pixelRect.xMin + panBorder) edgeMovement += Vector3.left;
            if (edgePointerPosition.x > pixelRect.xMax - panBorder) edgeMovement += Vector3.right;
            if (edgePointerPosition.y < pixelRect.yMin + panBorder) edgeMovement += Vector3.back;
            if (edgePointerPosition.y > pixelRect.yMax - panBorder) edgeMovement += Vector3.forward;
            if (edgeMovement.sqrMagnitude <= 0f) return;
            edgeMovement.Normalize();
            _cam.transform.position += edgeMovement * (panSpeed * GlobalGameSettings.CameraPanSpeed * 60f * Time.unscaledDeltaTime);
        }

        private void HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.001f) return;
            float forwardY = _cam.transform.forward.y;
            if (Mathf.Abs(forwardY) < 0.0001f) return;
            float requestedDistance = scroll * zoomSpeed * GlobalGameSettings.CameraZoomSpeed;
            float currentY = _cam.transform.position.y;
            float farthestY = Mathf.Max(minY, maxY, homePosition.y);
            float candidateY = Mathf.Clamp(currentY + forwardY * requestedDistance, minY, farthestY);
            float allowedDistance = (candidateY - currentY) / forwardY;
            _cam.transform.position += _cam.transform.forward * allowedDistance;
        }

        private void ClampPosition()
        {
            if (!TryGetViewFocusPoint(out Vector3 focusPoint)) return;
            float minimumX = hasNavigationBounds ? navigationBounds.min.x : -mapExtent;
            float maximumX = hasNavigationBounds ? navigationBounds.max.x : mapExtent;
            float minimumZ = hasNavigationBounds ? navigationBounds.min.z : -mapExtent;
            float maximumZ = hasNavigationBounds ? navigationBounds.max.z : mapExtent;
            Vector3 clampedFocus = new Vector3(Mathf.Clamp(focusPoint.x, minimumX, maximumX), focusPoint.y, Mathf.Clamp(focusPoint.z, minimumZ, maximumZ));
            _cam.transform.position += clampedFocus - focusPoint;
        }

        private bool TryGetViewFocusPoint(out Vector3 focusPoint)
        {
            Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
            if (groundPlane.Raycast(ray, out float distance) && distance > 0f)
            {
                focusPoint = ray.GetPoint(distance);
                return true;
            }

            focusPoint = default;
            return false;
        }

        private bool CanReceiveInput()
        {
            return Application.isFocused && _cam.pixelRect.Contains(Input.mousePosition) && !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) && !CardInspectionOverlay.BlocksWorldInput && !navigationLocked && !PlayableHuntInputGuard.IsBlocked;
        }
    }
}
