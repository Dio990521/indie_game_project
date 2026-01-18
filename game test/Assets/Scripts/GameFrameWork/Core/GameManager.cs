using UnityEngine;
using System;
using System.Collections;
using IndieGame.Core.Utilities;
using UnityEngine.SceneManagement;
using IndieGame.Core.CameraSystem;

namespace IndieGame.Core
{
    public class GameManager : MonoSingleton<GameManager>
    {
        protected override bool DestroyOnLoad => true;
        public GameState CurrentState { get; private set; } = GameState.Initialization;
        public static event Action<GameState> OnStateChanged;

        // 是否已初始化
        public bool IsInitialized { get; private set; } = false;
        public int LastBoardIndex { get; set; } = -1;

        private GameObject playerPrefab;

        public GameObject CurrentPlayer { get; private set; }

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
            OnStateChanged?.Invoke(newState);
        }

        public void LoadScene(string sceneName, GameState newState)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            StartCoroutine(LoadSceneRoutine(sceneName, newState));
        }

        private IEnumerator LoadSceneRoutine(string sceneName, GameState newState)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            while (!op.isDone) yield return null;
            ChangeState(newState);
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
