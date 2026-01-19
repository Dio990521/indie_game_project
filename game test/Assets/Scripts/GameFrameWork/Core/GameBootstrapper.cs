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
        [SerializeField] private GameObject playerPrefab;

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
            var gm = EnsureManagerFromPrefab<GameManager>(root, gameManagerPrefab, "GameManager");
            EnsureManagerFromPrefab<UIManager>(root, uiManagerPrefab, "UIManager");
            EnsureManagerFromPrefab<BoardGameManager>(root, boardGameManagerPrefab, "BoardGameManager");
            EnsureManagerFromPrefab<InventoryManager>(root, inventoryManagerPrefab, "InventoryManager");
            EnsureManagerFromPrefab<CameraManager>(root, cameraManagerPrefab, "CameraManager");

            // 2. 可以在这里查找场景里的其他依赖
            // var ui = FindObjectOfType<UIManager>();
            // ui.Init();

            // 3. 正式启动游戏逻辑
            if (gm != null)
            {
                if (playerPrefab != null)
                {
                    gm.SetPlayerPrefab(playerPrefab);
                }
                gm.InitGame();
                
                // 如果是测试场景，可能会强制覆盖状态
                if (isTestScene)
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

        private T EnsureManager<T>(GameObject root, string name) where T : MonoBehaviour
        {
            T instance = FindAnyObjectByType<T>();
            if (instance != null)
            {
                if (instance.transform.parent != root.transform)
                {
                    instance.transform.SetParent(root.transform, false);
                }
                return instance;
            }

            GameObject go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            return go.AddComponent<T>();
        }

        private T EnsureManagerFromPrefab<T>(GameObject root, GameObject prefab, string fallbackName) where T : MonoBehaviour
        {
            T instance = FindAnyObjectByType<T>();
            if (instance != null)
            {
                if (instance.transform.parent != root.transform)
                {
                    instance.transform.SetParent(root.transform, false);
                }
                return instance;
            }

            if (prefab == null)
            {
                Debug.LogWarning($"[GameBootstrapper] Missing manager prefab for {fallbackName}, falling back to empty GameObject.");
                return EnsureManager<T>(root, fallbackName);
            }

            GameObject go = Instantiate(prefab, root.transform);
            T comp = go.GetComponent<T>();
            if (comp == null)
            {
                Debug.LogWarning($"[GameBootstrapper] Prefab {fallbackName} has no {typeof(T).Name}, falling back.");
                Destroy(go);
                return EnsureManager<T>(root, fallbackName);
            }
            return comp;
        }
    }
}
