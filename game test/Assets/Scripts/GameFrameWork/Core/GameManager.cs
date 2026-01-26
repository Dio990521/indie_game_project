using UnityEngine;
using IndieGame.Core.Utilities;
using IndieGame.Core.CameraSystem;

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
            
            // 1. 这里可以加载配置表、读取存档、初始化音频系统等
            // StartCoroutine(LoadAssetsRoutine()); 
            
            IsInitialized = true;
            
            // 2. 初始化完成后，进入第一个逻辑状态
            // 如果有主菜单场景，这里应该切到 MainMenu
            // 对于 Demo，我们直接进 FreeRoam
            EnsurePlayer();
            ChangeState(GameState.FreeRoam);
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
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.SetFollowTarget(player.transform);
                CameraManager.Instance.WarpCameraToTarget();
            }
        }
        
    }
}
