using UnityEngine;
using Unity.Cinemachine;
using IndieGame.Core.Utilities;

namespace IndieGame.Core.CameraSystem
{
    /// <summary>
    /// 摄像机管理器（单例）：
    /// 负责统一管理游戏内的主 Cinemachine 摄像机，
    /// 提供初始化、跟随目标设置、以及瞬移同步等功能。
    /// </summary>
    public class CameraManager : MonoSingleton<CameraManager>
    {
        [Header("Settings")]
        // 主游戏摄像机引用（CinemachineCamera 组件）
        [SerializeField] private CinemachineCamera _mainGameplayCamera; 

        // 当前跟随目标（通常为玩家）
        private Transform _currentTarget;
        // 初始化标记，避免重复查找与重复设置
        private bool _isInitialized;

        /// <summary>
        /// 初始化摄像机管理器：
        /// - 尝试在子物体中查找 CinemachineCamera
        /// - 只执行一次
        /// </summary>
        public void Init()
        {
            if (_isInitialized) return;
            if (_mainGameplayCamera == null)
            {
                // 自动寻找子物体中的 CinemachineCamera
                _mainGameplayCamera = GetComponentInChildren<CinemachineCamera>();
            }
            _isInitialized = true;
        }

        /// <summary>
        /// 设置摄像机跟随目标。
        /// </summary>
        /// <param name="target">要跟随的 Transform（通常为玩家）</param>
        public void SetFollowTarget(Transform target)
        {
            if (_mainGameplayCamera == null)
            {
                // 尝试从当前对象子节点获取
                _mainGameplayCamera = GetComponentInChildren<CinemachineCamera>();
                if (_mainGameplayCamera == null)
                {
                    // 兜底：从场景中查找任意一个 CinemachineCamera
                    _mainGameplayCamera = FindAnyObjectByType<CinemachineCamera>();
                }
                
                if (_mainGameplayCamera == null)
                {
                    // 摄像机缺失时给出警告并退出
                    Debug.LogWarning("CameraManager: Main Cinemachine Camera is missing.");
                    return;
                }
            }

            // 缓存目标并设置 Cinemachine Follow
            _currentTarget = target;
            _mainGameplayCamera.Follow = _currentTarget;
            Debug.Log($"[CameraManager] Camera is now following: {target.name}");
        }

        /// <summary>
        /// 将摄像机瞬移到当前目标位置：
        /// 用于场景切换/传送时防止镜头缓慢过渡。
        /// </summary>
        public void WarpCameraToTarget()
        {
             if (_currentTarget != null && _mainGameplayCamera != null)
             {
                 // 通知 Cinemachine 目标发生“瞬移”，让相机立即对齐
                 _mainGameplayCamera.OnTargetObjectWarped(_currentTarget, _currentTarget.position - _mainGameplayCamera.transform.position);
             }
        }
    }
}
