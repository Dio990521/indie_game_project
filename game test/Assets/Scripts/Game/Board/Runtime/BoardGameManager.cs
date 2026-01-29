using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.CameraSystem;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Board.Runtime.States;

namespace IndieGame.Gameplay.Board.Runtime
{
    public class BoardGameManager : MonoSingleton<BoardGameManager>
    {
        [Header("Dependencies")]
        public BoardMovementController movementController;

        private readonly StateMachine<BoardGameManager> _stateMachine = new StateMachine<BoardGameManager>();
        private readonly StateMachine<BoardGameManager> _overlayStateMachine = new StateMachine<BoardGameManager>();
        public BaseState<BoardGameManager> CurrentState => _stateMachine.CurrentState;
        public BaseState<BoardGameManager> OverlayState => _overlayStateMachine.CurrentState;
        private bool _isBoardActive = false;
        private bool _isInitialized;

        protected override bool DestroyOnLoad => true;

        private void Start()
        {
            _isBoardActive = false;
            // 默认关闭棋盘逻辑，等待进入 Board 模式
            SetBoardComponentsActive(false);
        }

        private void Update()
        {
            if (!_isBoardActive) return;
            // Overlay 先更新，确保 UI/选择状态优先处理
            _overlayStateMachine.Update(this);
            _stateMachine.Update(this);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<BoardEntityInteractionEvent>(HandleEntityInteraction);
            EventBus.Subscribe<GameModeChangedEvent>(HandleGameModeChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BoardEntityInteractionEvent>(HandleEntityInteraction);
            EventBus.Unsubscribe<GameModeChangedEvent>(HandleGameModeChanged);
        }

        public void ChangeState(BaseState<BoardGameManager> newState)
        {
            if (newState == null) return;
            _stateMachine.ChangeState(newState, this);
        }

        public void RequestRollDice()
        {
            if (OverlayState != null)
            {
                OverlayState.OnInteract(this);
                return;
            }
            CurrentState?.OnInteract(this);
        }

        public void ResetToStart()
        {
            if (movementController != null)
            {
                movementController.ResetToStart();
            }
        }

        private void HandleGameModeChanged(GameModeChangedEvent evt)
        {
            _isBoardActive = evt.Mode == GameMode.Board;
            if (_isBoardActive)
            {
                // 进入棋盘场景：显式唤醒并强制重新初始化
                SetBoardComponentsActive(true);
                Init(true);
                return;
            }
            // 离开棋盘时进入休眠，停止更新与可视
            Sleep();
        }

        private void SetBoardComponentsActive(bool isActive)
        {
            if (movementController != null)
            {
                movementController.enabled = isActive;
                if (movementController.forkSelector != null)
                {
                    movementController.forkSelector.enabled = isActive;
                }
            }
            // 同步棋盘可视对象
            SetBoardVisualActive(isActive);
        }

        // 由 GameManager 显式调用，避免协程轮询。
        public void Init(bool force)
        {
            if (!_isBoardActive) return;
            if (_isInitialized && !force) return;

            if (movementController == null || movementController.Equals(null))
            {
                // 场景中查找控制器作为兜底
                movementController = FindAnyObjectByType<BoardMovementController>();
            }
            if (movementController == null) return;

            // 初始化引用并根据保存位置设置起点
            movementController.ResolveReferences(-1);
            bool restoredFromSave = RestoreBoardPosition();

            ClearOverlayState();
            _stateMachine.Clear(this);
            // 如果有存档点则直接进入玩家回合，否则进入初始化状态
            ChangeState(restoredFromSave ? new PlayerTurnState() : new InitState());
            _isInitialized = true;
        }

        public void PushOverlayState(BaseState<BoardGameManager> newState)
        {
            if (newState == null) return;
            ClearOverlayState();
            _overlayStateMachine.ChangeState(newState, this);
        }

        public void PopOverlayState()
        {
            ClearOverlayState();
        }

        private void ClearOverlayState()
        {
            if (OverlayState == null) return;
            _overlayStateMachine.Clear(this);
        }

        private bool RestoreBoardPosition()
        {
            if (movementController == null) return false;
            SceneLoader loader = SceneLoader.Instance;
            int savedIndex = loader != null ? loader.GetSavedBoardIndex() : -1;
            if (savedIndex >= 0)
            {
                // 从 SceneLoader 缓存的节点恢复
                movementController.SetCurrentNodeById(savedIndex);
                SyncCameraToPlayer();
                if (loader != null && loader.IsReturnToBoard)
                {
                    // 清理返回棋盘标记，避免重复触发
                    loader.ClearPayload();
                }
                return true;
            }
            // 没有保存点时回到起始位置
            movementController.ResetToStart();
            SyncCameraToPlayer();
            return false;
        }

        private void SetBoardVisualActive(bool isActive)
        {
            if (movementController == null) return;
            BoardEntity entity = movementController.PlayerEntity;
            if (entity == null) return;
            if (GameManager.Instance != null && GameManager.Instance.CurrentPlayer == entity.gameObject) return;
            // 非玩家棋盘实体按模式显示/隐藏
            entity.gameObject.SetActive(isActive);
        }

        private void SyncCameraToPlayer()
        {
            if (CameraManager.Instance == null) return;
            if (GameManager.Instance == null || GameManager.Instance.CurrentPlayer == null) return;
            CameraManager.Instance.SetFollowTarget(GameManager.Instance.CurrentPlayer.transform);
            CameraManager.Instance.WarpCameraToTarget();
        }

        private void Sleep()
        {
            ClearOverlayState();
            _stateMachine.Clear(this);
            SetBoardComponentsActive(false);
            SetBoardVisualActive(false);
        }

        private void HandleEntityInteraction(BoardEntityInteractionEvent evt)
        {
            if (evt.Target == null || evt.Node == null)
            {
                evt.OnCompleted?.Invoke();
                return;
            }
            // 这里只做简单输出，后续可扩展战斗或对话
            Debug.Log($"<color=yellow>⚔ 遇到单位: {evt.Target.name} (Node {evt.Node.nodeID})</color>");
            evt.OnCompleted?.Invoke();
        }
    }
}
