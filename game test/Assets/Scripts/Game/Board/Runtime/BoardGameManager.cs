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
        private bool _isBoardActive = false;

        protected override bool DestroyOnLoad => true;

        private void Start()
        {
            _isBoardActive = GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.BoardMode;
            SetBoardComponentsActive(_isBoardActive);
            if (_isBoardActive)
            {
                ChangeState(new InitState(this));
            }
        }

        private void Update()
        {
            if (!_isBoardActive) return;
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
            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState.Enter();
        }

        public void RequestRollDice()
        {
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
            if (newState == GameState.TurnDecision) return;
            _isBoardActive = newState == GameState.BoardMode;
            SetBoardComponentsActive(_isBoardActive);
            if (_isBoardActive)
            {
                if (movementController != null)
                {
                    movementController.ResolveReferences(GameManager.Instance != null ? GameManager.Instance.LastBoardIndex : -1);
                }
                if (CurrentState == null)
                {
                    InitializeBoard();
                }
                return;
            }
            CurrentState?.Exit();
            CurrentState = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (GameManager.Instance == null) return;

            if (movementController == null || movementController.Equals(null))
            {
                movementController = FindAnyObjectByType<BoardMovementController>();
            }

            if (movementController != null)
            {
                bool isBoardMode = GameManager.Instance.CurrentState == GameState.BoardMode;
                SetBoardComponentsActive(isBoardMode);
                if (!isBoardMode) return;
                movementController.ResolveReferences(GameManager.Instance.LastBoardIndex);
                ChangeState(new InitState(this));
            }
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

        private void InitializeBoard()
        {
            ChangeState(new InitState(this));
        }
    }
}
