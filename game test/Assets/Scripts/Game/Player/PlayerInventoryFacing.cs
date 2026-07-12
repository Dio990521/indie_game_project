using DG.Tweening;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using UnityEngine;

namespace IndieGame.Gameplay.Player
{
    /// <summary>
    /// 玩家背包朝向控制：
    /// 背包打开期间，角色逐帧转向主相机镜头；背包关闭后，转身恢复到打开背包之前的朝向
    /// （例如棋盘操作菜单场景下由 <see cref="PlayerActionMenuFacing"/> 设置的朝向）。
    ///
    /// 设计说明：
    /// 1) 打开阶段为什么用逐帧追踪而不是像 PlayerActionMenuFacing 那样对着某一瞬间的
    ///    相机位置做一次性 DOTween？因为背包打开会触发 InventoryCameraController 平滑地
    ///    把镜头右移，相机本身的位置在过渡期间持续变化。若在打开瞬间就对着"旧"相机位置
    ///    做一次性转身，转身结束时角色会和镜头实际停靠的位置有偏差。逐帧追踪相机的实时
    ///    位置，能保证镜头停下来之后角色也稳稳正对着镜头最终的位置。
    /// 2) 追踪逻辑放在 LateUpdate 而不是 Update：项目里 SimpleMover / BoardEntity 等移动
    ///    相关脚本也会在 Update 阶段写 transform.rotation，若本脚本同样在 Update 里写，
    ///    执行顺序一旦排在它们前面就会被后写入的值覆盖，导致"打开背包时看起来完全没转身"。
    ///    Unity 保证同一帧内所有 Update 先执行完才会执行 LateUpdate，因此放在 LateUpdate
    ///    能确保本脚本对朝向有最终话语权（与 CinemachineBrain 用 LateUpdate 更新相机是同一道理）。
    /// 3) 关闭阶段目标是固定的（回到打开前缓存的朝向），因此复用 SmoothFacingTween 的
    ///    一次性缓动（现在通过 TryRotateToRotation 支持直接传入目标 Quaternion）。
    /// 4) 不像 PlayerActionMenuFacing 那样强绑定 BoardEntity（只在棋盘场景存在）——背包
    ///    可能在棋盘操作菜单、露营、城镇等多个场景打开，这里改为通过 GameManager.CurrentPlayer
    ///    动态解析当前玩家角色，挂在任意一个跨场景常驻物体上即可对任意场景生效。
    /// </summary>
    public class PlayerInventoryFacing : MonoBehaviour
    {
        [Header("Track While Open")]
        [Tooltip("背包打开期间，逐帧转向相机的角速度（度/秒）")]
        [SerializeField] private float trackRotateSpeedDegPerSec = 720f;
        [Tooltip("是否只绕 Y 轴旋转（角色推荐开启）")]
        [SerializeField] private bool yAxisOnly = true;

        [Header("Restore On Close")]
        [Tooltip("背包关闭后，恢复到打开前朝向的平滑参数")]
        [SerializeField] private SmoothFacingTweenOptions restoreOptions = SmoothFacingTweenOptions.Default;

        // 背包关闭时用来恢复的目标缓动
        private Tween _restoreTween;
        // 打开背包那一刻缓存的朝向（关闭后要恢复到这里）
        private Quaternion _rotationBeforeOpen;
        // _rotationBeforeOpen 是否已经被成功缓存过（避免打开时找不到玩家时，关闭仍误用默认值恢复）
        private bool _hasCachedRotation;
        // 是否正处于"背包打开、逐帧追踪相机"状态
        private bool _tracking;

        private void OnEnable()
        {
            EventBus.Subscribe<InventoryOpenedEvent>(HandleInventoryOpened);
            EventBus.Subscribe<InventoryClosedEvent>(HandleInventoryClosed);
            // 装备界面与背包共用同一套"转向镜头"效果。
            EventBus.Subscribe<EquipmentUIOpenedEvent>(HandleEquipmentUIOpened);
            EventBus.Subscribe<EquipmentUIClosedEvent>(HandleEquipmentUIClosed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InventoryOpenedEvent>(HandleInventoryOpened);
            EventBus.Unsubscribe<InventoryClosedEvent>(HandleInventoryClosed);
            EventBus.Unsubscribe<EquipmentUIOpenedEvent>(HandleEquipmentUIOpened);
            EventBus.Unsubscribe<EquipmentUIClosedEvent>(HandleEquipmentUIClosed);
            SmoothFacingTween.Kill(ref _restoreTween);
            _tracking = false;
        }

        /// <summary>
        /// 背包打开：缓存当前朝向（供关闭时恢复），并开始逐帧追踪相机。
        /// </summary>
        private void HandleInventoryOpened(InventoryOpenedEvent evt) => BeginTracking();

        /// <summary>
        /// 背包关闭：停止追踪相机，转身恢复到打开前缓存的朝向。
        /// </summary>
        private void HandleInventoryClosed(InventoryClosedEvent evt) => EndTracking();

        // 装备界面共用同一份逻辑。
        private void HandleEquipmentUIOpened(EquipmentUIOpenedEvent evt) => BeginTracking();
        private void HandleEquipmentUIClosed(EquipmentUIClosedEvent evt) => EndTracking();

        private void BeginTracking()
        {
            Transform player = ResolvePlayerTransform();
            if (player == null)
            {
                // 找不到玩家：直接跳过追踪，且不缓存朝向——避免关闭时用一个没意义的默认值去恢复。
                DebugTools.LogWarning("[PlayerInventoryFacing] 未找到 GameManager.CurrentPlayer，跳过朝向追踪。");
                return;
            }

            SmoothFacingTween.Kill(ref _restoreTween);
            _rotationBeforeOpen = player.rotation;
            _hasCachedRotation = true;
            _tracking = true;
        }

        /// <summary>
        /// 若打开时从未成功缓存过朝向（例如当时找不到玩家），则跳过，避免误用默认值转身。
        /// </summary>
        private void EndTracking()
        {
            _tracking = false;
            if (!_hasCachedRotation) return;
            _hasCachedRotation = false;

            Transform player = ResolvePlayerTransform();
            if (player == null) return;

            SmoothFacingTween.TryRotateToRotation(
                player,
                _rotationBeforeOpen,
                ref _restoreTween,
                restoreOptions);
        }

        private void LateUpdate()
        {
            if (!_tracking) return;

            Transform player = ResolvePlayerTransform();
            Camera mainCam = Camera.main;
            if (player == null || mainCam == null) return;

            Vector3 toCamera = mainCam.transform.position - player.position;
            if (yAxisOnly) toCamera.y = 0f;
            if (toCamera.sqrMagnitude <= 0.0001f) return;

            Quaternion targetRotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            if (yAxisOnly)
            {
                Vector3 currentEuler = player.eulerAngles;
                Vector3 targetEuler = targetRotation.eulerAngles;
                targetRotation = Quaternion.Euler(currentEuler.x, targetEuler.y, currentEuler.z);
            }

            player.rotation = Quaternion.RotateTowards(
                player.rotation, targetRotation, trackRotateSpeedDegPerSec * Time.deltaTime);
        }

        private Transform ResolvePlayerTransform()
        {
            GameObject playerObj = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
            return playerObj != null ? playerObj.transform : null;
        }
    }
}
