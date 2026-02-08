using System;
using UnityEngine;
using IndieGame.Core.Utilities;
using UnityEngine.SceneManagement;
using IndieGame.Core;
using DG.Tweening;
using IndieGame.UI.Crafting;

namespace IndieGame.UI
{
    /// <summary>
    /// UI 层级优先级：
    /// 用于区分 UI 的不同渲染层（例如 Overlay vs Camera）。
    /// </summary>
    public enum UILayerPriority
    {
        // 顶部 Overlay 层（屏幕 UI）
        Bottom25,
        // 依赖 Camera 的 UI 层
        Top75
    }

    /// <summary>
    /// UI 管理器（单例）：
    /// 负责 UI Canvas 的创建、根节点缓存、UI 实例生成与场景切换的相机同步。
    /// </summary>
    public class UIManager : MonoSingleton<UIManager>
    {
        // 顶部 Overlay 根节点（通常为 ScreenSpaceOverlay）
        private Transform screenOverlayTop75;
        // Camera 渲染根节点（ScreenSpaceCamera）
        private Transform screenCameraBottom25;
        // Canvas 上的根节点组件
        private UIPriorityRoots uiRoots;

        [Header("UI Prefabs")]
        // UI Canvas 预制体
        [SerializeField] private GameObject uiCanvasPrefab;
        // 全屏黑屏遮罩预制体（CanvasGroup，用于转场淡入淡出）
        [SerializeField] private CanvasGroup fullscreenFadePrefab;
        // 棋盘操作菜单
        [SerializeField] private BoardActionMenuView boardActionMenuPrefab;
        // 背包 UI
        [SerializeField] private Inventory.InventoryUIView inventoryPrefab;
        // 确认弹窗
        [SerializeField] private Confirmation.ConfirmationPopupView confirmationPrefab;
        // 露营 UI
        [SerializeField] private Camp.CampUIView campUIPrefab;
        // 打造 UI（仅由 UIManager 负责实例化）
        [SerializeField] private CraftingUIController craftingUIPrefab;

        // --- 运行时实例 ---
        public GameObject CanvasInstance { get; private set; }
        public BoardActionMenuView BoardActionMenuInstance { get; private set; }
        public Inventory.InventoryUIView InventoryInstance { get; private set; }
        public Confirmation.ConfirmationPopupView ConfirmationInstance { get; private set; }
        public Camp.CampUIView CampUIInstance { get; private set; }
        public CraftingUIController CraftingUIInstance { get; private set; }
        // 全屏黑屏遮罩实例
        public CanvasGroup FullscreenFadeInstance { get; private set; }

        // UI 准备完成事件（供外部监听）
        public static event Action OnUIReady;
        // 是否已初始化
        private bool _isInitialized;

        // UI 管理器在场景切换时销毁（由 GameBootstrapper 重新创建）
        protected override bool DestroyOnLoad => true;

        protected override void Awake()
        {
            base.Awake();
            // 单例保护
            if (Instance != this) return;
        }

        // 由 GameManager 以确定性顺序调用。
        public void Init()
        {
            if (_isInitialized) return;
            // 确保 Canvas 先存在
            EnsureCanvasInstance();
            // 缓存根节点
            CacheRoots();
            // 生成 UI 实例
            InitUI();
            _isInitialized = true;
        }

        private void OnEnable()
        {
            // 监听场景加载与游戏状态变化
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EventBus.Subscribe<GameStateChangedEvent>(HandleGameStateChanged);
            EventBus.Subscribe<FadeRequestedEvent>(HandleFadeRequested);
        }

        private void OnDisable()
        {
            // 退订事件，避免生命周期结束后被调用
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            EventBus.Unsubscribe<GameStateChangedEvent>(HandleGameStateChanged);
            EventBus.Unsubscribe<FadeRequestedEvent>(HandleFadeRequested);
        }

        /// <summary>
        /// 获取指定优先级的 UI 根节点。
        /// </summary>
        public Transform GetRoot(UILayerPriority priority)
        {
            if (screenOverlayTop75 == null || screenCameraBottom25 == null)
            {
                // 若根节点丢失，重新缓存
                CacheRoots();
            }
            return priority == UILayerPriority.Top75 ? screenOverlayTop75 : screenCameraBottom25;
        }

        /// <summary>
        /// 在指定层级生成 UI 实例。
        /// </summary>
        public T SpawnOnLayer<T>(T prefab, UILayerPriority priority) where T : Component
        {
            Transform root = GetRoot(priority);
            if (root == null)
            {
                Debug.LogWarning("[UIManager] Missing UI root for layer.");
                return null;
            }

            return Instantiate(prefab, root);
        }

        /// <summary>
        /// 将已有 UI 挂载到指定层级。
        /// </summary>
        public void AttachToLayer(Transform uiRoot, UILayerPriority priority)
        {
            Transform root = GetRoot(priority);
            if (root == null || uiRoot == null) return;
            uiRoot.SetParent(root, false);
        }

        private void InitUI()
        {
            // 生成 UI，并同步 Camera
            SpawnUI();
            RefreshWorldCamera();
        }

        /// <summary>
        /// 生成 UI 预制体实例（若尚未生成）。
        /// </summary>
        private void SpawnUI()
        {

            if (boardActionMenuPrefab != null && BoardActionMenuInstance == null)
            {
                BoardActionMenuInstance = SpawnOnLayer(boardActionMenuPrefab, UILayerPriority.Top75);
                if (BoardActionMenuInstance != null) BoardActionMenuInstance.gameObject.SetActive(true);
            }

            if (inventoryPrefab != null && InventoryInstance == null)
            {
                InventoryInstance = SpawnOnLayer(inventoryPrefab, UILayerPriority.Top75);
                if (InventoryInstance != null) InventoryInstance.gameObject.SetActive(true);
            }

            if (confirmationPrefab != null && ConfirmationInstance == null)
            {
                ConfirmationInstance = SpawnOnLayer(confirmationPrefab, UILayerPriority.Top75);
                if (ConfirmationInstance != null) ConfirmationInstance.gameObject.SetActive(true);
            }

            if (campUIPrefab != null && CampUIInstance == null)
            {
                CampUIInstance = SpawnOnLayer(campUIPrefab, UILayerPriority.Top75);
                if (CampUIInstance != null) CampUIInstance.gameObject.SetActive(true);
            }

            if (craftingUIPrefab != null && CraftingUIInstance == null)
            {
                CraftingUIInstance = SpawnOnLayer(craftingUIPrefab, UILayerPriority.Top75);
                if (CraftingUIInstance != null)
                {
                    // UIManager 仅负责“生成实例”：
                    // 保持对象激活，让 CraftingUIController 能持续监听 EventBus 并自行控制 show/hide。
                    CraftingUIInstance.gameObject.SetActive(true);
                }
            }

            // 生成全屏遮罩，确保始终在 UI 最上层
            if (fullscreenFadePrefab != null && FullscreenFadeInstance == null)
            {
                FullscreenFadeInstance = SpawnOnLayer(fullscreenFadePrefab, UILayerPriority.Top75);
                if (FullscreenFadeInstance != null)
                {
                    FullscreenFadeInstance.gameObject.SetActive(true);
                    FullscreenFadeInstance.alpha = 0f;
                    FullscreenFadeInstance.blocksRaycasts = false;
                    FullscreenFadeInstance.interactable = false;
                }
            }

            // 通知外部 UI 已准备完毕
            OnUIReady?.Invoke();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 场景切换后刷新 UI Camera
            RefreshWorldCamera();
        }

        private void HandleGameStateChanged(GameStateChangedEvent evt)
        {
            // 非棋盘模式时隐藏棋盘菜单
            if (evt.NewState == GameState.BoardMode) return;
            if (BoardActionMenuInstance != null)
            {
                BoardActionMenuInstance.Hide();
            }
        }

        private void RefreshWorldCamera()
        {
            if (screenCameraBottom25 == null)
            {
                // 若根节点丢失，重新缓存
                CacheRoots();
            }
            if (screenCameraBottom25 == null) return;
            var canvas = screenCameraBottom25.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                // 绑定主相机，确保 UI 与场景 Camera 同步
                canvas.worldCamera = Camera.main;
            }
        }

        private void CacheRoots()
        {
            if (CanvasInstance == null) return;

            if (uiRoots == null)
            {
                uiRoots = CanvasInstance.GetComponent<UIPriorityRoots>();
                if (uiRoots == null)
                {
                    Debug.LogWarning("[UIManager] UIRoots component missing on Canvas.");
                    return;
                }
            }

            screenOverlayTop75 = uiRoots.OverlayTop75;
            screenCameraBottom25 = uiRoots.CameraBottom25;
        }

        private void EnsureCanvasInstance()
        {
            if (CanvasInstance != null) return;

            if (uiCanvasPrefab == null)
            {
                Debug.LogWarning("[UIManager] Missing UICanvas prefab.");
                return;
            }

            // 实例化 Canvas 并设置名称
            CanvasInstance = Instantiate(uiCanvasPrefab);
            CanvasInstance.name = "UICanvas";
            // 确保 Canvas 常驻
            EnsureDontDestroyRoot(CanvasInstance);
        }

        private void EnsureDontDestroyRoot(GameObject target)
        {
            if (target == null) return;
            if (target.GetComponent<DontDestroyRoot>() == null)
            {
                // 添加 DontDestroyRoot 组件
                target.AddComponent<DontDestroyRoot>();
            }
        }

        /// <summary>
        /// 处理全屏淡入淡出事件：
        /// 通过 DOTween 改变 CanvasGroup.alpha 实现黑屏转场。
        /// </summary>
        private void HandleFadeRequested(FadeRequestedEvent evt)
        {
            if (FullscreenFadeInstance == null)
            {
                Debug.LogWarning("[UIManager] Missing fullscreenFade CanvasGroup.");
                return;
            }

            // 防止重入动画叠加
            FullscreenFadeInstance.DOKill();
            FullscreenFadeInstance.blocksRaycasts = true;
            FullscreenFadeInstance.interactable = false;

            float targetAlpha = evt.FadeIn ? 1f : 0f;
            FullscreenFadeInstance.DOFade(targetAlpha, evt.Duration).OnComplete(() =>
            {
                // 淡出完成后解除遮挡
                if (!evt.FadeIn)
                {
                    FullscreenFadeInstance.blocksRaycasts = false;
                }
            });
        }
    }
}
