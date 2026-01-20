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

        public BoardState CurrentState { get; private set; }
        public BoardState OverlayState { get; private set; }
        private bool _isBoardActive = false;
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
            OverlayState?.Update();
            CurrentState?.Update();
        }

        private void OnEnable()
        {
            GameManager.OnStateChanged += HandleGlobalStateChanged;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            GameManager.OnStateChanged -= HandleGlobalStateChanged;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        public void ChangeState(BoardState newState)
        {
            if (newState == null) return;
            if (CurrentState != null && CurrentState.GetType() == newState.GetType()) return;
            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState.Enter();
        }

        public void RequestRollDice()
        {
            if (OverlayState != null)
            {
                OverlayState.OnInteract();
                return;
            }
            CurrentState?.OnInteract();
        }

        public void ResetToStart()
        {
            if (movementController != null)
            {
                movementController.ResetToStart();
            }
        }

        private void HandleGlobalStateChanged(GameState newState)
        {
            _isBoardActive = newState == GameState.BoardMode;
            SetBoardComponentsActive(_isBoardActive);
            if (_isBoardActive)
            {
                BeginBoardInitialization();
                return;
            }
            StopBoardInitialization();
            ClearOverlayState();
            CurrentState?.Exit();
            CurrentState = null;
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
        }

        private IEnumerator InitializeBoardRoutine()
        {
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
                ChangeState(new InitState(this));
            }

            _initRoutine = null;
        }

        public void PushOverlayState(BoardState newState)
        {
            if (newState == null) return;
            ClearOverlayState();
            OverlayState = newState;
            OverlayState.Enter();
        }

        public void PopOverlayState()
        {
            ClearOverlayState();
        }

        private void ClearOverlayState()
        {
            if (OverlayState == null) return;
            OverlayState.Exit();
            OverlayState = null;
        }
    }
}
