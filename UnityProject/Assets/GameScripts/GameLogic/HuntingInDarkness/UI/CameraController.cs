using Core;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Boss 战相机的轨道、角色注视与面板近景控制。
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        private enum CameraMode
        {
            Orbit,
            CharacterFocus,
            DetailFocus
        }

        [Header("轨道目标（棋盘中心）")]
        [SerializeField] private Vector3 orbitTarget = Vector3.zero;

        [Header("旋转灵敏度")]
        [SerializeField] private float rotateSensitivity = 2.5f;
        [SerializeField] private float minPitch = 15f;
        [SerializeField] private float maxPitch = 80f;

        [Header("过渡速度")]
        [Tooltip("轨道跟随的平滑速度（非聚焦时）")]
        [SerializeField] private float transitionSpeed = 6f;
        [Tooltip("视角切换的过渡时长（秒），整段匀速")]
        [SerializeField] private float focusTransitionDuration = 0.5f;

        [Header("角色注视")]
        [Tooltip("角色距离 Boss 最远时使用的局部 offset。坐标轴：X=角色-Boss 连线右侧，Y=世界上方，Z=角色指向 Boss。")]
        [SerializeField] private Vector3 characterFocusFarOffset = new Vector3(4f, 6f, -7f);
        [Tooltip("角色距离 Boss 最近时使用的局部 offset。坐标轴同上。")]
        [SerializeField] private Vector3 characterFocusNearOffset = new Vector3(2.5f, 4f, -4f);
        [Tooltip("角色-Boss 距离达到该值时完全使用 Far Offset；<=0 时使用棋盘直径。")]
        [SerializeField] private float maxCharacterBossFocusDistance = 0f;

        [Header("滚轮聚焦距离（仅轨道状态）")]
        [SerializeField] private float zoomSpeed = 1f;
        [SerializeField] private float minFocusDistance = 3f;
        [SerializeField] private float maxFocusDistance = 20f;

        [Header("WASD 平移 + 动态死区")]
        [Tooltip("WASD 平移速度（世界单位/秒）")]
        [SerializeField] private float panSpeed = 6f;
        [Tooltip("死区半径相对棋盘世界半径的倍数（1 = 恰好到棋盘边缘）")]
        [SerializeField] private float panRangeFactor = 1.2f;
        [Tooltip("未收到棋盘大小事件前的默认世界半径")]
        [SerializeField] private float defaultBoardWorldRadius = 4f;

        private float _yaw;
        private float _pitch;
        private float _distance;

        private float _savedYaw;
        private float _savedPitch;
        private float _savedDistance;

        private CameraMode _mode;
        private Vector3 _targetPos;
        private Quaternion _targetRot;

        private bool _transitioning;
        private float _transT;
        private Vector3 _fromPos;
        private Quaternion _fromRot;

        private Vector3 _detailReturnPos;
        private Quaternion _detailReturnRot;
        private float _boardWorldRadius;

        private void OnEnable()
        {
            _distance = Vector3.Distance(transform.position, orbitTarget);

            var euler = transform.eulerAngles;
            _yaw = euler.y;
            _pitch = euler.x > 180f ? euler.x - 360f : euler.x;

            _targetPos = transform.position;
            _targetRot = transform.rotation;
            _mode = CameraMode.Orbit;
            _transitioning = false;

            if (_boardWorldRadius <= 0f) _boardWorldRadius = defaultBoardWorldRadius;

            EventBus.Subscribe<BoardFocusChangedEvent>(OnFocusChanged);
            EventBus.Subscribe<CharacterDetailFocusChangedEvent>(OnDetailFocusChanged);
            EventBus.Subscribe<BoardReadyEvent>(OnBoardReady);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BoardFocusChangedEvent>(OnFocusChanged);
            EventBus.Unsubscribe<CharacterDetailFocusChangedEvent>(OnDetailFocusChanged);
            EventBus.Unsubscribe<BoardReadyEvent>(OnBoardReady);
        }

        private void OnBoardReady(BoardReadyEvent evt)
        {
            _boardWorldRadius = evt.MapRadius * evt.CellSize;
        }

        private void Update()
        {
            if (_transitioning)
            {
                _transT += Time.deltaTime / Mathf.Max(0.0001f, focusTransitionDuration);
                float t = Mathf.Clamp01(_transT);
                transform.position = Vector3.Lerp(_fromPos, _targetPos, t);
                transform.rotation = Quaternion.Slerp(_fromRot, _targetRot, t);
                if (t >= 1f) _transitioning = false;
                return;
            }

            if (_mode != CameraMode.Orbit)
            {
                transform.position = _targetPos;
                transform.rotation = _targetRot;
                return;
            }

            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * rotateSensitivity;
                _pitch -= Input.GetAxis("Mouse Y") * rotateSensitivity;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            }

            HandleZoom();
            HandleWasdPan();

            var orbitRot = Quaternion.Euler(_pitch, _yaw, 0f);
            _targetPos = orbitTarget + orbitRot * (Vector3.back * _distance);
            _targetRot = orbitRot;

            transform.position = Vector3.Lerp(
                transform.position, _targetPos, Time.deltaTime * transitionSpeed);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, _targetRot, Time.deltaTime * transitionSpeed);
        }

        private void HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Approximately(scroll, 0f)) return;

            float minDistance = Mathf.Max(0.1f, minFocusDistance);
            float maxDistance = Mathf.Max(minDistance, maxFocusDistance);
            _distance = Mathf.Clamp(_distance - scroll * zoomSpeed, minDistance, maxDistance);
        }

        private void HandleWasdPan()
        {
            float h = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            float v = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
            if (h == 0f && v == 0f) return;

            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            Vector3 right = transform.right;
            right.y = 0f;
            fwd.Normalize();
            right.Normalize();

            Vector3 move = fwd * v + right * h;
            if (move.sqrMagnitude > 1f) move.Normalize();
            orbitTarget += move * (panSpeed * Time.deltaTime);

            float maxR = Mathf.Max(0.01f, _boardWorldRadius * panRangeFactor);
            var planar = new Vector2(orbitTarget.x, orbitTarget.z);
            if (planar.magnitude <= maxR) return;

            planar = planar.normalized * maxR;
            orbitTarget.x = planar.x;
            orbitTarget.z = planar.y;
        }

        private void OnFocusChanged(BoardFocusChangedEvent evt)
        {
            if (evt.HasFocus)
            {
                if (_mode == CameraMode.Orbit)
                {
                    _savedYaw = _yaw;
                    _savedPitch = _pitch;
                    _savedDistance = _distance;
                }

                CalculateCharacterFocusPose(
                    evt.CharacterWorldPosition,
                    evt.BossWorldPosition,
                    out Vector3 position,
                    out Quaternion rotation);

                StartTransition(position, rotation);
                _mode = CameraMode.CharacterFocus;
                return;
            }

            _yaw = _savedYaw;
            _pitch = _savedPitch;
            _distance = _savedDistance;

            var orbitRot = Quaternion.Euler(_pitch, _yaw, 0f);
            StartTransition(
                orbitTarget + orbitRot * (Vector3.back * _distance), orbitRot);
            _mode = CameraMode.Orbit;
        }

        private void CalculateCharacterFocusPose(
            Vector3 characterPosition,
            Vector3 bossPosition,
            out Vector3 position,
            out Quaternion rotation)
        {
            Vector3 midpoint = Vector3.Lerp(characterPosition, bossPosition, 0.5f);

            Vector3 forward = bossPosition - characterPosition;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = transform.forward;
                forward.y = 0f;
            }

            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float distanceToBoss = Vector3.Distance(characterPosition, bossPosition);
            float maxReferenceDistance = maxCharacterBossFocusDistance > 0f
                ? maxCharacterBossFocusDistance
                : Mathf.Max(0.01f, _boardWorldRadius * 2f);
            float nearT = 1f - Mathf.Clamp01(distanceToBoss / maxReferenceDistance);
            Vector3 localOffset = Vector3.Lerp(
                characterFocusFarOffset,
                characterFocusNearOffset,
                nearT);

            Vector3 candidateA = CharacterLocalToWorldOffset(
                characterPosition, right, forward, localOffset);
            localOffset.x = -localOffset.x;
            Vector3 candidateB = CharacterLocalToWorldOffset(
                characterPosition, right, forward, localOffset);

            position = Vector3.SqrMagnitude(candidateA - transform.position)
                <= Vector3.SqrMagnitude(candidateB - transform.position)
                ? candidateA
                : candidateB;

            Vector3 lookDirection = midpoint - position;
            rotation = lookDirection.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(lookDirection, Vector3.up)
                : transform.rotation;
        }

        private static Vector3 CharacterLocalToWorldOffset(
            Vector3 origin,
            Vector3 right,
            Vector3 forward,
            Vector3 localOffset)
        {
            return origin
                + right * localOffset.x
                + Vector3.up * localOffset.y
                + forward * localOffset.z;
        }

        private void OnDetailFocusChanged(CharacterDetailFocusChangedEvent evt)
        {
            if (evt.HasFocus)
            {
                if (_mode != CameraMode.DetailFocus)
                {
                    _detailReturnPos = _targetPos;
                    _detailReturnRot = _targetRot;
                }

                StartTransition(evt.CameraWorldPosition, evt.CameraWorldRotation);
                _mode = CameraMode.DetailFocus;
                return;
            }

            if (_mode != CameraMode.DetailFocus) return;
            StartTransition(_detailReturnPos, _detailReturnRot);
            _mode = CameraMode.CharacterFocus;
        }

        private void StartTransition(Vector3 position, Quaternion rotation)
        {
            _fromPos = transform.position;
            _fromRot = transform.rotation;
            _targetPos = position;
            _targetRot = rotation;
            _transT = 0f;
            _transitioning = true;
        }
    }
}
