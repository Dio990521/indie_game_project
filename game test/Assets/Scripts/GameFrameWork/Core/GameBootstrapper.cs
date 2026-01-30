using UnityEngine;
using IndieGame.UI;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Inventory;
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
        public bool autoInitOnStart = true;
        
        [Tooltip("如果是开发测试场景，可能没有主菜单，直接进入 Gameplay")]
        public bool isTestScene = true;

        [Header("Manager Prefabs")]
        [SerializeField] private GameObject gameManagerPrefab;
        [SerializeField] private GameObject uiManagerPrefab;
        [SerializeField] private GameObject inventoryManagerPrefab;
        [SerializeField] private GameObject boardGameManagerPrefab;
        [SerializeField] private GameObject cameraManagerPrefab;
        [SerializeField] private GameObject sceneLoaderPrefab;
        [SerializeField] private GameObject boardMapManagerPrefab;
        [SerializeField] private GameObject boardEntityManagerPrefab;
        [SerializeField] private GameObject playerPrefab;

        private GameManager _gameManagerInstance;
        private UIManager _uiManagerInstance;
        private InventoryManager _inventoryManagerInstance;
        private BoardGameManager _boardGameManagerInstance;
        private CameraManager _cameraManagerInstance;
        private SceneLoader _sceneLoaderInstance;
        private BoardMapManager _boardMapManagerInstance;
        private BoardEntityManager _boardEntityManagerInstance;

        private void Awake()
        {
            EnsureGameSystemRoot();
        }

        private void Start()
        {
            if (autoInitOnStart)
            {
                Bootstrap();
            }
        }

        private void Bootstrap()
        {
            Debug.Log("[GameBootstrapper] Starting Game Systems...");

            GameObject root = EnsureGameSystemRoot();
            var gm = EnsureManagerFromPrefab(root, gameManagerPrefab, "GameManager", ref _gameManagerInstance);
            EnsureManagerFromPrefab(root, uiManagerPrefab, "UIManager", ref _uiManagerInstance);
            EnsureManagerFromPrefab(root, boardGameManagerPrefab, "BoardGameManager", ref _boardGameManagerInstance);
            EnsureManagerFromPrefab(root, inventoryManagerPrefab, "InventoryManager", ref _inventoryManagerInstance);
            EnsureManagerFromPrefab(root, cameraManagerPrefab, "CameraManager", ref _cameraManagerInstance);
            EnsureManagerFromPrefab(root, sceneLoaderPrefab, "SceneLoader", ref _sceneLoaderInstance);
            EnsureManagerFromPrefab(root, boardMapManagerPrefab, "BoardMapManager", ref _boardMapManagerInstance);
            EnsureManagerFromPrefab(root, boardEntityManagerPrefab, "BoardEntityManager", ref _boardEntityManagerInstance);

            // 2. 可以在这里查找场景里的其他依赖
            // var ui = FindObjectOfType<UIManager>();
            // ui.Init();

            // 3. 正式启动游戏逻辑
            if (gm != null)
            {
                bool wasInitialized = gm.IsInitialized;
                if (playerPrefab != null)
                {
                    gm.SetPlayerPrefab(playerPrefab);
                }
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
            GameObject root = GameObject.Find("[GameSystem]");
            if (root != null) return root;

            root = new GameObject("[GameSystem]");
            root.AddComponent<DontDestroyRoot>();
            return root;
        }

        private T EnsureManagerFromPrefab<T>(GameObject root, GameObject prefab, string fallbackName, ref T instance)
            where T : MonoBehaviour
        {
            if (instance != null) return instance;

            T existing = root.GetComponentInChildren<T>(true);
            if (existing != null)
            {
                instance = existing;
                return instance;
            }

            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, root.transform);
            }
            else
            {
                Debug.LogWarning($"[GameBootstrapper] Missing manager prefab for {fallbackName}, creating empty GameObject.");
                go = new GameObject(fallbackName);
                go.transform.SetParent(root.transform, false);
            }

            instance = go.GetComponent<T>();
            if (instance == null)
            {
                instance = go.AddComponent<T>();
            }
            return instance;
        }
    }
}
