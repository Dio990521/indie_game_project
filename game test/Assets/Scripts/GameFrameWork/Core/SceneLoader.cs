using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using IndieGame.Core.Utilities;
using IndieGame.UI;

namespace IndieGame.Core
{
    /// <summary>
    /// 场景加载器（单例，partial 主文件）：
    /// 负责根据 SceneRegistry 的场景模式，执行“主菜单 / 棋盘 / 探索”的加载策略，
    /// 同时维护跨场景的临时载荷（出生点、返回棋盘标记等）。
    ///
    /// 文件拆分（按场景策略）：
    /// - 本文件：字段/载荷、公开入口（LoadScene/ReturnToBoard/Raw）、转场协程与 Token 管理、场景模式查询；
    /// - <c>SceneLoader.Board.cs</c>：棋盘常驻策略（加载/恢复棋盘、根物体显隐、模式广播）；
    /// - <c>SceneLoader.Exploration.cs</c>：探索叠加策略（Additive 加载/卸载链）。
    /// </summary>
    public partial class SceneLoader : MonoSingleton<SceneLoader>
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
        // 转场协程的 Token（用于等待异步加载完成）
        private int _transitionToken;
        // 当前转场是否完成
        private bool _transitionCompleted;
        // 当前活跃的转场 Token
        private int _activeTransitionToken = -1;

        // 本类自启动的转场协程（LoadScene / ReturnToBoard），用于：
        // 1) 防止同一类 SceneLoader 自身发起的转场被并发启动；
        // 2) OnDisable 时强制停止，避免组件销毁后协程继续访问已销毁的 Manager。
        // 注意：外部直接 yield return ReturnToBoardRoutine 的协程不由本字段管理，
        // 调用方各自负责其生命周期（CampUIController / TownUIController 已做互斥保护）。
        private Coroutine _activeTransitionCoroutine;

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

            // 兜底：组件被禁用/销毁时若自启动的转场协程仍在跑，强制停止，
            // 防止协程在后续帧里访问已销毁的 GameManager / 其他单例触发 NRE。
            if (_activeTransitionCoroutine != null)
            {
                StopCoroutine(_activeTransitionCoroutine);
                _activeTransitionCoroutine = null;
            }
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
        public Coroutine LoadScene(string sceneName, LocationID targetID, float fadeDuration = 1f)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;

            // 互斥保护：若上一个自启动转场协程还没结束，先 Stop 再 Start，
            // 否则两个并行的 LoadSceneRoutine 会互相覆盖 _payload 与 _activeTransitionToken。
            if (_activeTransitionCoroutine != null)
            {
                DebugTools.LogWarning("[SceneLoader] 检测到上一次转场协程未结束，强制停止后启动新转场。");
                StopCoroutine(_activeTransitionCoroutine);
            }

            _activeTransitionCoroutine = StartCoroutine(LoadSceneRoutine(sceneName, targetID, fadeDuration));
            return _activeTransitionCoroutine;
        }

        /// <summary>
        /// 用于加载进度条的原始加载接口（不触发淡入淡出）。
        /// 注意：仅建议在 Title 场景加载主棋盘时使用。
        /// </summary>
        public AsyncOperation LoadSceneAsyncRaw(string sceneName, LocationID targetID)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            return LoadSceneInternal(sceneName, targetID, -1);
        }

        /// <summary>
        /// 返回棋盘入口：
        /// 不销毁棋盘场景，仅卸载探索场景并恢复棋盘根物体。
        /// </summary>
        public void ReturnToBoard()
        {
            // 互斥保护：与 LoadScene 共用 _activeTransitionCoroutine，
            // 既能防止重复点击"返回棋盘"，也能避免 LoadScene/ReturnToBoard 互相覆盖。
            if (_activeTransitionCoroutine != null)
            {
                DebugTools.LogWarning("[SceneLoader] 已有转场协程在运行，强制停止后启动 ReturnToBoard。");
                StopCoroutine(_activeTransitionCoroutine);
            }

            _activeTransitionCoroutine = StartCoroutine(ReturnToBoardRoutine(true, 1f, true));
        }

        /// <summary>
        /// 返回棋盘的协程流程：
        /// 可选择是否执行淡入淡出，便于外部自行控制（如 Sleep）。
        /// </summary>
        public IEnumerator ReturnToBoardRoutine(bool useFade, float fadeDuration = 1f, bool endLoadingWhenDone = true)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.BeginLoading();
            }

            if (_lastBoardNodeIndex < 0)
            {
                DebugTools.LogWarning("[SceneLoader] Saved board node index is invalid, will fallback to start node.");
            }
            _payload = new TransitionPayload
            {
                SceneName = GetBoardSceneName(),
                TargetLocation = null,
                WaypointIndex = _lastBoardNodeIndex,
                ReturnToBoard = true
            };
            _hasPayload = true;

            if (useFade)
            {
                EventBus.Raise(new FadeRequestedEvent { FadeIn = true, Duration = fadeDuration });
                // M2 修复：用 Realtime 等待，避免 timeScale=0（暂停）时黑屏流程永久卡住
                yield return new WaitForSecondsRealtime(fadeDuration);
            }

            int token = BeginTransition();
            LoadBoardScene(_payload.SceneName, token);

            while (!_transitionCompleted)
            {
                yield return null;
            }

            if (useFade)
            {
                EventBus.Raise(new FadeRequestedEvent { FadeIn = false, Duration = fadeDuration });
            }
            if (endLoadingWhenDone && GameManager.Instance != null)
            {
                GameManager.Instance.EndLoading();
            }
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
            else if (modeResult == GameMode.Title)
            {
                _currentExplorationScene = null;
                _boardScene = default;
            }
            if (modeResult == GameMode.Camp && GameManager.Instance != null && GameManager.Instance.CurrentPlayer != null)
            {
                // 进入露营场景时隐藏玩家（避免与 Camp UI/场景冲突）
                GameManager.Instance.CurrentPlayer.SetActive(false);
            }
            if (modeResult == GameMode.Camp && UIManager.Instance != null && UIManager.Instance.CampUIInstance != null)
            {
                // Camp 场景加载完成后显示露营 UI
                UIManager.Instance.CampUIInstance.Show();
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

        // 注：以下方法已按"场景策略"迁往同类 partial 文件：
        // - 棋盘策略（CacheBoardNodeIndex / LoadBoardScene / GetBoardSceneName / GetBoardScene /
        //   IsBoardScene / ActivateBoardScene / SetBoardSceneRootsActive / RaiseBoardModeChanged）
        //   → SceneLoader.Board.cs
        // - 探索策略（LoadExplorationScene / LoadExplorationAdditive）
        //   → SceneLoader.Exploration.cs

        /// <summary>
        /// 协程入口：场景加载 + 黑屏淡入淡出。
        /// </summary>
        private IEnumerator LoadSceneRoutine(string sceneName, LocationID targetID, float fadeDuration)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.BeginLoading();
            }

            EventBus.Raise(new FadeRequestedEvent { FadeIn = true, Duration = fadeDuration });
            // M2 修复：用 Realtime 等待，避免 timeScale=0（暂停）时黑屏流程永久卡住
            yield return new WaitForSecondsRealtime(fadeDuration);

            int token = BeginTransition();
            LoadSceneInternal(sceneName, targetID, token);

            while (!_transitionCompleted)
            {
                yield return null;
            }

            EventBus.Raise(new FadeRequestedEvent { FadeIn = false, Duration = fadeDuration });
            if (GameManager.Instance != null)
            {
                GameManager.Instance.EndLoading();
            }
        }

        /// <summary>
        /// 场景加载内部逻辑（不包含淡入淡出）。
        /// </summary>
        private AsyncOperation LoadSceneInternal(string sceneName, LocationID targetID, int transitionToken)
        {
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

            if (targetMode == GameMode.Title)
            {
                // 菜单场景：完全切换，清理叠加状态
                _currentExplorationScene = null;
                _boardScene = default;
                AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (op != null && transitionToken >= 0)
                {
                    op.completed += _ => CompleteTransition(transitionToken);
                }
                return op;
            }

            if (targetMode == GameMode.Board)
            {
                // 目标为棋盘：走“回棋盘”逻辑
                return LoadBoardScene(sceneName, transitionToken);
            }

            if (targetMode == GameMode.Camp)
            {
                // 露营场景：视为 Additive 叠加场景处理
                return LoadExplorationScene(sceneName, transitionToken);
            }

            // 目标为探索：走“棋盘常驻 + 叠加探索”逻辑
            return LoadExplorationScene(sceneName, transitionToken);
        }

        /// <summary>
        /// 开始一个转场流程，返回 Token。
        /// </summary>
        private int BeginTransition()
        {
            _transitionToken++;
            _activeTransitionToken = _transitionToken;
            _transitionCompleted = false;
            return _activeTransitionToken;
        }

        /// <summary>
        /// 标记转场完成（仅当前 Token 生效）。
        /// </summary>
        private void CompleteTransition(int token)
        {
            if (token < 0) return;
            if (token != _activeTransitionToken) return;
            _transitionCompleted = true;
        }

        /// <summary>
        /// M1 修复：判断某个 Token 对应的转场是否已过期。
        /// 转场协程可被 StopCoroutine 打断，但已发起的 AsyncOperation.completed 回调无法取消；
        /// 过期回调若继续执行"改 _currentExplorationScene / 切换棋盘根显隐"等副作用，
        /// 会与新转场互相踩踏。所有异步回调里的状态突变前都应先做本判断。
        /// 注：token &lt; 0 表示"无 Token 的原始加载路径"（LoadSceneAsyncRaw），不参与守卫。
        /// </summary>
        private bool IsStaleTransition(int token)
        {
            return token >= 0 && token != _activeTransitionToken;
        }

        /// <summary>
        /// 获取场景模式（若未配置则默认 Exploration）。
        /// </summary>
        private GameMode GetModeForScene(string sceneName)
        {
            return sceneRegistry != null ? sceneRegistry.GetGameMode(sceneName) : GameMode.Exploration;
        }

        /// <summary>
        /// 公开的场景模式查询（M7 修复配套）：
        /// 供 GameManager 在 EndLoading 无缓存状态时按当前场景推导正确的 GameState，
        /// 替代原先硬编码回退 FreeRoam 的隐性耦合。
        /// </summary>
        public GameMode GetSceneMode(string sceneName)
        {
            return GetModeForScene(sceneName);
        }
    }
}
