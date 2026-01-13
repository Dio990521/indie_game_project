using UnityEngine;

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

            // 1. 确保核心单例存在 (因为 GameManager 是懒加载的，访问 Instance 就会创建)
            var gm = GameManager.Instance;

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
    }
}