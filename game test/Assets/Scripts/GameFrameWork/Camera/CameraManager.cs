using UnityEngine;
using Unity.Cinemachine; 

namespace IndieGame.Core.CameraSystem
{
    public class CameraManager : MonoBehaviour
    {
        public static CameraManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private CinemachineCamera _mainGameplayCamera; 

        private Transform _currentTarget;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void SetFollowTarget(Transform target)
        {
            if (_mainGameplayCamera == null)
            {
                Debug.LogError("CameraManager: Main Cinemachine Camera is missing!");
                return;
            }

            _currentTarget = target;
            _mainGameplayCamera.Follow = _currentTarget;
            Debug.Log($"[CameraManager] Camera is now following: {target.name}");
        }

        public void WarpCameraToTarget()
        {
             if (_currentTarget != null && _mainGameplayCamera != null)
             {
                 _mainGameplayCamera.OnTargetObjectWarped(_currentTarget, _currentTarget.position - _mainGameplayCamera.transform.position);
             }
        }
    }
}