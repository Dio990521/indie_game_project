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

        [Header("Player Spawn")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private string playerTag = "Player";
        private const string PlayerResourcePath = "Prefabs/Player";

        public GameObject CurrentPlayer { get; private set; }

        public void SetPlayerPrefab(GameObject prefab)
        {
            if (prefab == null) return;
            playerPrefab = prefab;
        }

        public Transform GetCurrentPlayerTransform()
        {
            GameObject player = GetOrFindCurrentPlayer();
            return player != null ? player.transform : null;
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
            ChangeState(GameState.FreeRoam);
            EnsurePlayer();
        }

        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState) return;
            
            // 状态退出逻辑 (可选)
            // if (CurrentState == GameState.BoardMode) { ... }

            CurrentState = newState;
            Debug.Log($"[GameManager] State Changed to: {newState}");
            OnStateChanged?.Invoke(newState);
            EnsurePlayer();
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
            EnsurePlayer();
        }

        private void EnsurePlayer()
        {
            if (CurrentPlayer != null && !CurrentPlayer.Equals(null)) return;

            GameObject existing = FindPlayerInScene();
            if (existing != null)
            {
                CurrentPlayer = existing;
                DontDestroyOnLoad(existing);
                return;
            }

            GameObject prefab = playerPrefab != null
                ? playerPrefab
                : Resources.Load<GameObject>(PlayerResourcePath);
            if (prefab == null) return;

            GameObject player = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            CurrentPlayer = player;
            DontDestroyOnLoad(player);
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.SetFollowTarget(player.transform);
                CameraManager.Instance.WarpCameraToTarget();
            }
        }

        private GameObject FindPlayerInScene()
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player == null && CurrentPlayer != null && !CurrentPlayer.Equals(null))
            {
                player = CurrentPlayer;
            }
            return player;
        }

        public GameObject GetOrFindCurrentPlayer()
        {
            if (CurrentPlayer != null && !CurrentPlayer.Equals(null)) return CurrentPlayer;
            GameObject player = FindPlayerInScene();
            if (player != null)
            {
                CurrentPlayer = player;
                DontDestroyOnLoad(player);
            }
            return CurrentPlayer;
        }
        
        // 移除原来的 Start() 中的 ChangeState 调用，交给 Bootstrapper
    }
}
