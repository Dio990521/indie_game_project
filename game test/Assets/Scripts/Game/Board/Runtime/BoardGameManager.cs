using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using IndieGame.Core;
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
            _isBoardActive = GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.BoardMode;
            SetBoardComponentsActive(_isBoardActive);
            if (_isBoardActive) BeginBoardInitialization();
        }

        private void Update()
        {
            if (!_isBoardActive) return;
            _overlayStateMachine.Update(this);
            _stateMachine.Update(this);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EventBus.Subscribe<BoardEntityInteractionEvent>(HandleEntityInteraction);
            EventBus.Subscribe<GameStateChangedEvent>(HandleGlobalStateChanged);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            EventBus.Unsubscribe<BoardEntityInteractionEvent>(HandleEntityInteraction);
            EventBus.Unsubscribe<GameStateChangedEvent>(HandleGlobalStateChanged);
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

        private void HandleGlobalStateChanged(GameStateChangedEvent evt)
        {
            GameState newState = evt.NewState;
            _isBoardActive = newState == GameState.BoardMode;
            SetBoardComponentsActive(_isBoardActive);
            if (_isBoardActive)
            {
                BeginBoardInitialization();
                return;
            }
            StopBoardInitialization();
            ClearOverlayState();
            _stateMachine.Clear(this);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameState.BoardMode) return;
            BeginBoardInitialization();
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
                movementController.ResolveReferences(GameManager.Instance != null ? GameManager.Instance.LastBoardIndex : -1);
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
