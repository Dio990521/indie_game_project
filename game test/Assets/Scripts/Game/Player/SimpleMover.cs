using UnityEngine;
using IndieGame.Core; 
using IndieGame.Core.CameraSystem; 
using IndieGame.Core.Input; // 引用 InputReader

namespace IndieGame.Gameplay.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class SimpleMover : MonoBehaviour
    {
        [Header("Architecture Dependencies")]
        public GameInputReader inputReader;

        [Header("Configuration")]
        public float moveSpeed = 5f;
        public float rotateSpeed = 15f; 
        public string speedParamName = "Speed"; 
        
        private CharacterController _controller;
        private Animator _animator;
        private Transform _cameraTransform;
        private Vector2 _currentInputVector;
        private int _animIDSpeed;

        private bool _canMove = true;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>();
            if (Camera.main != null) _cameraTransform = Camera.main.transform;
            
            // 缓存 Animator ID
            _animIDSpeed = Animator.StringToHash(speedParamName);

            GameManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDestroy()
        {
            GameManager.OnStateChanged -= HandleStateChanged;
        }

        private void OnEnable()
        {
            // 订阅输入事件
            if (inputReader != null)
            {
                inputReader.MoveEvent += OnMoveInput;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.MoveEvent -= OnMoveInput;
            }
        }

        private void Start()
        {
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.SetFollowTarget(this.transform);
            }
            
            // 初始化状态检查
            if(GameManager.Instance != null)
            {
                HandleStateChanged(GameManager.Instance.CurrentState);
            }
        }

        private void HandleStateChanged(GameState newState)
        {
            if (newState == GameState.FreeRoam)
            {
                _canMove = true;
                _controller.enabled = true;
            }
            else
            {
                _canMove = false;
                _controller.enabled = false;
                
                // 重置输入和动画
                _currentInputVector = Vector2.zero;
                if (_animator) _animator.SetFloat(_animIDSpeed, 0);
            }
        }

        // 事件回调
        private void OnMoveInput(Vector2 input)
        {
            // 即使在 BoardMode 下我们也会收到这个事件，
            // 但 Update 里的 _canMove 锁会阻止移动。
            // 这样设计保留了输入的连贯性。
            _currentInputVector = input;
        }

        private void Update()
        {
            if (!_canMove) return;

            MovePlayer();
            UpdateAnimation(); 
        }

        private void MovePlayer()
        {
            if (_currentInputVector.magnitude < 0.1f) return;

            Vector3 forward = _cameraTransform.forward;
            Vector3 right = _cameraTransform.right;

            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDir = (forward * _currentInputVector.y + right * _currentInputVector.x).normalized;

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
            float currentSpeed = _currentInputVector.magnitude;
            _animator.SetFloat(speedParamName, currentSpeed); 
        }
    }
}