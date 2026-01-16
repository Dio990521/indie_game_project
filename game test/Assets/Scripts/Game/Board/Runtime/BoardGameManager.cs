using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.Board.Runtime.States;

namespace IndieGame.Gameplay.Board.Runtime
{
    public class BoardGameManager : MonoSingleton<BoardGameManager>
    {
        [Header("Dependencies")]
        public BoardMovementController movementController;
        public IndieGame.UI.BoardActionMenuView actionMenu;

        public BoardState CurrentState { get; private set; }

        private void Start()
        {
            ChangeState(new InitState(this));
        }

        private void Update()
        {
            CurrentState?.Update();
        }

        private void OnEnable()
        {
            if (actionMenu != null) actionMenu.OnRollDiceRequested += HandleRollDiceRequested;
            InventoryManager.OnInventoryClosed += HandleInventoryClosed;
        }

        private void OnDisable()
        {
            if (actionMenu != null) actionMenu.OnRollDiceRequested -= HandleRollDiceRequested;
            InventoryManager.OnInventoryClosed -= HandleInventoryClosed;
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
    }
}
