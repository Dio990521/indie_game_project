using UnityEngine;
using UnityEngine.InputSystem; 
using IndieGame.Core; 
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

        // 内部开关，用来替代 enabled
        private bool _canMove = true;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>();
            if (Camera.main != null) _cameraTransform = Camera.main.transform;

            // 【修复重点】在 Awake 中订阅，保证脚本 Disable 时依然能收到消息
            GameManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDestroy()
        {
            // 【修复重点】在对象彻底销毁时才取消订阅
            GameManager.OnStateChanged -= HandleStateChanged;
        }

        private void Start()
        {
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.SetFollowTarget(this.transform);
            }
            
            // 初始化时检查一次状态
            if(GameManager.Instance != null)
            {
                HandleStateChanged(GameManager.Instance.CurrentState);
            }
        }

        private void HandleStateChanged(GameState newState)
        {
            if (newState == GameState.FreeRoam)
            {
                // 进入自由模式
                _canMove = true;
                _controller.enabled = true; // 物理碰撞开启
            }
            else
            {
                // 进入棋盘模式
                _canMove = false;
                _controller.enabled = false; // 必须禁用CC，否则会卡住Board移动
                
                // 清理状态
                _inputVector = Vector2.zero;
                if (_animator) _animator.SetFloat(speedParamName, 0);
            }
        }

        // Input System 的消息依然会被接收，但我们会根据 _canMove 决定是否处理
        public void OnMove(InputValue value)
        {
            // 即使在 BoardMode，我们也可以接收输入，
            // 这样切回 FreeRoam 的瞬间如果你按着 W，角色会直接动（手感更好）
            _inputVector = value.Get<Vector2>();
        }

        private void Update()
        {
            // 【修复重点】不再禁用脚本，而是通过 flag 拦截逻辑
            if (!_canMove) return;

            MovePlayer();
            UpdateAnimation(); 
        }

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