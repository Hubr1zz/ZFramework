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
        [SerializeField] private Vector3 initialPosition = new Vector3(0f, 10f, -6f);
        [SerializeField] private float   initialPitch    = 55f;

        private Vector3  _dragStartPos;
        private bool     _isDragging;
        private Camera   _cam;

        private void Awake()
        {
            _cam = GetComponent<Camera>() ?? Camera.main;
        }

        private void OnEnable()
        {
            // 每次进入狩猎阶段时归位（支持阶段切换后重新进入）
            if (_cam == null) return;
            _cam.transform.rotation = Quaternion.Euler(initialPitch, 0f, 0f);
            _cam.transform.position = initialPosition;
            _isDragging = false;
        }

        private void Update()
        {
            if (_cam == null) return;
            HandlePan();
            HandleZoom();
            ClampPosition();
        }

        private void HandlePan()
        {
            // 鼠标中键拖拽
            if (Input.GetMouseButtonDown(2))
            {
                _dragStartPos = _cam.ScreenToWorldPoint(
                    new Vector3(Input.mousePosition.x, Input.mousePosition.y, _cam.transform.position.y));
                _isDragging = true;
            }
            if (_isDragging && Input.GetMouseButton(2))
            {
                var current = _cam.ScreenToWorldPoint(
                    new Vector3(Input.mousePosition.x, Input.mousePosition.y, _cam.transform.position.y));
                var delta = _dragStartPos - current;
                delta.y = 0;
                _cam.transform.position += delta;
            }
            if (Input.GetMouseButtonUp(2))
                _isDragging = false;

            // WASD / 方向键平移
            var move = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    move.z += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  move.z -= 1;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  move.x -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1;
            if (move != Vector3.zero)
                _cam.transform.position += move * (panSpeed * _cam.transform.position.y * Time.deltaTime * 60f);

            // 鼠标贴边平移
            var mp = Input.mousePosition;
            if (mp.x < panBorder)  _cam.transform.position += Vector3.left  * (panSpeed * 60f * Time.deltaTime);
            if (mp.x > Screen.width  - panBorder) _cam.transform.position += Vector3.right * (panSpeed * 60f * Time.deltaTime);
            if (mp.y < panBorder)  _cam.transform.position += Vector3.back  * (panSpeed * 60f * Time.deltaTime);
            if (mp.y > Screen.height - panBorder) _cam.transform.position += Vector3.forward* (panSpeed * 60f * Time.deltaTime);
        }

        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) < 0.001f) return;
            var pos = _cam.transform.position;
            pos.y -= scroll * zoomSpeed * 10f;
            pos.y  = Mathf.Clamp(pos.y, minY, maxY);
            _cam.transform.position = pos;
        }

        private void ClampPosition()
        {
            var pos = _cam.transform.position;
            pos.x = Mathf.Clamp(pos.x, -mapExtent, mapExtent);
            pos.z = Mathf.Clamp(pos.z, -mapExtent, mapExtent);
            _cam.transform.position = pos;
        }
    }
}
