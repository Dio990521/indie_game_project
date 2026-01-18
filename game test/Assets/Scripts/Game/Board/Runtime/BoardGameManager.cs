using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.Board.Runtime.States;
using UnityEngine.SceneManagement;

namespace IndieGame.Gameplay.Board.Runtime
{
    public class BoardGameManager : MonoSingleton<BoardGameManager>
    {
        [Header("Dependencies")]
        public BoardMovementController movementController;
        public IndieGame.UI.BoardActionMenuView actionMenu;

        public BoardState CurrentState { get; private set; }
        private bool _isBoardActive = false;
        private bool _menuBound = false;

        protected override bool DestroyOnLoad => true;

        private void Start()
        {
            ChangeState(new InitState(this));
            _isBoardActive = GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.BoardMode;
        }

        private void Update()
        {
            if (!_isBoardActive) return;
            CurrentState?.Update();
        }

        private void OnEnable()
        {
            EnsureActionMenu();
            BindActionMenu();
            InventoryManager.OnInventoryClosed += HandleInventoryClosed;
            GameManager.OnStateChanged += HandleGlobalStateChanged;
            IndieGame.UI.UIManager.OnUIReady += HandleUIReady;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            InventoryManager.OnInventoryOpened += HandleInventoryOpened;
        }

        private void OnDisable()
        {
            UnbindActionMenu();
            InventoryManager.OnInventoryClosed -= HandleInventoryClosed;
            GameManager.OnStateChanged -= HandleGlobalStateChanged;
            IndieGame.UI.UIManager.OnUIReady -= HandleUIReady;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            InventoryManager.OnInventoryOpened -= HandleInventoryOpened;
        }

        [ContextMenu("Roll Dice")]
        public void RollDice()
        {
            if (GameManager.Instance.CurrentState != GameState.BoardMode) return;
            if (movementController == null || movementController.IsMoving) return;

            int steps = Random.Range(1, 7);
            Debug.Log($"<color=cyan>🎲 掷骰子: {steps}</color>");
            movementController.BeginMove(steps);
        }

        public void ChangeState(BoardState newState)
        {
            if (newState == null) return;
            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState.Enter();
        }

        public void ResetToStart()
        {
            if (movementController != null)
            {
                movementController.ResetToStart();
            }
        }

        private void HandleRollDiceRequested()
        {
            CurrentState?.OnInteract();
        }

        private void HandleInventoryClosed()
        {
            if (CurrentState is PlayerTurnState && actionMenu != null)
            {
                actionMenu.SetAllowShow(true);
            }
        }

        private void HandleInventoryOpened()
        {
            if (actionMenu != null)
            {
                actionMenu.SetAllowShow(false);
            }
        }

        private void HandleGlobalStateChanged(GameState newState)
        {
            _isBoardActive = newState == GameState.BoardMode;
            if (!_isBoardActive && actionMenu != null)
            {
                actionMenu.SetAllowShow(false);
            }
            if (_isBoardActive)
            {
                if (movementController != null)
                {
                    movementController.ResolveReferences(GameManager.Instance != null ? GameManager.Instance.LastBoardIndex : -1);
                }
                ChangeState(new InitState(this));
            }
        }

        private void EnsureActionMenu()
        {
            if (actionMenu != null) return;
            if (IndieGame.UI.UIManager.Instance != null)
            {
                actionMenu = IndieGame.UI.UIManager.Instance.BoardActionMenuInstance;
            }
        }

        private void HandleUIReady()
        {
            EnsureActionMenu();
            BindActionMenu();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameState.BoardMode) return;

            if (movementController == null || movementController.Equals(null))
            {
                movementController = FindAnyObjectByType<BoardMovementController>();
            }

            if (movementController != null)
            {
                movementController.ResolveReferences(GameManager.Instance.LastBoardIndex);
                ChangeState(new InitState(this));
            }
        }

        private void BindActionMenu()
        {
            if (_menuBound) return;
            if (actionMenu == null) return;
            actionMenu.OnRollDiceRequested += HandleRollDiceRequested;
            _menuBound = true;
        }

        private void UnbindActionMenu()
        {
            if (!_menuBound) return;
            if (actionMenu != null) actionMenu.OnRollDiceRequested -= HandleRollDiceRequested;
            _menuBound = false;
        }

        private void LateUpdate()
        {
            if (actionMenu == null)
            {
                EnsureActionMenu();
                BindActionMenu();
            }
        }
    }
}
