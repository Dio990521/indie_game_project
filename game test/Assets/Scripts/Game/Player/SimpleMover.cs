using UnityEngine;
using UnityEngine.InputSystem; 
using IndieGame.Core; // 引用 Core 命名空间
using IndieGame.Core.CameraSystem; 

namespace IndieGame.Gameplay.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class SimpleMover : MonoBehaviour
    {
        [Header("Configuration")]
        public float moveSpeed = 5f;
        public float rotateSpeed = 15f; 
        public string speedParamName = "Speed"; 
        
        private Vector2 _inputVector;
        private CharacterController _controller;
        private Animator _animator;
        private Transform _cameraTransform;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>();
            if (Camera.main != null) _cameraTransform = Camera.main.transform;
        }

        private void Start()
        {
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.SetFollowTarget(this.transform);
            }
        }

        // ==================== 状态管理核心代码 ====================
        private void OnEnable()
        {
            // 订阅事件
            GameManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            // 取消订阅 (防止内存泄漏)
            GameManager.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState newState)
        {
            if (newState == GameState.FreeRoam)
            {
                // 启用输入控制
                this.enabled = true;
                _controller.enabled = true; // 启用 CC，允许物理碰撞
            }
            else
            {
                // 禁用输入控制
                this.enabled = false;
                _controller.enabled = false; // 必须禁用 CC！否则它会阻止 transform.position 的直接修改
                
                // 重置所有输入值，防止切换瞬间角色还在跑
                _inputVector = Vector2.zero;
                if (_animator) _animator.SetFloat(speedParamName, 0);
            }
        }
        // ========================================================

        public void OnMove(InputValue value)
        {
            _inputVector = value.Get<Vector2>();
        }

        private void Update()
        {
            // 如果 GameManager 还没初始化或不在自由模式，双重保险
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.FreeRoam) return;

            MovePlayer();
            UpdateAnimation(); 
        }

        // ... MovePlayer 和 UpdateAnimation 方法保持不变 ...
        // (省略以节省篇幅，请保留原本逻辑)
        private void MovePlayer()
        {
            if (_inputVector.magnitude < 0.1f) return;

            Vector3 forward = _cameraTransform.forward;
            Vector3 right = _cameraTransform.right;

            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDir = (forward * _inputVector.y + right * _inputVector.x).normalized;

            _controller.Move(moveDir * moveSpeed * Time.deltaTime);

            if (moveDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
            }
        }

        private void UpdateAnimation()
        {
            if (_animator == null) return;
            float currentSpeed = _inputVector.magnitude;
            _animator.SetFloat(speedParamName, currentSpeed, 0.1f, Time.deltaTime); 
        }
    }
}