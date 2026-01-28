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
        private bool _isInitialized;
        private Scene _boardScene;
        private string _currentExplorationScene;

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

        // 由 GameManager 按顺序调用，避免各系统抢跑。
        public void Init()
        {
            if (_isInitialized) return;
            Scene scene = SceneManager.GetActiveScene();
            GameMode mode = GetModeForScene(scene.name);
            if (mode == GameMode.Board)
            {
                _boardScene = scene;
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ChangeState(GameState.BoardMode);
                }
            }
            EventBus.Raise(new GameModeChangedEvent
            {
                SceneName = scene.name,
                Mode = mode
            });
            _isInitialized = true;
        }

        public AsyncOperation LoadScene(string sceneName, LocationID targetID)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            // 进入探索场景前，缓存棋盘位置
            CacheBoardNodeIndex();
            GameMode targetMode = GetModeForScene(sceneName);
            _payload = new TransitionPayload
            {
                SceneName = sceneName,
                TargetLocation = targetID,
                WaypointIndex = -1,
                ReturnToBoard = false
            };
            _hasPayload = true;

            if (targetMode == GameMode.Menu)
            {
                _currentExplorationScene = null;
                _boardScene = default;
                return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }

            if (targetMode == GameMode.Board)
            {
                return LoadBoardScene(sceneName);
            }

            return LoadExplorationScene(sceneName);
        }

        public void ReturnToBoard()
        {
            if (_lastBoardNodeIndex < 0)
            {
                Debug.LogWarning("[SceneLoader] Saved board node index is invalid, will fallback to start node.");
            }
            _payload = new TransitionPayload
            {
                SceneName = GetBoardSceneName(),
                TargetLocation = null,
                WaypointIndex = _lastBoardNodeIndex,
                ReturnToBoard = true
            };
            _hasPayload = true;

            LoadBoardScene(_payload.SceneName);
        }

        public void ClearPayload()
        {
            _hasPayload = false;
            _payload = default;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            GameMode modeResult = GetModeForScene(scene.name);
            if (modeResult == GameMode.Board)
            {
                _boardScene = scene;
            }
            else if (modeResult == GameMode.Exploration)
            {
                _currentExplorationScene = scene.name;
            }
            else if (modeResult == GameMode.Menu)
            {
                _currentExplorationScene = null;
                _boardScene = default;
            }
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

        private AsyncOperation LoadBoardScene(string sceneName)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(_currentExplorationScene))
            {
                Scene exploration = SceneManager.GetSceneByName(_currentExplorationScene);
                if (exploration.IsValid() && exploration.isLoaded)
                {
                    AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(exploration);
                    if (unloadOp != null)
                    {
                        unloadOp.completed += _ =>
                        {
                            _currentExplorationScene = null;
                            SetBoardSceneRootsActive(true);
                            ActivateBoardScene();
                            RaiseBoardModeChanged();
                        };
                    }
                    return unloadOp;
                }
                _currentExplorationScene = null;
            }

            if (IsBoardScene(activeScene))
            {
                SetBoardSceneRootsActive(true);
                RaiseBoardModeChanged();
                return null;
            }

            return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }

        private AsyncOperation LoadExplorationScene(string sceneName)
        {
            GameMode activeMode = GetModeForScene(SceneManager.GetActiveScene().name);
            if (activeMode == GameMode.Menu)
            {
                AsyncOperation loadBoardOp = SceneManager.LoadSceneAsync(GetBoardSceneName(), LoadSceneMode.Single);
                if (loadBoardOp != null)
                {
                    loadBoardOp.completed += _ =>
                    {
                        _boardScene = SceneManager.GetSceneByName(GetBoardSceneName());
                        SetBoardSceneRootsActive(false);
                        LoadExplorationAdditive(sceneName);
                    };
                }
                return loadBoardOp;
            }

            if (activeMode == GameMode.Exploration && !string.IsNullOrEmpty(_currentExplorationScene))
            {
                Scene currentExploration = SceneManager.GetSceneByName(_currentExplorationScene);
                if (currentExploration.IsValid() && currentExploration.isLoaded && _currentExplorationScene != sceneName)
                {
                    AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(currentExploration);
                    if (unloadOp != null)
                    {
                        unloadOp.completed += _ => LoadExplorationAdditive(sceneName);
                    }
                    return unloadOp;
                }
            }

            if (IsBoardScene(SceneManager.GetActiveScene()))
            {
                SetBoardSceneRootsActive(false);
            }

            return LoadExplorationAdditive(sceneName);
        }

        private AsyncOperation LoadExplorationAdditive(string sceneName)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op != null)
            {
                op.completed += _ =>
                {
                    Scene loadedScene = SceneManager.GetSceneByName(sceneName);
                    if (loadedScene.IsValid() && loadedScene.isLoaded)
                    {
                        SceneManager.SetActiveScene(loadedScene);
                        _currentExplorationScene = sceneName;
                    }
                    else
                    {
                        Debug.LogWarning($"[SceneLoader] Additive scene '{sceneName}' did not load correctly.");
                    }
                };
            }
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameState.FreeRoam);
            }
            return op;
        }

        private string GetBoardSceneName()
        {
            return string.IsNullOrEmpty(_lastBoardSceneName) ? boardSceneName : _lastBoardSceneName;
        }

        private Scene GetBoardScene()
        {
            if (_boardScene.IsValid() && _boardScene.isLoaded) return _boardScene;
            return SceneManager.GetSceneByName(GetBoardSceneName());
        }

        private bool IsBoardScene(Scene scene)
        {
            if (!scene.IsValid()) return false;
            if (sceneRegistry != null)
            {
                return sceneRegistry.GetGameMode(scene.name) == GameMode.Board;
            }
            return scene.name == GetBoardSceneName();
        }

        private void ActivateBoardScene()
        {
            Scene boardScene = GetBoardScene();
            if (boardScene.IsValid() && boardScene.isLoaded)
            {
                SceneManager.SetActiveScene(boardScene);
            }
            else
            {
                Debug.LogWarning($"[SceneLoader] Board scene '{GetBoardSceneName()}' is not loaded.");
            }
        }

        private void SetBoardSceneRootsActive(bool active)
        {
            Scene boardScene = GetBoardScene();
            if (!boardScene.IsValid() || !boardScene.isLoaded) return;
            GameObject[] roots = boardScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null) continue;
                if (root.scene.name == "DontDestroyOnLoad") continue;
                root.SetActive(active);
            }
        }

        private void RaiseBoardModeChanged()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameState.BoardMode);
            }
            Scene boardScene = GetBoardScene();
            EventBus.Raise(new GameModeChangedEvent
            {
                SceneName = boardScene.IsValid() ? boardScene.name : GetBoardSceneName(),
                Mode = GameMode.Board
            });
        }

        private GameMode GetModeForScene(string sceneName)
        {
            return sceneRegistry != null ? sceneRegistry.GetGameMode(sceneName) : GameMode.Exploration;
        }
    }
}
