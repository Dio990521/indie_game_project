using UnityEngine;
using IndieGame.Gameplay.Inventory;
using IndieGame.Core;
using IndieGame.Core.Utilities;

namespace IndieGame.Gameplay.Board.Runtime
{
    public class BoardGameManager : MonoSingleton<BoardGameManager>
    {
        [Header("Dependencies")]
        public BoardMovementController movementController;
        public IndieGame.UI.BoardActionMenu actionMenu;

        private void OnEnable()
        {
            if (actionMenu != null) actionMenu.OnRollDiceRequested += HandleRollDiceRequested;
            if (movementController != null)
            {
                movementController.MoveStarted += HandleMoveStarted;
                movementController.MoveEnded += HandleMoveEnded;
            }
            InventoryManager.OnInventoryClosed += HandleInventoryClosed;
        }

        private void OnDisable()
        {
            if (actionMenu != null) actionMenu.OnRollDiceRequested -= HandleRollDiceRequested;
            if (movementController != null)
            {
                movementController.MoveStarted -= HandleMoveStarted;
                movementController.MoveEnded -= HandleMoveEnded;
            }
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

        private void HandleRollDiceRequested()
        {
            RollDice();
        }

        private void HandleMoveStarted()
        {
            if (actionMenu != null)
            {
                actionMenu.SetAllowShow(false);
            }
        }

        private void HandleMoveEnded()
        {
            if (actionMenu != null)
            {
                actionMenu.SetAllowShow(true);
            }
        }

        private void HandleInventoryClosed()
        {
            if (actionMenu != null && (movementController == null || !movementController.IsMoving))
            {
                actionMenu.SetAllowShow(true);
            }
        }
        public void ResetToStart()
        {
            if (movementController != null)
            {
                movementController.ResetToStart();
            }
        }
    }
}
