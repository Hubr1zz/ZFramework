using Cards3D;
using HuntingInDarkness.ViewLayer.Tabletop;
using UI;
using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    /// <summary>
    /// 狩猎地图相机控制器（MonoBehaviour）。
    /// 支持：鼠标中键/右键拖拽平移、滚轮缩放。
    /// 挂载在场景相机上，或在 HuntRoot 激活时动态添加。
    /// </summary>
    public class HuntCameraController : MonoBehaviour
    {
        [Header("平移")]
        [SerializeField] private float panSpeed  = 0.025f;
        [SerializeField] private float panBorder = 80f;

        [Header("缩放")]
        [SerializeField] private float zoomSpeed = 2.0f;
        [SerializeField] private float minY      = 3f;
        [SerializeField] private float maxY      = 20f;

        [Header("边界")]
        [SerializeField] private float mapExtent = 8f;

        [Header("初始位置")]
        [SerializeField] private Vector3 initialPosition = new Vector3(0f, 16f, -11f);
        [SerializeField] private float   initialPitch    = 55f;
        [SerializeField, Range(25f, 65f)] private float perspectiveFieldOfView = 42f;
        [SerializeField, Min(1f)] private float framingPadding = 1.15f;
        [SerializeField, Min(1f)] private float minimumFramingDistance = 10f;
        [SerializeField] private string controlHint = "中键拖动 / WASD 移动 · 滚轮缩放 · 悬停卡牌按 F 查看详情 · H 显示提示";

        private Vector3  _dragStartPos;
        private bool     _isDragging;
        private Camera   _cam;
        private TabletopControlHintOverlay controlHintOverlay;
        private static readonly Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        private Bounds navigationBounds;
        private bool hasNavigationBounds;
        private bool navigationLocked;

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
            controlHintOverlay = _cam.GetComponent<TabletopControlHintOverlay>() ?? _cam.gameObject.AddComponent<TabletopControlHintOverlay>();
            controlHintOverlay.Show(this, controlHint);
            TabletopCameraFraming.Changed += RefreshFraming;
            RefreshFraming();
            _isDragging = false;
        }

        private void OnDisable()
        {
            TabletopCameraFraming.Changed -= RefreshFraming;
            controlHintOverlay?.Hide(this);
        }

        public void RefreshFraming()
        {
            _cam ??= GetComponent<Camera>() ?? Camera.main;
            if (_cam == null) return;
            if (TabletopCameraFraming.TryGetActiveFrame(out Bounds bounds, out int priority))
            {
                TabletopCameraFraming.CalculatePerspectivePose(_cam, bounds, initialPitch, 0f, framingPadding, minimumFramingDistance, out Vector3 position, out Quaternion rotation);
                _cam.transform.SetPositionAndRotation(position, rotation);
                navigationBounds = bounds;
                hasNavigationBounds = true;
                navigationLocked = priority > 0;
                return;
            }

            hasNavigationBounds = false;
            navigationLocked = false;
            _cam.transform.SetPositionAndRotation(initialPosition, Quaternion.Euler(initialPitch, 0f, 0f));
        }

        private void Update()
        {
            if (_cam == null || CardInspectionOverlay.BlocksWorldInput || navigationLocked) return;
            HandlePan();
            HandleZoom();
            ClampPosition();
        }

        private void HandlePan()
        {
            bool dragPressed = Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
            bool dragHeld = Input.GetMouseButton(1) || Input.GetMouseButton(2);
            bool dragReleased = Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2);
            if (dragPressed)
            {
                _dragStartPos = Input.mousePosition;
                _isDragging = true;
            }
            if (_isDragging && dragHeld)
            {
                Vector3 pointerPosition = Input.mousePosition;
                Vector3 pointerDelta = pointerPosition - _dragStartPos;
                _dragStartPos = pointerPosition;
                Vector3 forward = Vector3.ProjectOnPlane(_cam.transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(_cam.transform.right, Vector3.up).normalized;
                float worldUnitsPerPixel = 2f * _cam.transform.position.y * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);
                _cam.transform.position -= (right * pointerDelta.x + forward * pointerDelta.y) * (worldUnitsPerPixel * GlobalGameSettings.CameraPanSpeed);
            }
            if (dragReleased)
                _isDragging = false;

            // WASD / 方向键平移
            var move = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    move.z += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  move.z -= 1;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  move.x -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1;
            if (move != Vector3.zero)
                _cam.transform.position += move * (panSpeed * GlobalGameSettings.CameraPanSpeed * _cam.transform.position.y * Time.deltaTime * 60f);

            // 鼠标贴边平移
            var mp = Input.mousePosition;
            float edgePan = panSpeed * GlobalGameSettings.CameraPanSpeed * 60f * Time.deltaTime;
            if (mp.x < panBorder) _cam.transform.position += Vector3.left * edgePan;
            if (mp.x > Screen.width - panBorder) _cam.transform.position += Vector3.right * edgePan;
            if (mp.y < panBorder) _cam.transform.position += Vector3.back * edgePan;
            if (mp.y > Screen.height - panBorder) _cam.transform.position += Vector3.forward * edgePan;
        }

        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) < 0.001f) return;
            Vector3 candidate = _cam.transform.position + _cam.transform.forward * (scroll * zoomSpeed * GlobalGameSettings.CameraZoomSpeed * 10f);
            if (candidate.y >= minY && candidate.y <= maxY)
                _cam.transform.position = candidate;
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
            var ray = new Ray(_cam.transform.position, _cam.transform.forward);
            if (groundPlane.Raycast(ray, out float distance))
            {
                focusPoint = ray.GetPoint(distance);
                return true;
            }

            focusPoint = default;
            return false;
        }
    }
}
