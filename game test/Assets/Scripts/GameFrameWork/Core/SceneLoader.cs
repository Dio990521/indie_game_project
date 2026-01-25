using UnityEngine;
using UnityEngine.SceneManagement;
using IndieGame.Core.Utilities;

namespace IndieGame.Core
{
    public class SceneLoader : MonoSingleton<SceneLoader>
    {
        [Header("Board Scene")]
        [SerializeField] private string boardSceneName = "World";
        [SerializeField] private SceneRegistrySO sceneRegistry;

        // 用于跨场景传递的临时数据
        private struct TransitionPayload
        {
            public string SceneName;
            public LocationID TargetLocation;
            public int WaypointIndex;
            public bool ReturnToBoard;
        }

        private bool _hasPayload;
        private TransitionPayload _payload;
        private int _lastBoardNodeIndex = -1; // 记录上次离开棋盘时的节点
        private string _lastBoardSceneName; // 记录上次棋盘场景名

        public bool HasPayload => _hasPayload;
        public bool IsReturnToBoard => _hasPayload && _payload.ReturnToBoard;
        public LocationID TargetLocationId => _payload.TargetLocation;
        public int TargetWaypointIndex => _payload.WaypointIndex;
        public string TargetSceneName => _payload.SceneName;
        public int GetSavedBoardIndex() => _lastBoardNodeIndex;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Start()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameMode mode = sceneRegistry != null ? sceneRegistry.GetGameMode(scene.name) : GameMode.Exploration;
            EventBus.Raise(new GameModeChangedEvent
            {
                SceneName = scene.name,
                Mode = mode
            });
        }

        public void LoadScene(string sceneName, LocationID targetID)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            // 进入探索场景前，缓存棋盘位置
            CacheBoardNodeIndex();
            _payload = new TransitionPayload
            {
                SceneName = sceneName,
                TargetLocation = targetID,
                WaypointIndex = -1,
                ReturnToBoard = false
            };
            _hasPayload = true;

            SceneManager.LoadSceneAsync(sceneName);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameState.FreeRoam);
            }
        }

        public void ReturnToBoard()
        {
            if (_lastBoardNodeIndex < 0)
            {
                Debug.LogWarning("[SceneLoader] Saved board node index is invalid, will fallback to start node.");
            }
            _payload = new TransitionPayload
            {
                SceneName = string.IsNullOrEmpty(_lastBoardSceneName) ? boardSceneName : _lastBoardSceneName,
                TargetLocation = null,
                WaypointIndex = _lastBoardNodeIndex,
                ReturnToBoard = true
            };
            _hasPayload = true;

            SceneManager.LoadSceneAsync(_payload.SceneName);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameState.BoardMode);
            }
        }

        public void ClearPayload()
        {
            _hasPayload = false;
            _payload = default;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            GameMode modeResult = sceneRegistry != null
                ? sceneRegistry.GetGameMode(scene.name)
                : GameMode.Exploration;
            EventBus.Raise(new GameModeChangedEvent
            {
                SceneName = scene.name,
                Mode = modeResult
            });

            if (!_hasPayload) return;
            EventBus.Raise(new SceneTransitionEvent
            {
                SceneName = scene.name,
                TargetLocation = _payload.TargetLocation,
                WaypointIndex = _payload.WaypointIndex,
                ReturnToBoard = _payload.ReturnToBoard
            });
        }

        private void CacheBoardNodeIndex()
        {
            if (sceneRegistry != null)
            {
                string currentScene = SceneManager.GetActiveScene().name;
                if (sceneRegistry.GetGameMode(currentScene) == GameMode.Board)
                {
                    _lastBoardSceneName = currentScene;
                }
            }
            var board = Gameplay.Board.Runtime.BoardGameManager.Instance;
            if (board == null || board.movementController == null) return;
            _lastBoardNodeIndex = board.movementController.CurrentNodeId;
        }
    }
}
