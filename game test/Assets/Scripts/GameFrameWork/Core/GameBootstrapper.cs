using UnityEngine;
using IndieGame.UI;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.Economy;
using IndieGame.Core.CameraSystem;

namespace IndieGame.Core
{
    /// <summary>
    /// 游戏启动器
    /// 负责在场景加载完毕后，按正确顺序启动各个系统
    /// </summary>
    public class GameBootstrapper : MonoBehaviour
    {
        [Header("Settings")]
        // 是否在 Start 时自动初始化
        public bool autoInitOnStart = true;
        
        [Tooltip("如果是开发测试场景，可能没有主菜单，直接进入 Gameplay")]
        // 是否为测试场景（用于跳过主菜单流程）
        public bool isTestScene = true;

        [Header("Manager Prefabs")]
        // 各类系统管理器的预制体（允许在 Inspector 中配置）
        [SerializeField] private GameObject gameManagerPrefab;
        [SerializeField] private GameObject uiManagerPrefab;
        [SerializeField] private GameObject inventoryManagerPrefab;
        [SerializeField] private GameObject boardGameManagerPrefab;
        [SerializeField] private GameObject cameraManagerPrefab;
        [SerializeField] private GameObject sceneLoaderPrefab;
        [SerializeField] private GameObject boardMapManagerPrefab;
        [SerializeField] private GameObject boardEntityManagerPrefab;
        [SerializeField] private GameObject goldSystemPrefab;
        // 玩家预制体（交由 GameManager 实例化）
        [SerializeField] private GameObject playerPrefab;

        // --- 已创建的管理器实例缓存 ---
        private GameManager _gameManagerInstance;
        private UIManager _uiManagerInstance;
        private InventoryManager _inventoryManagerInstance;
        private BoardGameManager _boardGameManagerInstance;
        private CameraManager _cameraManagerInstance;
        private SceneLoader _sceneLoaderInstance;
        private BoardMapManager _boardMapManagerInstance;
        private BoardEntityManager _boardEntityManagerInstance;
        private GoldSystem _goldSystemInstance;

        private void Awake()
        {
            // 提前确保有全局根节点，避免系统散落到场景中
            EnsureGameSystemRoot();
        }

        private void Start()
        {
            // 根据开关决定是否自动启动
            if (autoInitOnStart)
            {
                Bootstrap();
            }
        }

        private void Bootstrap()
        {
            Debug.Log("[GameBootstrapper] Starting Game Systems...");

            // 1) 统一确保所有管理器在同一根节点下生成
            GameObject root = EnsureGameSystemRoot();
            var gm = EnsureManagerFromPrefab(root, gameManagerPrefab, "GameManager", ref _gameManagerInstance);
            EnsureManagerFromPrefab(root, uiManagerPrefab, "UIManager", ref _uiManagerInstance);
            EnsureManagerFromPrefab(root, boardGameManagerPrefab, "BoardGameManager", ref _boardGameManagerInstance);
            EnsureManagerFromPrefab(root, inventoryManagerPrefab, "InventoryManager", ref _inventoryManagerInstance);
            EnsureManagerFromPrefab(root, cameraManagerPrefab, "CameraManager", ref _cameraManagerInstance);
            EnsureManagerFromPrefab(root, sceneLoaderPrefab, "SceneLoader", ref _sceneLoaderInstance);
            EnsureManagerFromPrefab(root, boardMapManagerPrefab, "BoardMapManager", ref _boardMapManagerInstance);
            EnsureManagerFromPrefab(root, boardEntityManagerPrefab, "BoardEntityManager", ref _boardEntityManagerInstance);
            // 金币系统也纳入统一引导：
            // 1) 优先复用已有实例；
            // 2) 若未配置预制体则自动创建并挂载 GoldSystem 组件；
            // 3) 与其他系统同级放在 [GameSystem] 根节点下，便于运维与排查。
            EnsureManagerFromPrefab(root, goldSystemPrefab, "GoldSystem", ref _goldSystemInstance);

            // 2. 可以在这里查找场景里的其他依赖
            // var ui = FindObjectOfType<UIManager>();
            // ui.Init();

            // 3. 正式启动游戏逻辑
            if (gm != null)
            {
                // 记录是否已初始化，避免重复进入测试逻辑
                bool wasInitialized = gm.IsInitialized;
                if (playerPrefab != null)
                {
                    // 注入玩家预制体给 GameManager
                    gm.SetPlayerPrefab(playerPrefab);
                }
                // 执行全局系统初始化
                gm.InitGame();
                
                // 如果是测试场景，可能会强制覆盖状态
                if (isTestScene && !wasInitialized)
                {
                    // 确保刚开始是自由移动
                    if(gm.CurrentState != GameState.FreeRoam) 
                        gm.ChangeState(GameState.FreeRoam);
                }
            }
        }

        private GameObject EnsureGameSystemRoot()
        {
            // 统一使用固定名称的根对象存放所有管理器。
            // 若已经存在（例如由其他启动引导器提前创建），则直接复用，避免重复根节点。
            GameObject root = GameObject.Find("[GameSystem]");
            if (root == null)
            {
                root = new GameObject("[GameSystem]");
            }

            if (root.GetComponent<DontDestroyRoot>() == null)
            {
                root.AddComponent<DontDestroyRoot>();
            }
            return root;
        }

        private T EnsureManagerFromPrefab<T>(GameObject root, GameObject prefab, string fallbackName, ref T instance)
            where T : MonoBehaviour
        {
            // 已有实例时直接返回
            if (instance != null) return instance;

            // 优先查找根节点下已有组件（避免重复创建）
            T existing = root.GetComponentInChildren<T>(true);
            if (existing != null)
            {
                instance = existing;
                return instance;
            }

            GameObject go;
            if (prefab != null)
            {
                // 使用预制体实例化
                go = Instantiate(prefab, root.transform);
            }
            else
            {
                // 兜底创建空物体并附加组件
                Debug.LogWarning($"[GameBootstrapper] Missing manager prefab for {fallbackName}, creating empty GameObject.");
                go = new GameObject(fallbackName);
                go.transform.SetParent(root.transform, false);
            }

            instance = go.GetComponent<T>();
            if (instance == null)
            {
                // 预制体上没有目标组件时，动态添加
                instance = go.AddComponent<T>();
            }
            return instance;
        }
    }
}
