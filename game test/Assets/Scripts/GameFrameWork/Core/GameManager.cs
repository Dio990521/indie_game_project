using UnityEngine;
using IndieGame.Core.Utilities;
using IndieGame.Core.CameraSystem;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Inventory;
using IndieGame.UI;

namespace IndieGame.Core
{
    /// <summary>
    /// 游戏核心管理器（单例）：
    /// 负责全局初始化流程、玩家对象生成、游戏状态切换与核心系统联动。
    /// </summary>
    public class GameManager : MonoSingleton<GameManager>
    {
        // GameManager 作为全局管理器需常驻，不随场景切换销毁
        protected override bool DestroyOnLoad => false;
        // 当前游戏状态（由本类统一维护）
        public GameState CurrentState { get; private set; } = GameState.Initialization;

        // 是否已初始化
        public bool IsInitialized { get; private set; } = false;

        // 玩家预制体（由 GameBootstrapper 或外部设置）
        private GameObject playerPrefab;

        // 当前玩家实例（运行时生成）
        public GameObject CurrentPlayer { get; private set; }

        private void OnEnable()
        {
            // 监听场景模式变化，驱动 GameState
            EventBus.Subscribe<GameModeChangedEvent>(OnGameModeChanged);
        }

        private void OnDisable()
        {
            // 退订事件，避免生命周期结束后仍被触发
            EventBus.Unsubscribe<GameModeChangedEvent>(OnGameModeChanged);
        }

        private void OnGameModeChanged(GameModeChangedEvent evt)
        {
            // 根据事件传来的 Mode 切换状态
            if (evt.Mode == GameMode.Board)
            {
                ChangeState(GameState.BoardMode);
            }
            else
            {
                ChangeState(GameState.FreeRoam);
            }
        }

        /// <summary>
        /// 设置玩家预制体（通常由 GameBootstrapper 调用）。
        /// </summary>
        public void SetPlayerPrefab(GameObject prefab)
        {
            playerPrefab = prefab;
        }

        /// <summary>
        /// 游戏唯一的启动入口：
        /// 按顺序初始化各系统，确保依赖关系正确。
        /// </summary>
        public void InitGame()
        {
            if (IsInitialized) return;

            Debug.Log("<color=green>[GameManager] Game Initializing...</color>");

            // 1) SceneLoader: 先广播当前场景模式，清理状态。
            if (SceneLoader.Instance != null) SceneLoader.Instance.Init();

            // 2) BoardMapManager: 缓存地图数据，作为后续逻辑基础。
            if (BoardMapManager.Instance != null) BoardMapManager.Instance.Init();

            // 3) InventoryManager: 准备玩家数据。
            if (InventoryManager.Instance != null) InventoryManager.Instance.Init();

            // 4) UIManager: 生成并初始化 UI。
            if (UIManager.Instance != null) UIManager.Instance.Init();

            // 5) BoardGameManager: 玩家存在后再启动棋盘逻辑。
            EnsurePlayer();
            if (BoardGameManager.Instance != null) BoardGameManager.Instance.Init(true);

            // 6) CameraManager: 最后设置跟随目标。
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.Init();
                if (CurrentPlayer != null)
                {
                    CameraManager.Instance.SetFollowTarget(CurrentPlayer.transform);
                    CameraManager.Instance.WarpCameraToTarget();
                }
            }

            IsInitialized = true;
        }

        /// <summary>
        /// 切换游戏状态并广播事件。
        /// </summary>
        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState) return;
            
            // 状态退出逻辑 (可选)
            // if (CurrentState == GameState.BoardMode) { ... }

            CurrentState = newState;
            Debug.Log($"[GameManager] State Changed to: {newState}");
            EventBus.Raise(new GameStateChangedEvent { NewState = newState });
        }

        /// <summary>
        /// 确保玩家对象存在（首次进入时会实例化并设为常驻）。
        /// </summary>
        private void EnsurePlayer()
        {
            if (CurrentPlayer != null) return;
            if (playerPrefab == null)
            {
                Debug.LogError("[GameManager] playerPrefab is not assigned.");
                return;
            }

            GameObject player = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            CurrentPlayer = player;
            // 玩家常驻于所有场景
            DontDestroyOnLoad(player);
        }
        
    }
}
