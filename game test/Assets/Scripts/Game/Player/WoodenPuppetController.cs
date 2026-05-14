using UnityEngine;
using IndieGame.Core;

namespace IndieGame.Gameplay.Player
{
    /// <summary>
    /// 木头人移动控制器：
    /// 由 WoodenPuppetTreasureState 在运行时通过 AddComponent 附加到临时 GameObject 上。
    /// 订阅 InputMoveEvent，将 WASD 输入转换为相机坐标系方向并执行移动。
    /// Y 轴固定为召唤时格子高度；XZ 平面做圆形半径约束，防止木头人超出允许范围。
    /// </summary>
    public class WoodenPuppetController : MonoBehaviour
    {
        // ── 由 Init() 注入的运行参数 ──
        private Vector3 _origin;     // 召唤原点（玩家当前格子 world position），圆形约束中心
        private float _maxRadius;    // 可移动最大半径
        private float _moveSpeed;    // 移动速度
        private float _rotateSpeed;  // 旋转平滑速度

        // ── 内部状态 ──
        private Vector2 _moveInput;
        private Transform _cameraTransform;

        /// <summary>
        /// 在 AddComponent 之后立即调用，注入运行参数并锁定初始 Y 轴。
        /// </summary>
        public void Init(Vector3 origin, float maxRadius, float moveSpeed, float rotateSpeed)
        {
            _origin      = origin;
            _maxRadius   = maxRadius;
            _moveSpeed   = moveSpeed;
            _rotateSpeed = rotateSpeed;

            // 将初始 Y 轴固定到格子高度，防止生成位置有偏差
            Vector3 pos = transform.position;
            pos.y = origin.y;
            transform.position = pos;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<InputMoveEvent>(OnMoveInput);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InputMoveEvent>(OnMoveInput);
        }

        private void Start()
        {
            if (Camera.main != null)
                _cameraTransform = Camera.main.transform;
        }

        private void OnMoveInput(InputMoveEvent evt)
        {
            _moveInput = evt.Value;
        }

        private void Update()
        {
            if (_moveInput.magnitude < 0.1f) return;

            // 延迟获取摄像机引用（摄像机切换后 Camera.main 始终指向 Cinemachine Brain）
            if (_cameraTransform == null && Camera.main != null)
                _cameraTransform = Camera.main.transform;
            if (_cameraTransform == null) return;

            // ── 相机坐标系转换（与 SimpleMover 一致）──
            Vector3 forward = _cameraTransform.forward;
            Vector3 right   = _cameraTransform.right;
            forward.y = 0f;
            right.y   = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDir = (forward * _moveInput.y + right * _moveInput.x).normalized;

            // ── 计算新位置，Y 轴每帧强制锁定防止浮点漂移 ──
            Vector3 newPos = transform.position + moveDir * _moveSpeed * Time.deltaTime;
            newPos.y = _origin.y;

            // ── XZ 半径约束：超出圆形边界时将 XZ 位置夹回边界点 ──
            Vector2 offsetXZ = new Vector2(newPos.x - _origin.x, newPos.z - _origin.z);
            if (offsetXZ.magnitude > _maxRadius)
            {
                offsetXZ = offsetXZ.normalized * _maxRadius;
                newPos.x = _origin.x + offsetXZ.x;
                newPos.z = _origin.z + offsetXZ.y;
            }

            transform.position = newPos;

            // ── 平滑转向 ──
            if (moveDir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRot, _rotateSpeed * Time.deltaTime);
            }
        }
    }
}
