using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.CameraSystem;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Board.Runtime.States;

namespace IndieGame.Gameplay.Board.Runtime
{
    /// <summary>
    /// 棋盘游戏管理器：作为大富翁类玩法的核心大脑。
    /// 负责驱动棋盘逻辑状态机、处理玩家/NPC回合切换、同步相机以及响应游戏模式变更。
    /// 采用单例模式 (MonoSingleton) 确保全局唯一访问。
    /// </summary>
    public class BoardGameManager : MonoSingleton<BoardGameManager>
    {
        [Header("外部依赖")]
        [Tooltip("负责具体位移逻辑和节点坐标转换的控制器")]
        public BoardMovementController movementController;

        // --- 状态机相关 ---
        // 主状态机：控制游戏的主循环流程（如：初始化 -> 玩家回合 -> 移动中 -> 事件触发 -> 敌人回合）
        private readonly StateMachine<BoardGameManager> _stateMachine = new StateMachine<BoardGameManager>();

        // 覆盖状态机 (Overlay)：用于处理临时弹出的中断逻辑（如：岔路选择 UI、弹窗交互）
        // 它的 Update 优先级高于主状态机，能捕获并拦截输入。
        private readonly StateMachine<BoardGameManager> _overlayStateMachine = new StateMachine<BoardGameManager>();

        // 公开属性：供外部查询当前运行中的状态
        public BaseState<BoardGameManager> CurrentState => _stateMachine.CurrentState;
        public BaseState<BoardGameManager> OverlayState => _overlayStateMachine.CurrentState;

        // 控制开关：标识当前是否处于棋盘玩法模式下
        private bool _isBoardActive = false;
        // 初始化标记：防止单次场景加载中重复初始化
        private bool _isInitialized;

        // 重写单例属性：当切换场景时，由于棋盘模式通常是独立模式，设置为销毁以防数据残留
        protected override bool DestroyOnLoad => true;

        private void Start()
        {
            // 脚本启动时默认处于非活动状态，直到 GameManager 切换模式
            _isBoardActive = false;
            SetBoardComponentsActive(false);
        }

        private void Update()
        {
            // 如果棋盘模式未激活，直接跳过逻辑轮询
            if (!_isBoardActive) return;

            // 核心逻辑驱动顺序：
            // 1. 先更新 Overlay 状态机：确保如果玩家正在进行“分叉路口选择”，该选择逻辑优先被执行。
            _overlayStateMachine.Update(this);
            // 2. 后更新主状态机：处理常规的移动或回合逻辑。
            _stateMachine.Update(this);
        }

        private void OnEnable()
        {
            // 订阅实体交互事件：例如玩家走到了 NPC 所在的格子上
            EventBus.Subscribe<BoardEntityInteractionEvent>(HandleEntityInteraction);
            // 订阅全局游戏模式变更事件：用于在“探索模式”与“棋盘模式”间无缝切换
            EventBus.Subscribe<GameModeChangedEvent>(HandleGameModeChanged);
        }

        private void OnDisable()
        {
            // 销毁时务必取消订阅，防止内存泄漏或在空对象上触发回调
            EventBus.Unsubscribe<BoardEntityInteractionEvent>(HandleEntityInteraction);
            EventBus.Unsubscribe<GameModeChangedEvent>(HandleGameModeChanged);
        }

        /// <summary>
        /// 切换主状态机的当前状态。
        /// </summary>
        /// <param name="newState">即将进入的新状态实例（如 MovementState）</param>
        public void ChangeState(BaseState<BoardGameManager> newState)
        {
            if (newState == null) return;
            _stateMachine.ChangeState(newState, this);
        }

        /// <summary>
        /// 外部接口：请求执行交互（通常绑定到掷骰子按钮）。
        /// 逻辑：如果有弹出的 Overlay（如岔路选择），则由 Overlay 消耗输入；否则传给主回合状态。
        /// </summary>
        public void RequestRollDice()
        {
            if (OverlayState != null)
            {
                OverlayState.OnInteract(this);
                return;
            }
            CurrentState?.OnInteract(this);
        }

        /// <summary>
        /// 强制将所有移动控制器重置到起点地块。
        /// </summary>
        public void ResetToStart()
        {
            if (movementController != null)
            {
                movementController.ResetToStart();
            }
        }

        /// <summary>
        /// 处理游戏模式切换的响应逻辑。
        /// </summary>
        private void HandleGameModeChanged(GameModeChangedEvent evt)
        {
            _isBoardActive = evt.Mode == GameMode.Board;
            if (_isBoardActive)
            {
                // 如果切换到棋盘模式：唤醒组件、执行初始化。
                SetBoardComponentsActive(true);
                Init(true); // force = true 确保在模式切回时重新刷新状态
                return;
            }
            // 如果离开棋盘模式：停止所有协程和状态，进入休眠。
            Sleep();
        }

        /// <summary>
        /// 启用/禁用棋盘相关的核心组件。
        /// </summary>
        /// <param name="isActive">是否激活</param>
        private void SetBoardComponentsActive(bool isActive)
        {
            if (movementController != null)
            {
                movementController.enabled = isActive;
                // 同时同步分叉选择器的激活状态
                if (movementController.forkSelector != null)
                {
                    movementController.forkSelector.enabled = isActive;
                }
            }
            // 同步棋盘实体的视觉显示/隐藏
            SetBoardVisualActive(isActive);
        }

        /// <summary>
        /// 初始化棋盘环境。由 GameManager 或模式切换器显式调用。
        /// </summary>
        /// <param name="force">是否强制重新初始化（即使已初始化过）</param>
        public void Init(bool force)
        {
            if (!_isBoardActive) return;
            if (_isInitialized && !force) return;

            // 依赖检查：如果 inspector 没挂载，尝试在当前场景动态搜索
            if (movementController == null || movementController.Equals(null))
            {
                movementController = FindAnyObjectByType<BoardMovementController>();
            }
            if (movementController == null) return;

            // 1. 解析引用：绑定玩家 Token、加载地图节点数据。
            movementController.ResolveReferences(-1);

            // 2. 位置恢复：从存档或内存中读取玩家应该在哪一个地块。
            bool restoredFromSave = RestoreBoardPosition();

            // 3. 状态清空：确保状态机处于干净的初始状态。
            ClearOverlayState();
            _stateMachine.Clear(this);

            // 4. 逻辑进入点：如果是从其他地图返回，直接进入玩家回合；否则执行 InitState（可能包含开场动画）。
            ChangeState(restoredFromSave ? new PlayerTurnState() : new InitState());

            _isInitialized = true;
        }

        /// <summary>
        /// 压入一个覆盖状态（Overlay）。
        /// 常用场景：玩家移动到分叉点，弹出 UI 让玩家选择路径，此时主状态机暂停在移动中。
        /// </summary>
        public void PushOverlayState(BaseState<BoardGameManager> newState)
        {
            if (newState == null) return;
            ClearOverlayState(); // 棋盘模式通常只允许同时存在一个 Overlay
            _overlayStateMachine.ChangeState(newState, this);
        }

        /// <summary>
        /// 弹出/退出当前的覆盖状态。
        /// </summary>
        public void PopOverlayState()
        {
            ClearOverlayState();
        }

        /// <summary>
        /// 内部清理：强制终结 Overlay 状态机的生命周期。
        /// </summary>
        private void ClearOverlayState()
        {
            if (OverlayState == null) return;
            _overlayStateMachine.Clear(this);
        }

        /// <summary>
        /// 尝试从存档或场景加载器中恢复棋盘位置。
        /// </summary>
        /// <returns>如果成功恢复了位置返回 true，否则重置到起点返回 false</returns>
        private bool RestoreBoardPosition()
        {
            if (movementController == null) return false;
            SceneLoader loader = SceneLoader.Instance;

            // 获取在切换场景（如进入战斗/商店）前保存的地块索引
            int savedIndex = loader != null ? loader.GetSavedBoardIndex() : -1;

            if (savedIndex >= 0)
            {
                // 传送玩家到保存的节点
                movementController.SetCurrentNodeById(savedIndex);
                SyncCameraToPlayer(); // 确保相机立即对齐，防止画面闪烁

                // 检查是否是从战斗/室内返回棋盘，是的话清理临时标记
                if (loader != null && loader.IsReturnToBoard)
                {
                    loader.ClearPayload();
                }
                return true;
            }

            // 兜底方案：如果没有存档记录，将玩家放回起始地块（Node 0）
            movementController.ResetToStart();
            SyncCameraToPlayer();
            return false;
        }

        /// <summary>
        /// 设置棋盘实体的视觉显隐。
        /// 玩家实体通常受 GameManager 统一管理，此方法主要处理非玩家实体（如 NPC 或 棋盘 Token）。
        /// </summary>
        private void SetBoardVisualActive(bool isActive)
        {
            if (movementController == null) return;
            BoardEntity entity = movementController.PlayerEntity;
            if (entity == null) return;

            // 如果当前场景的玩家对象正是该实体，则不在此处理（由 GameManager 控制切换）
            if (GameManager.Instance != null && GameManager.Instance.CurrentPlayer == entity.gameObject) return;

            entity.gameObject.SetActive(isActive);
        }

        /// <summary>
        /// 将主摄像机强制同步到玩家位置。
        /// </summary>
        private void SyncCameraToPlayer()
        {
            if (CameraManager.Instance == null) return;
            if (GameManager.Instance == null || GameManager.Instance.CurrentPlayer == null) return;

            // 设置相机跟随目标，并调用 Warp 立即瞬移相机，避免从旧位置滑行过来
            CameraManager.Instance.SetFollowTarget(GameManager.Instance.CurrentPlayer.transform);
            CameraManager.Instance.WarpCameraToTarget();
        }

        /// <summary>
        /// 关闭棋盘模式：清理所有状态机，并禁用相关物理/逻辑组件。
        /// </summary>
        private void Sleep()
        {
            ClearOverlayState();
            _stateMachine.Clear(this);
            SetBoardComponentsActive(false);
            SetBoardVisualActive(false);
        }

        /// <summary>
        /// 处理实体间交互事件（如：玩家停在 NPC 所在的格子上）。
        /// 目前仅作为日志输出，未来可在此处触发战斗流程或对话 UI。
        /// </summary>
        private void HandleEntityInteraction(BoardEntityInteractionEvent evt)
        {
            if (evt.Target == null || evt.Node == null)
            {
                evt.OnCompleted?.Invoke();
                return;
            }

            // 输出调试信息：显示遇到的单位名称和所在节点 ID
            Debug.Log($"<color=yellow>⚔ 遇到单位: {evt.Target.name} (Node {evt.Node.nodeID})</color>");

            // 交互完成回调：允许后续逻辑继续（如主流程继续移动或切换回合）
            evt.OnCompleted?.Invoke();
        }
    }
}