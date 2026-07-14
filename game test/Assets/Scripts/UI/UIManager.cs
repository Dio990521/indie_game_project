using System;
using UnityEngine;
using IndieGame.Core.Utilities;
using UnityEngine.SceneManagement;
using IndieGame.Core;
using DG.Tweening;
using IndieGame.UI.Crafting;
using IndieGame.UI.Dialogue;
using IndieGame.UI.Hud;
using IndieGame.UI.Shop;
using IndieGame.UI.Treasure;
using IndieGame.UI.Town;
using IndieGame.UI.SystemMenu;
using IndieGame.UI.SkillTree;
using IndieGame.UI.Memory;
using IndieGame.UI.Equipment;

namespace IndieGame.UI
{
    /// <summary>
    /// UI 层级优先级：
    /// 对应 UICanvas 下各独立子 Canvas 的 SortingOrder，数值越高渲染越靠前。
    /// </summary>
    public enum UILayerPriority
    {
        // ScreenSpaceCamera，世界相机关联 UI
        Bottom25,
        // ScreenSpaceOverlay SortingOrder=10，游戏功能 UI（HUD、背包、商店等）
        GameUI,
        // ScreenSpaceOverlay SortingOrder=20，系统菜单（语言切换、存读档）
        SystemUI,
        // ScreenSpaceOverlay SortingOrder=30，确认弹窗（始终在菜单上方）
        Popup,
        // ScreenSpaceOverlay SortingOrder=40，全屏转场遮罩（始终最顶层）
        Fullscreen
    }

    /// <summary>
    /// UI 管理器（单例）：
    /// 负责 UI Canvas 的创建、根节点缓存、UI 实例生成与场景切换的相机同步。
    /// </summary>
    public class UIManager : MonoSingleton<UIManager>
    {
        // 各层级根节点缓存（由 CacheRoots 从 UIPriorityRoots 读取）
        private Transform layerCameraBottom25;
        private Transform layerGameUI;
        private Transform layerSystemUI;
        private Transform layerPopup;
        private Transform layerFullscreen;
        // Canvas 上的根节点组件
        private UIPriorityRoots uiRoots;

        [Header("UI Prefabs")]
        // UI Canvas 预制体
        [SerializeField] private GameObject uiCanvasPrefab;
        // 全屏黑屏遮罩预制体（CanvasGroup，用于转场淡入淡出）
        [SerializeField] private CanvasGroup fullscreenFadePrefab;
        // 棋盘操作菜单
        [SerializeField] private BoardActionMenuView boardActionMenuPrefab;
        // 背包 UI（全屏版）
        [SerializeField] private Inventory.InventoryFullScreenController inventoryPrefab;
        // 确认弹窗
        [SerializeField] private Confirmation.ConfirmationPopupView confirmationPrefab;
        // 露营 UI
        [SerializeField] private Camp.CampUIView campUIPrefab;
        // 打造 UI（仅由 UIManager 负责实例化）
        [SerializeField] private CraftingUIController craftingUIPrefab;
        // 对话 UI（仅由 UIManager 负责实例化）
        [SerializeField] private DialogueUIView dialogueUIPrefab;
        // 玩家 HUD（仅由 UIManager 负责实例化）
        [SerializeField] private PlayerHudController playerHudPrefab;
        // 商店 UI（仅由 UIManager 负责实例化）
        [SerializeField] private ShopUIController shopUIPrefab;
        // 宝具菜单 UI（仅由 UIManager 负责实例化）
        [SerializeField] private TreasureMenuView treasureMenuPrefab;
        // 城镇菜单 UI（仅由 UIManager 负责实例化）
        [SerializeField] private TownUIView townUIPrefab;
        // 系统菜单（语言切换）——始终常驻，挂在 Top75 层
        [SerializeField] private SystemMenuController systemMenuPrefab;
        // 技能树 UI（仅由 UIManager 负责实例化）
        [SerializeField] private SkillTreeController skillTreeUIPrefab;
        // Memory 图鉴 UI（仅由 UIManager 负责实例化）
        [SerializeField] private MemoryUIController memoryUIPrefab;
        // 装备 UI（仅由 UIManager 负责实例化）
        [SerializeField] private EquipmentUIController equipmentUIPrefab;

        // --- 运行时实例 ---
        public GameObject CanvasInstance { get; private set; }
        public BoardActionMenuView BoardActionMenuInstance { get; private set; }
        public Inventory.InventoryFullScreenController InventoryInstance { get; private set; }
        public Confirmation.ConfirmationPopupView ConfirmationInstance { get; private set; }
        public Camp.CampUIView CampUIInstance { get; private set; }
        public CraftingUIController CraftingUIInstance { get; private set; }
        public DialogueUIView DialogueUIInstance { get; private set; }
        public PlayerHudController PlayerHudInstance { get; private set; }
        public ShopUIController ShopUIInstance { get; private set; }
        public TreasureMenuView TreasureMenuInstance { get; private set; }
        public TownUIView TownUIInstance { get; private set; }
        public SystemMenuController SystemMenuInstance { get; private set; }
        public SkillTreeController SkillTreeUIInstance { get; private set; }
        public MemoryUIController MemoryUIInstance { get; private set; }
        public EquipmentUIController EquipmentUIInstance { get; private set; }
        // 全屏黑屏遮罩实例
        public CanvasGroup FullscreenFadeInstance { get; private set; }

        // UI 准备完成事件（供外部监听）
        public static event Action OnUIReady;
        // 是否已初始化
        private bool _isInitialized;

        // UI 管理器在历史代码中通过旧 DestroyOnLoad => true 跨场景保留（基类语义反向）。
        // 迁移到 KeepAcrossScenes 时保持运行时行为一致：跨场景常驻，由 GameBootstrapper
        // 在新场景中复用同一个实例。
        protected override bool KeepAcrossScenes => true;

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
            // 若根节点丢失，重新缓存
            if (layerGameUI == null) CacheRoots();

            return priority switch
            {
                UILayerPriority.Bottom25   => layerCameraBottom25,
                UILayerPriority.GameUI     => layerGameUI,
                UILayerPriority.SystemUI   => layerSystemUI,
                UILayerPriority.Popup      => layerPopup,
                UILayerPriority.Fullscreen => layerFullscreen,
                _                          => layerGameUI,
            };
        }

        /// <summary>
        /// 在指定层级生成 UI 实例。
        /// </summary>
        public T SpawnOnLayer<T>(T prefab, UILayerPriority priority) where T : Component
        {
            Transform root = GetRoot(priority);
            if (root == null)
            {
                DebugTools.LogWarning("[UIManager] Missing UI root for layer.");
                return null;
            }

            return Instantiate(prefab, root);
        }

        /// <summary>
        /// 将已有 UI 挂载到指定层级。1
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
        /// L6 重构说明：原先每个 UI 都是一段"判空 → SpawnOnLayer → SetActive"的重复代码（15 段），
        /// 收敛为 EnsureUIInstance 泛型辅助后每个 UI 只占一行，新增 UI 时只需加一行 + 一个字段。
        /// 统一约定：UIManager 只负责"生成实例并保持激活"，
        /// 显示/隐藏一律由各 Controller/View 监听 EventBus 自行控制。
        /// </summary>
        private void SpawnUI()
        {
            // ── GameUI 层（SortingOrder=10）：游戏功能 UI ──────────────────────
            BoardActionMenuInstance = EnsureUIInstance(boardActionMenuPrefab, BoardActionMenuInstance, UILayerPriority.GameUI);
            InventoryInstance       = EnsureUIInstance(inventoryPrefab,       InventoryInstance,       UILayerPriority.GameUI);
            CampUIInstance          = EnsureUIInstance(campUIPrefab,          CampUIInstance,          UILayerPriority.GameUI);
            CraftingUIInstance      = EnsureUIInstance(craftingUIPrefab,      CraftingUIInstance,      UILayerPriority.GameUI);
            DialogueUIInstance      = EnsureUIInstance(dialogueUIPrefab,      DialogueUIInstance,      UILayerPriority.GameUI);
            PlayerHudInstance       = EnsureUIInstance(playerHudPrefab,       PlayerHudInstance,       UILayerPriority.GameUI);
            ShopUIInstance          = EnsureUIInstance(shopUIPrefab,          ShopUIInstance,          UILayerPriority.GameUI);
            TreasureMenuInstance    = EnsureUIInstance(treasureMenuPrefab,    TreasureMenuInstance,    UILayerPriority.GameUI);
            TownUIInstance          = EnsureUIInstance(townUIPrefab,          TownUIInstance,          UILayerPriority.GameUI);
            SkillTreeUIInstance     = EnsureUIInstance(skillTreeUIPrefab,     SkillTreeUIInstance,     UILayerPriority.GameUI);
            MemoryUIInstance        = EnsureUIInstance(memoryUIPrefab,        MemoryUIInstance,        UILayerPriority.GameUI);
            EquipmentUIInstance     = EnsureUIInstance(equipmentUIPrefab,     EquipmentUIInstance,     UILayerPriority.GameUI);

            // ── SystemUI 层（SortingOrder=20）：系统菜单 ──────────────────────
            SystemMenuInstance = EnsureUIInstance(systemMenuPrefab, SystemMenuInstance, UILayerPriority.SystemUI);

            // ── Popup 层（SortingOrder=30）：确认弹窗，始终在菜单上方 ────────
            ConfirmationInstance = EnsureUIInstance(confirmationPrefab, ConfirmationInstance, UILayerPriority.Popup);

            // ── Fullscreen 层（SortingOrder=40）：全屏遮罩，绝对最顶层 ────────
            bool fadeJustSpawned = FullscreenFadeInstance == null && fullscreenFadePrefab != null;
            FullscreenFadeInstance = EnsureUIInstance(fullscreenFadePrefab, FullscreenFadeInstance, UILayerPriority.Fullscreen);
            if (fadeJustSpawned && FullscreenFadeInstance != null)
            {
                // 遮罩初始为完全透明且不拦截点击
                FullscreenFadeInstance.alpha = 0f;
                FullscreenFadeInstance.blocksRaycasts = false;
                FullscreenFadeInstance.interactable = false;
            }

            // 通知外部 UI 已准备完毕
            OnUIReady?.Invoke();
        }

        /// <summary>
        /// 确保某个 UI 实例存在（L6 新增）：
        /// - 已生成或未配置预制体：原样返回；
        /// - 否则在指定层级实例化并激活后返回。
        /// </summary>
        private T EnsureUIInstance<T>(T prefab, T existing, UILayerPriority layer) where T : Component
        {
            if (prefab == null || existing != null) return existing;

            T instance = SpawnOnLayer(prefab, layer);
            if (instance != null) instance.gameObject.SetActive(true);
            return instance;
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
            if (layerCameraBottom25 == null) CacheRoots();
            if (layerCameraBottom25 == null) return;
            var canvas = layerCameraBottom25.GetComponent<Canvas>();
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
                    DebugTools.LogWarning("[UIManager] UIRoots component missing on Canvas.");
                    return;
                }
            }

            layerCameraBottom25 = uiRoots.CameraBottom25;
            layerGameUI          = uiRoots.OverlayGameUI;
            layerSystemUI        = uiRoots.OverlaySystemUI;
            layerPopup           = uiRoots.OverlayPopup;
            layerFullscreen      = uiRoots.OverlayFullscreen;
        }

        private void EnsureCanvasInstance()
        {
            if (CanvasInstance != null) return;

            if (uiCanvasPrefab == null)
            {
                DebugTools.LogWarning("[UIManager] Missing UICanvas prefab.");
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
                DebugTools.LogWarning("[UIManager] Missing fullscreenFade CanvasGroup.");
                return;
            }

            // 防止重入动画叠加
            FullscreenFadeInstance.DOKill();
            FullscreenFadeInstance.blocksRaycasts = true;
            FullscreenFadeInstance.interactable = false;

            float targetAlpha = evt.FadeIn ? 1f : 0f;
            // M2 修复：SetUpdate(true) 让淡入淡出使用 unscaled time，
            // 与 SceneLoader 的 WaitForSecondsRealtime 对齐，timeScale=0（暂停）时转场不会卡住
            FullscreenFadeInstance.DOFade(targetAlpha, evt.Duration).SetUpdate(true).OnComplete(() =>
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
