using UnityEngine;
using UnityEngine.SceneManagement;
using IndieGame.Core.Utilities;

namespace IndieGame.Core
{
    /// <summary>
    /// 场景加载器（单例）：
    /// 负责根据 SceneRegistry 的场景模式，执行“主菜单 / 棋盘 / 探索”的加载策略，
    /// 同时维护跨场景的临时载荷（出生点、返回棋盘标记等）。
    /// </summary>
    public class SceneLoader : MonoSingleton<SceneLoader>
    {
        [Header("Board Scene")]
        // 棋盘场景名称（默认 World）
        [SerializeField] private string boardSceneName = "World";
        // 场景注册表，用于获取场景类型
        [SerializeField] private SceneRegistrySO sceneRegistry;

        // 用于跨场景传递的临时数据
        private struct TransitionPayload
        {
            // 目标场景名
            public string SceneName;
            // 目标出生点
            public LocationID TargetLocation;
            // 棋盘返回时的节点索引
            public int WaypointIndex;
            // 是否为“返回棋盘”
            public bool ReturnToBoard;
        }

        // 是否有有效载荷
        private bool _hasPayload;
        // 当前载荷内容
        private TransitionPayload _payload;
        private int _lastBoardNodeIndex = -1; // 记录上次离开棋盘时的节点
        private string _lastBoardSceneName; // 记录上次棋盘场景名
        private bool _isInitialized;
        // 缓存棋盘场景引用（用于常驻场景架构）
        private Scene _boardScene;
        // 当前叠加的探索场景名
        private string _currentExplorationScene;

        // --- 载荷读取接口 ---
        public bool HasPayload => _hasPayload;
        public bool IsReturnToBoard => _hasPayload && _payload.ReturnToBoard;
        public LocationID TargetLocationId => _payload.TargetLocation;
        public int TargetWaypointIndex => _payload.WaypointIndex;
        public string TargetSceneName => _payload.SceneName;
        public int GetSavedBoardIndex() => _lastBoardNodeIndex;

        private void OnEnable()
        {
            // 监听场景加载完成事件
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            // 退订事件，避免重复触发
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
            // 广播当前场景模式，驱动系统初始化
            EventBus.Raise(new GameModeChangedEvent
            {
                SceneName = scene.name,
                Mode = mode
            });
            _isInitialized = true;
        }

        /// <summary>
        /// 通用场景加载入口：
        /// 根据目标场景的 GameMode 决定 Single / Additive / 先加载棋盘再叠加等策略。
        /// </summary>
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
                // 菜单场景：完全切换，清理叠加状态
                _currentExplorationScene = null;
                _boardScene = default;
                return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }

            if (targetMode == GameMode.Board)
            {
                // 目标为棋盘：走“回棋盘”逻辑
                return LoadBoardScene(sceneName);
            }

            if (targetMode == GameMode.Camp)
            {
                // 露营场景：视为 Additive 叠加场景处理
                return LoadExplorationScene(sceneName);
            }

            // 目标为探索：走“棋盘常驻 + 叠加探索”逻辑
            return LoadExplorationScene(sceneName);
        }

        /// <summary>
        /// 返回棋盘入口：
        /// 不销毁棋盘场景，仅卸载探索场景并恢复棋盘根物体。
        /// </summary>
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

        /// <summary>
        /// 清空跨场景载荷。
        /// </summary>
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
                // 进入棋盘场景时确保玩家可见
                if (GameManager.Instance != null && GameManager.Instance.CurrentPlayer != null)
                {
                    GameManager.Instance.CurrentPlayer.SetActive(true);
                }
            }
            else if (modeResult == GameMode.Exploration || modeResult == GameMode.Camp)
            {
                _currentExplorationScene = scene.name;
            }
            else if (modeResult == GameMode.Menu)
            {
                _currentExplorationScene = null;
                _boardScene = default;
            }
            if (modeResult == GameMode.Camp && GameManager.Instance != null && GameManager.Instance.CurrentPlayer != null)
            {
                // 进入露营场景时隐藏玩家（避免与 Camp UI/场景冲突）
                GameManager.Instance.CurrentPlayer.SetActive(false);
            }
            // 广播场景模式变化
            EventBus.Raise(new GameModeChangedEvent
            {
                SceneName = scene.name,
                Mode = modeResult
            });

            // 若无载荷，不触发场景过渡事件
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
            // 从棋盘控制器读取当前节点，保存以便返回时复位
            var board = Gameplay.Board.Runtime.BoardGameManager.Instance;
            if (board == null || board.movementController == null) return;
            _lastBoardNodeIndex = board.movementController.CurrentNodeId;
        }

        /// <summary>
        /// 加载/恢复棋盘场景：
        /// - 若当前叠加了探索，则先卸载探索，再显示棋盘
        /// - 若已经在棋盘，则仅恢复根物体
        /// - 否则 Single 方式切换到棋盘
        /// </summary>
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
                            // 卸载完成后恢复棋盘显示并激活
                            _currentExplorationScene = null;
                            SetBoardSceneRootsActive(true);
                            // 返回棋盘时重新显示玩家
                            if (GameManager.Instance != null && GameManager.Instance.CurrentPlayer != null)
                            {
                                GameManager.Instance.CurrentPlayer.SetActive(true);
                            }
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
                // 已经在棋盘场景，只需恢复根物体
                SetBoardSceneRootsActive(true);
                // 返回棋盘时重新显示玩家
                if (GameManager.Instance != null && GameManager.Instance.CurrentPlayer != null)
                {
                    GameManager.Instance.CurrentPlayer.SetActive(true);
                }
                RaiseBoardModeChanged();
                return null;
            }

            // 从菜单等场景进入棋盘，直接 Single 加载
            return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }

        /// <summary>
        /// 加载探索场景：
        /// - 若当前是菜单：先 Single 加载棋盘，再 Additive 叠加探索
        /// - 若当前是探索：先卸载旧探索，再叠加新探索
        /// - 若当前是棋盘：隐藏棋盘根物体，再叠加探索
        /// </summary>
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
                        // 先让棋盘常驻，再叠加探索
                        _boardScene = SceneManager.GetSceneByName(GetBoardSceneName());
                        SetBoardSceneRootsActive(false);
                        LoadExplorationAdditive(sceneName);
                    };
                }
                return loadBoardOp;
            }

            if ((activeMode == GameMode.Exploration || activeMode == GameMode.Camp) && !string.IsNullOrEmpty(_currentExplorationScene))
            {
                Scene currentExploration = SceneManager.GetSceneByName(_currentExplorationScene);
                if (currentExploration.IsValid() && currentExploration.isLoaded && _currentExplorationScene != sceneName)
                {
                    AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(currentExploration);
                    if (unloadOp != null)
                    {
                        // 卸载旧探索后再加载新探索
                        unloadOp.completed += _ => LoadExplorationAdditive(sceneName);
                    }
                    return unloadOp;
                }
            }

            if (IsBoardScene(SceneManager.GetActiveScene()))
            {
                // 从棋盘进入探索时先隐藏棋盘根物体
                SetBoardSceneRootsActive(false);
            }

            return LoadExplorationAdditive(sceneName);
        }

        /// <summary>
        /// 以 Additive 方式叠加探索场景，并设置为 ActiveScene。
        /// </summary>
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
                        // 切换活动场景，确保灯光/摄像机等生效
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
                // 进入探索时同步游戏状态
                GameManager.Instance.ChangeState(GameState.FreeRoam);
            }
            return op;
        }

        /// <summary>
        /// 获取棋盘场景名（优先使用最后记录的棋盘场景）。
        /// </summary>
        private string GetBoardSceneName()
        {
            return string.IsNullOrEmpty(_lastBoardSceneName) ? boardSceneName : _lastBoardSceneName;
        }

        /// <summary>
        /// 获取棋盘 Scene 对象（优先使用缓存）。
        /// </summary>
        private Scene GetBoardScene()
        {
            if (_boardScene.IsValid() && _boardScene.isLoaded) return _boardScene;
            return SceneManager.GetSceneByName(GetBoardSceneName());
        }

        /// <summary>
        /// 判断给定场景是否为棋盘场景。
        /// </summary>
        private bool IsBoardScene(Scene scene)
        {
            if (!scene.IsValid()) return false;
            if (sceneRegistry != null)
            {
                return sceneRegistry.GetGameMode(scene.name) == GameMode.Board;
            }
            return scene.name == GetBoardSceneName();
        }

        /// <summary>
        /// 将棋盘场景设为 ActiveScene，确保其光照/相机生效。
        /// </summary>
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

        /// <summary>
        /// 切换棋盘场景根物体的激活状态：
        /// 用于“隐藏棋盘但保留状态”。
        /// </summary>
        private void SetBoardSceneRootsActive(bool active)
        {
            Scene boardScene = GetBoardScene();
            if (!boardScene.IsValid() || !boardScene.isLoaded) return;
            GameObject[] roots = boardScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null) continue;
                // 不影响 DontDestroyOnLoad 根节点上的全局单例
                if (root.scene.name == "DontDestroyOnLoad") continue;
                root.SetActive(active);
            }
        }

        /// <summary>
        /// 广播“棋盘模式”并同步 GameManager 状态。
        /// </summary>
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

        /// <summary>
        /// 获取场景模式（若未配置则默认 Exploration）。
        /// </summary>
        private GameMode GetModeForScene(string sceneName)
        {
            return sceneRegistry != null ? sceneRegistry.GetGameMode(sceneName) : GameMode.Exploration;
        }
    }
}
