using UnityEngine;
using IndieGame.UI;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Inventory;

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

            GameObject root = EnsureGameRoot();
            var gm = EnsureManager<GameManager>(root, "GameManager");
            EnsureManager<UIManager>(root, "UIManager");
            EnsureManager<BoardGameManager>(root, "BoardGameManager");
            EnsureManager<InventoryManager>(root, "InventoryManager");

            // 2. 可以在这里查找场景里的其他依赖
            // var ui = FindObjectOfType<UIManager>();
            // ui.Init();

            // 3. 正式启动游戏逻辑
            if (gm != null)
            {
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

        private GameObject EnsureGameRoot()
        {
            GameObject root = GameObject.Find("GameRoot");
            if (root != null) return root;

            root = new GameObject("GameRoot");
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
    }
}
