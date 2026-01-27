using UnityEngine;
using Unity.Cinemachine;
using IndieGame.Core.Utilities;

namespace IndieGame.Core.CameraSystem
{
    public class CameraManager : MonoSingleton<CameraManager>
    {
        [Header("Settings")]
        [SerializeField] private CinemachineCamera _mainGameplayCamera; 

        private Transform _currentTarget;
        private bool _isInitialized;

        public void Init()
        {
            if (_isInitialized) return;
            if (_mainGameplayCamera == null)
            {
                _mainGameplayCamera = GetComponentInChildren<CinemachineCamera>();
            }
            _isInitialized = true;
        }

        public void SetFollowTarget(Transform target)
        {
            if (_mainGameplayCamera == null)
            {
                _mainGameplayCamera = GetComponentInChildren<CinemachineCamera>();
                if (_mainGameplayCamera == null)
                {
                    _mainGameplayCamera = FindAnyObjectByType<CinemachineCamera>();
                }
                
                if (_mainGameplayCamera == null)
                {
                    Debug.LogWarning("CameraManager: Main Cinemachine Camera is missing.");
                    return;
                }
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
