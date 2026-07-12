using IndieGame.Core;
using IndieGame.Core.Utilities;
using Unity.Cinemachine;
using UnityEngine;

namespace IndieGame.Core.CameraSystem
{
    /// <summary>
    /// 背包镜头偏移控制器：
    /// 监听背包打开/关闭事件，对"当前正在生效的 Cinemachine 摄像机"应用/还原屏幕取景偏移，
    /// 让角色移动到屏幕左半边，为右侧的全屏背包 UI 让出空间。
    ///
    /// 设计说明（与 ActionMenuCameraController / DialogueCameraController 的区别）：
    /// 1) 不新建专属摄像机，也不做 Priority 切换——背包可能在棋盘操作菜单、露营、城镇等
    ///    不同场景下打开，此时"当前生效镜头"各不相同，新建专属镜头反而需要逐场景配置。
    ///    这里直接通过 CinemachineBrain.ActiveVirtualCamera 找到当前生效的镜头并偏移其构图，
    ///    天然兼容任意场景。
    /// 2) PositionComposer 源码明确不对 ScreenPosition 的变化做阻尼（"Don't damp change to
    ///    desired screen position"，见 CinemachinePositionComposer.cs），直接赋值会瞬移。
    ///    因此这里在 Update 中用 Vector2.SmoothDamp 手动实现平滑过渡，不依赖 Composer 自身阻尼。
    /// 3) Update 每帧都读取 Inspector 上的 inventoryScreenPositionOffset 作为目标值，
    ///    因此背包打开状态下在 Play Mode 里实时拖动该字段即可实时预览偏移效果，
    ///    配合 [SaveDuringPlay] 调好的数值会在退出 Play Mode 后保留。
    /// 4) 若当前镜头的 Body 组件不是 PositionComposer（例如城镇/露营用了别的取景方式），
    ///    则跳过并提示一次，不影响其余功能。
    /// </summary>
    [DisallowMultipleComponent]
    [SaveDuringPlay]
    public class InventoryCameraController : MonoBehaviour
    {
        [Header("Screen Position Offset")]
        [Tooltip("背包打开时的目标归一化取景偏移（PositionComposer.Composition.ScreenPosition）。X 为负值可将角色推向屏幕左侧，为右侧背包 UI 让出空间。背包打开时可在 Play Mode 里实时拖动此值预览效果。")]
        [SerializeField] private Vector2 inventoryScreenPositionOffset = new Vector2(-0.25f, 0f);

        [Header("Smoothing")]
        [Tooltip("镜头从当前取景位置过渡到目标位置的平滑时间（秒），值越大过渡越慢越柔和。")]
        [SerializeField] private float transitionSmoothTime = 0.35f;

        // 缓存背包打开瞬间生效的镜头 Composer，确保关闭时精确还原到同一个组件（而不是重新查找"当前"镜头）
        private CinemachinePositionComposer _activeComposer;
        // 打开背包前的原始取景偏移，用于关闭时还原
        private Vector2 _originalScreenPosition;
        // 当前已经写入 composer 的取景位置（SmoothDamp 的运行时状态，非目标值）
        private Vector2 _currentScreenPosition;
        // SmoothDamp 所需的速度缓存
        private Vector2 _smoothVelocity;
        // 背包当前是否处于打开状态（决定 Update 里追逐的目标是偏移值还是原始值）
        private bool _inventoryOpen;

        private void OnEnable()
        {
            EventBus.Subscribe<InventoryOpenedEvent>(HandleInventoryOpened);
            EventBus.Subscribe<InventoryClosedEvent>(HandleInventoryClosed);
            // 装备界面与背包是同一类全屏 UI（右侧内容区），复用同一套取景偏移效果。
            EventBus.Subscribe<EquipmentUIOpenedEvent>(HandleEquipmentUIOpened);
            EventBus.Subscribe<EquipmentUIClosedEvent>(HandleEquipmentUIClosed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InventoryOpenedEvent>(HandleInventoryOpened);
            EventBus.Unsubscribe<InventoryClosedEvent>(HandleInventoryClosed);
            EventBus.Unsubscribe<EquipmentUIOpenedEvent>(HandleEquipmentUIOpened);
            EventBus.Unsubscribe<EquipmentUIClosedEvent>(HandleEquipmentUIClosed);

            // 组件被禁用时若偏移仍生效，直接瞬间还原（不再有 Update 帮忙平滑），避免摄像机卡在偏移状态
            if (_activeComposer != null)
            {
                _activeComposer.Composition.ScreenPosition = _originalScreenPosition;
                _activeComposer = null;
            }
            _inventoryOpen = false;
        }

        /// <summary>
        /// 每帧把当前取景位置平滑追向目标值（打开时追向 inventoryScreenPositionOffset，
        /// 关闭时追向 _originalScreenPosition），并写回 Composer。
        /// </summary>
        private void Update()
        {
            if (_activeComposer == null) return;

            Vector2 target = _inventoryOpen ? inventoryScreenPositionOffset : _originalScreenPosition;
            _currentScreenPosition = Vector2.SmoothDamp(
                _currentScreenPosition, target, ref _smoothVelocity, Mathf.Max(0.01f, transitionSmoothTime));
            _activeComposer.Composition.ScreenPosition = _currentScreenPosition;

            // 背包已关闭且已经平滑回到原始位置：停止逐帧写入并释放引用
            if (!_inventoryOpen && (_currentScreenPosition - _originalScreenPosition).sqrMagnitude < 0.0000001f)
            {
                _activeComposer.Composition.ScreenPosition = _originalScreenPosition;
                _activeComposer = null;
            }
        }

        /// <summary>
        /// 背包打开：找到当前生效镜头的 Composer，缓存其原始取景位置，交给 Update 平滑过渡到目标偏移。
        /// </summary>
        private void HandleInventoryOpened(InventoryOpenedEvent evt) => OpenScreenOffset();

        /// <summary>
        /// 背包关闭：不立即写值，交给 Update 平滑追回 _originalScreenPosition。
        /// </summary>
        private void HandleInventoryClosed(InventoryClosedEvent evt) => CloseScreenOffset();

        // 装备界面与背包共用同一套让位效果，直接转调同一份逻辑。
        private void HandleEquipmentUIOpened(EquipmentUIOpenedEvent evt) => OpenScreenOffset();
        private void HandleEquipmentUIClosed(EquipmentUIClosedEvent evt) => CloseScreenOffset();

        private void OpenScreenOffset()
        {
            CinemachinePositionComposer composer = ResolveActiveComposer();
            if (composer == null) return;

            // 仅在镜头发生变化（或本来没有镜头）时重新缓存"原始值"，
            // 避免快速连续开合背包/装备界面时，把过渡中途的值误当成原始值缓存下来。
            if (_activeComposer != composer)
            {
                _activeComposer = composer;
                _originalScreenPosition = composer.Composition.ScreenPosition;
                _currentScreenPosition = _originalScreenPosition;
                _smoothVelocity = Vector2.zero;
            }

            _inventoryOpen = true;
        }

        private void CloseScreenOffset()
        {
            _inventoryOpen = false;
        }

        /// <summary>
        /// 解析当前正在生效的 Cinemachine 摄像机上挂载的 PositionComposer。
        /// 通过 CinemachineBrain.ActiveVirtualCamera 获取，天然兼容棋盘/露营/城镇等
        /// 场景下由不同镜头接管的情况。
        /// </summary>
        private CinemachinePositionComposer ResolveActiveComposer()
        {
            CinemachineBrain brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            if (brain == null || brain.ActiveVirtualCamera == null)
            {
                DebugTools.LogWarning("[InventoryCameraController] 未找到激活的 CinemachineBrain/当前生效镜头，跳过背包取景偏移。");
                return null;
            }

            // CinemachineCamera 等虚拟摄像机均继承自 CinemachineVirtualCameraBase（MonoBehaviour），
            // 可直接 GetComponent 取同物体上的 PositionComposer。
            CinemachineVirtualCameraBase activeCamera = brain.ActiveVirtualCamera as CinemachineVirtualCameraBase;
            if (activeCamera == null) return null;

            CinemachinePositionComposer composer = activeCamera.GetComponent<CinemachinePositionComposer>();
            if (composer == null)
            {
                DebugTools.LogWarning($"[InventoryCameraController] 当前生效镜头 \"{activeCamera.name}\" 未使用 CinemachinePositionComposer，跳过背包取景偏移。");
            }
            return composer;
        }
    }
}
