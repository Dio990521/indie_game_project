using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        private bool _isInitializing = false;
        private Coroutine _initRoutine;

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
            if (_isInitializing && CurrentState == null && !(newState is InitState)) return;
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
            SetBoardComponentsActive(_isBoardActive);
            if (_isBoardActive)
            {
                // 进入棋盘场景：唤醒并初始化棋盘逻辑
                BeginBoardInitialization();
                return;
            }
            StopBoardInitialization();
            ClearOverlayState();
            _stateMachine.Clear(this);
            SetBoardVisualActive(false);
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

        private void BeginBoardInitialization()
        {
            if (_initRoutine != null) return;
            _initRoutine = StartCoroutine(InitializeBoardRoutine());
        }

        private void StopBoardInitialization()
        {
            if (_initRoutine == null) return;
            StopCoroutine(_initRoutine);
            _initRoutine = null;
            _isInitializing = false;
        }

        private IEnumerator InitializeBoardRoutine()
        {
            _isInitializing = true;
            yield return new WaitUntil(() => GameManager.Instance != null);
            yield return new WaitUntil(() => _isBoardActive);
            yield return new WaitUntil(() => SceneManager.GetActiveScene().isLoaded);

            if (movementController == null || movementController.Equals(null))
            {
                movementController = FindAnyObjectByType<BoardMovementController>();
            }

            if (movementController != null)
            {
                movementController.ResolveReferences(-1);
                RestoreBoardPosition();
            }

            if (CurrentState == null)
            {
                ChangeState(new InitState());
            }

            _isInitializing = false;
            _initRoutine = null;
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

        private void RestoreBoardPosition()
        {
            if (movementController == null) return;
            SceneLoader loader = SceneLoader.Instance;
            int savedIndex = loader != null ? loader.GetSavedBoardIndex() : -1;
            if (savedIndex >= 0)
            {
                // 有记忆节点，直接恢复
                movementController.SetCurrentNodeById(savedIndex);
            }
            else
            {
                // 无记忆节点，回到默认起点
                movementController.ResetToStart();
            }

            if (CameraManager.Instance != null && GameManager.Instance != null && GameManager.Instance.CurrentPlayer != null)
            {
                CameraManager.Instance.SetFollowTarget(GameManager.Instance.CurrentPlayer.transform);
                CameraManager.Instance.WarpCameraToTarget();
            }

            if (loader != null && loader.IsReturnToBoard)
            {
                loader.ClearPayload();
            }
        }

        private void SetBoardVisualActive(bool isActive)
        {
            if (movementController == null) return;
            BoardEntity entity = movementController.PlayerEntity;
            if (entity == null) return;
            if (GameManager.Instance != null && GameManager.Instance.CurrentPlayer == entity.gameObject) return;
            entity.gameObject.SetActive(isActive);
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
