using System.Collections;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using Unity.Cinemachine;
using UnityEngine;

namespace IndieGame.Core.CameraSystem
{
    /// <summary>
    /// 操作菜单镜头控制器（Cinemachine 3.x）：
    /// 挂在"操作菜单专用 CinemachineCamera"物体上，
    /// 通过监听操作菜单显示/投骰子事件来切换镜头优先级，实现：
    /// - 菜单显示：切到带弧度拉近的特写镜头，并围绕玩家居中取景
    /// - 投掷骰子：切回主镜头（俯视跟随）
    ///
    /// 设计说明（与 DialogueCameraController 一致）：
    /// 默认使用 Priority 切换，而不是 SetActive 开关，
    /// 避免脚本被禁用导致下一次事件无法自动响应。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineCamera))]
    [SaveDuringPlay] // 配合 Cinemachine 的"运行时保存"开关，让 Play 模式下调试的字段值在停止运行后保留
    public class ActionMenuCameraController : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("操作菜单专用 CinemachineCamera；不填则自动取同物体组件")]
        [SerializeField] private CinemachineCamera actionMenuCamera;

        [Header("Priority Switch")]
        [Tooltip("菜单镜头激活时的 Priority（需高于主镜头）")]
        [SerializeField] private int activePriority = 50;
        [Tooltip("菜单镜头闲置时的 Priority（建议低于主镜头）")]
        [SerializeField] private int inactivePriority = 0;

        [Header("Screen Position Offset")]
        [Tooltip("是否在菜单镜头激活时应用取景偏移，让\"玩家+操作菜单\"整体在屏幕居中")]
        [SerializeField] private bool applyScreenPositionOffset = true;
        [Tooltip("归一化取景偏移（PositionComposer.Composition.ScreenPosition），需配合菜单的屏幕偏移方向手动调出居中效果")]
        [SerializeField] private Vector2 screenPositionOffset;

        // 缓存的 Brain 引用，用于检测拉远 Blend 是否已经结束
        private CinemachineBrain _brain;
        // 当前正在等待"拉远 Blend 结束"的协程
        private Coroutine _zoomOutWatcher;

        private void Awake()
        {
            if (actionMenuCamera == null)
            {
                actionMenuCamera = GetComponent<CinemachineCamera>();
            }

            // 启动时强制设为闲置优先级，避免场景加载时误抢主镜头。
            ApplyPriority(isActive: false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<BoardActionMenuShownEvent>(HandleMenuShown);
            EventBus.Subscribe<BoardRollDiceRequestedEvent>(HandleRollDiceRequested);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BoardActionMenuShownEvent>(HandleMenuShown);
            EventBus.Unsubscribe<BoardRollDiceRequestedEvent>(HandleRollDiceRequested);

            if (_zoomOutWatcher != null)
            {
                StopCoroutine(_zoomOutWatcher);
                _zoomOutWatcher = null;
            }
        }

        /// <summary>
        /// 操作菜单显示：切到菜单镜头（Priority 提升），并对准当前目标。
        /// </summary>
        private void HandleMenuShown(BoardActionMenuShownEvent evt)
        {
            if (actionMenuCamera == null) return;

            Transform target = evt.Target != null
                ? evt.Target
                : (GameManager.Instance != null && GameManager.Instance.CurrentPlayer != null
                    ? GameManager.Instance.CurrentPlayer.transform
                    : null);

            if (target != null)
            {
                actionMenuCamera.Follow = target;
                actionMenuCamera.LookAt = target;
            }

            if (applyScreenPositionOffset)
            {
                ApplyScreenPositionOffset();
            }

            ApplyPriority(isActive: true);
        }

        /// <summary>
        /// 投掷骰子：降低菜单镜头优先级，让主镜头重新生效；
        /// 并启动协程监听 Blend 是否真正结束，结束后广播事件通知 PlayerTurnState 再开始移动。
        /// </summary>
        private void HandleRollDiceRequested(BoardRollDiceRequestedEvent evt)
        {
            ApplyPriority(isActive: false);

            if (_zoomOutWatcher != null) StopCoroutine(_zoomOutWatcher);
            _zoomOutWatcher = StartCoroutine(WaitForZoomOutThenNotify());
        }

        /// <summary>
        /// 等待 CinemachineBrain 的 Blend 真正结束（而不是用固定延时去猜测时长），
        /// 这样即便之后在 Cinemachine 里改了 Blend Time/Style，这里也不需要同步改代码。
        /// </summary>
        private IEnumerator WaitForZoomOutThenNotify()
        {
            if (_brain == null && Camera.main != null)
            {
                _brain = Camera.main.GetComponent<CinemachineBrain>();
            }

            // 等一帧，确保 Brain 已经根据刚才的 Priority 变化开始/结束新的 Blend
            yield return null;

            if (_brain != null)
            {
                while (_brain.IsBlending) yield return null;
            }

            _zoomOutWatcher = null;
            EventBus.Raise(new BoardActionMenuCameraSettledEvent());
        }

        /// <summary>
        /// 将取景偏移写入 CinemachinePositionComposer，使"玩家+菜单"整体在屏幕居中。
        /// 若 Body 组件不是 PositionComposer，则跳过并提示一次。
        /// </summary>
        private void ApplyScreenPositionOffset()
        {
            CinemachinePositionComposer composer = actionMenuCamera.GetComponent<CinemachinePositionComposer>();
            if (composer == null)
            {
                DebugTools.LogWarning("[ActionMenuCameraController] 未找到 CinemachinePositionComposer，跳过取景偏移设置。");
                return;
            }

            composer.Composition.ScreenPosition = screenPositionOffset;
        }

        /// <summary>
        /// 统一设置菜单镜头 Priority。
        /// </summary>
        private void ApplyPriority(bool isActive)
        {
            if (actionMenuCamera == null) return;
            actionMenuCamera.Priority = isActive ? activePriority : inactivePriority;
        }
    }
}
