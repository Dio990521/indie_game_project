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
            SetBoardComponentsActive(false);
        }

        private void Update()
        {
            if (!_isBoardActive) return;
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
            SetBoardVisualActive(isActive);
        }

        // 由 GameManager 显式调用，避免协程轮询。
        public void Init(bool force)
        {
            if (!_isBoardActive) return;
            if (_isInitialized && !force) return;

            if (movementController == null || movementController.Equals(null))
            {
                movementController = FindAnyObjectByType<BoardMovementController>();
            }
            if (movementController == null) return;

            movementController.ResolveReferences(-1);
            bool restoredFromSave = RestoreBoardPosition();

            ClearOverlayState();
            _stateMachine.Clear(this);
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
                movementController.SetCurrentNodeById(savedIndex);
                SyncCameraToPlayer();
                if (loader != null && loader.IsReturnToBoard)
                {
                    loader.ClearPayload();
                }
                return true;
            }
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
            Debug.Log($"<color=yellow>⚔ 遇到单位: {evt.Target.name} (Node {evt.Node.nodeID})</color>");
            evt.OnCompleted?.Invoke();
        }
    }
}
