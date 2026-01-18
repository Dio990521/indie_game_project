using UnityEngine;
using IndieGame.Core;
using IndieGame.UI;
using IndieGame.Gameplay.Inventory;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    public class PlayerTurnState : BoardState
    {
        private BoardActionMenuView _menu;

        public PlayerTurnState(BoardGameManager context) : base(context) { }

        public override void Enter()
        {
            _menu = UIManager.Instance != null ? UIManager.Instance.BoardActionMenuInstance : null;
            if (_menu != null)
            {
                _menu.SetAllowShow(true);
                _menu.OnRollDiceRequested += HandleRollDiceRequested;
            }
            InventoryManager.OnInventoryOpened += HandleInventoryOpened;
            InventoryManager.OnInventoryClosed += HandleInventoryClosed;
        }

        public override void Exit()
        {
            InventoryManager.OnInventoryOpened -= HandleInventoryOpened;
            InventoryManager.OnInventoryClosed -= HandleInventoryClosed;
            if (_menu != null)
            {
                _menu.OnRollDiceRequested -= HandleRollDiceRequested;
                _menu.SetAllowShow(false);
            }
        }

        public override void OnInteract()
        {
            if (GameManager.Instance.CurrentState != GameState.BoardMode) return;
            if (Context.movementController == null || Context.movementController.IsMoving) return;

            int steps = Random.Range(1, 7);
            Debug.Log($"<color=cyan>🎲 掷骰子: {steps}</color>");
            Context.ChangeState(new MovementState(Context, steps));
        }

        private void HandleRollDiceRequested()
        {
            OnInteract();
        }

        private void HandleInventoryOpened()
        {
            if (_menu != null)
            {
                _menu.SetAllowShow(false);
            }
        }

        private void HandleInventoryClosed()
        {
            if (_menu != null)
            {
                _menu.SetAllowShow(true);
            }
        }
    }
}
