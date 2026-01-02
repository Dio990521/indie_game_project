using UnityEngine;
using UnityEngine.InputSystem; 
using IndieGame.Core.CameraSystem; 

namespace IndieGame.Gameplay.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class SimpleMover : MonoBehaviour
    {
        [Header("Configuration")]
        public float moveSpeed = 5f;
        public float rotateSpeed = 10f;

        [Header("Animation Settings")]
        public string speedParamName = "Speed"; 
        
        private Vector2 _inputVector;
        private CharacterController _controller;
        private Transform _cameraTransform;
        
        private Animator _animator;

        private void Start()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>(); 

            if (Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }

            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.SetFollowTarget(this.transform);
            }
        }

        public void OnMove(InputValue value)
        {
            _inputVector = value.Get<Vector2>();
        }

        private void Update()
        {
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
            _animator.SetFloat(speedParamName, currentSpeed); 
        }
    }
}