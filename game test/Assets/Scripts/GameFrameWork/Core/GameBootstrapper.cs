using IndieGame.Core.Utilities;
using UnityEngine;
using IndieGame.UI;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.Economy;
using IndieGame.Gameplay.Shop;
using IndieGame.Gameplay.ActionPoint;
using IndieGame.Core.CameraSystem;
using IndieGame.Gameplay.Dialogue;
using IndieGame.Gameplay.Town;
using IndieGame.Gameplay.Date;
using IndieGame.Core;

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
        [SerializeField] private GameObject shopSystemPrefab;
        [SerializeField] private GameObject actionPointSystemPrefab;
        [SerializeField] private GameObject dialogueManagerPrefab;
        [SerializeField] private GameObject townUnlockManagerPrefab;
        [SerializeField] private GameObject dateSystemPrefab;
        [SerializeField] private GameObject gameFlagSystemPrefab;
        // 玩家预制体（交由 GameManager 实例化）
        [SerializeField] private GameObject playerPrefab;

        // 注：原本这里缓存了 13 个 _xxxManagerInstance 字段，但除了 GameManager（用于后续 InitGame）
        // 外，其余 12 个字段在 Bootstrap 之外没有任何使用，纯粹是 ref 参数占位。
        // P2-3 重构后改为：EnsureManagerFromPrefab 直接返回实例，Bootstrap 仅缓存真正需要的 gm，
        // 其余系统单例由各自的 MonoSingleton.Instance 提供访问。

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
            DebugTools.Log("[GameBootstrapper] Starting Game Systems...");

            // 1) 统一确保所有管理器在同一根节点下生成。
            //    GameManager 是唯一需要后续使用的实例（InitGame、状态切换等），
            //    其余 Manager 只需确保存在即可，访问由各自的 .Instance 提供。
            GameObject root = EnsureGameSystemRoot();
            GameManager gm = EnsureManagerFromPrefab<GameManager>(root, gameManagerPrefab, "GameManager");
            EnsureManagerFromPrefab<UIManager>(root, uiManagerPrefab, "UIManager");
            EnsureManagerFromPrefab<BoardGameManager>(root, boardGameManagerPrefab, "BoardGameManager");
            EnsureManagerFromPrefab<InventoryManager>(root, inventoryManagerPrefab, "InventoryManager");
            EnsureManagerFromPrefab<CameraManager>(root, cameraManagerPrefab, "CameraManager");
            EnsureManagerFromPrefab<SceneLoader>(root, sceneLoaderPrefab, "SceneLoader");
            EnsureManagerFromPrefab<BoardMapManager>(root, boardMapManagerPrefab, "BoardMapManager");
            EnsureManagerFromPrefab<BoardEntityManager>(root, boardEntityManagerPrefab, "BoardEntityManager");
            // 金币系统：常驻并参与存档，承载所有金币变动事件。
            EnsureManagerFromPrefab<GoldSystem>(root, goldSystemPrefab, "GoldSystem");
            // 商店系统：库存/限购动态状态与交易规则，常驻并参与存档。
            EnsureManagerFromPrefab<ShopSystem>(root, shopSystemPrefab, "ShopSystem");
            // 行动点系统：控制每回合可投掷骰子次数，支持存档与培养提升上限。
            EnsureManagerFromPrefab<ActionPointSystem>(root, actionPointSystemPrefab, "ActionPointSystem");
            // 对话系统：管理打字机效果、词条解析与对话状态切换。
            EnsureManagerFromPrefab<DialogueManager>(root, dialogueManagerPrefab, "DialogueManager");
            // 城镇解锁管理：追踪已解锁城镇节点，支持传送菜单与存档。
            EnsureManagerFromPrefab<TownUnlockManager>(root, townUnlockManagerPrefab, "TownUnlockManager");
            // 日期系统：追踪游戏内日期，每次 Sleep/Inn 推进一天，支持存档。
            EnsureManagerFromPrefab<DateSystem>(root, dateSystemPrefab, "DateSystem");
            // 全局事件标志数据库：存储任务开关、门是否开启等布尔状态，供障碍物和任务系统使用，支持存档。
            // 注：需在其他读写 Flag 的系统之前初始化，故放在列表末尾（Bootstrap 顺序即初始化顺序）。
            EnsureManagerFromPrefab<GameFlagSystem>(root, gameFlagSystemPrefab, "GameFlagSystem");

            // 2. 正式启动游戏逻辑
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
                    if (gm.CurrentState != GameState.FreeRoam)
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

        /// <summary>
        /// 统一的"确保某个 Manager 存在"入口：
        /// 1) 若根节点下已有该组件，直接复用；
        /// 2) 若提供了预制体，则实例化预制体；
        /// 3) 若预制体缺失，创建空 GameObject 并 AddComponent 作为兜底；
        /// 4) 兜底创建会打 Warning，提醒在 Inspector 配置预制体。
        /// </summary>
        private T EnsureManagerFromPrefab<T>(GameObject root, GameObject prefab, string fallbackName)
            where T : MonoBehaviour
        {
            // 优先查找根节点下已有组件（避免重复创建）
            T existing = root.GetComponentInChildren<T>(true);
            if (existing != null)
            {
                return existing;
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
                DebugTools.LogWarning($"[GameBootstrapper] Missing manager prefab for {fallbackName}, creating empty GameObject.");
                go = new GameObject(fallbackName);
                go.transform.SetParent(root.transform, false);
            }

            T instance = go.GetComponent<T>();
            if (instance == null)
            {
                // 预制体上没有目标组件时，动态添加
                instance = go.AddComponent<T>();
            }
            return instance;
        }
    }
}
