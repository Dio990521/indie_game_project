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
        
        private Vector2 _inputVector;
        private CharacterController _controller;
        private Transform _cameraTransform;

        private void Start()
        {
            _controller = GetComponent<CharacterController>();
            _cameraTransform = Camera.main.transform;

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
                transform.forward = Vector3.Slerp(transform.forward, moveDir, 15f * Time.deltaTime);
            }
        }
    }
}