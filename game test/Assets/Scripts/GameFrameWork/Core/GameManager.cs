using UnityEngine;
using IndieGame.Core.Utilities;
using IndieGame.Core.CameraSystem;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Inventory;
using IndieGame.UI;

namespace IndieGame.Core
{
    public class GameManager : MonoSingleton<GameManager>
    {
        protected override bool DestroyOnLoad => false;
        public GameState CurrentState { get; private set; } = GameState.Initialization;

        // 是否已初始化
        public bool IsInitialized { get; private set; } = false;

        private GameObject playerPrefab;

        public GameObject CurrentPlayer { get; private set; }

        private void OnEnable()
        {
            EventBus.Subscribe<GameModeChangedEvent>(OnGameModeChanged);
        }

        private void OnDisable()
        {
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

        public void SetPlayerPrefab(GameObject prefab)
        {
            playerPrefab = prefab;
        }

        /// <summary>
        /// 游戏唯一的启动入口
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

        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState) return;
            
            // 状态退出逻辑 (可选)
            // if (CurrentState == GameState.BoardMode) { ... }

            CurrentState = newState;
            Debug.Log($"[GameManager] State Changed to: {newState}");
            EventBus.Raise(new GameStateChangedEvent { NewState = newState });
        }

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
            DontDestroyOnLoad(player);
        }
        
    }
}
