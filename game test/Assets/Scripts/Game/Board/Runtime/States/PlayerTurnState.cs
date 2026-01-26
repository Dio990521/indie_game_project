using UnityEngine;
using IndieGame.Core;
using IndieGame.UI;
using IndieGame.Gameplay.Inventory;
using UnityEngine.Localization;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    public class PlayerTurnState : BoardState
    {
        private BoardActionMenuView _menu;
        private System.Action _onRollDice;
        private System.Action _onInventoryOpened;
        private System.Action _onInventoryClosed;

        public override void OnEnter(BoardGameManager context)
        {
            _menu = UIManager.Instance != null ? UIManager.Instance.BoardActionMenuInstance : null;
            if (_menu != null)
            {
                _menu.Show(BuildDefaultMenuData());
                _onRollDice = () => OnInteract(context);
                _menu.OnRollDiceRequested += _onRollDice;
            }
            _onInventoryOpened = () => HandleInventoryOpened(context);
            _onInventoryClosed = () => HandleInventoryClosed(context);
            InventoryManager.OnInventoryOpened += _onInventoryOpened;
            InventoryManager.OnInventoryClosed += _onInventoryClosed;
        }

        public override void OnExit(BoardGameManager context)
        {
            if (_onInventoryOpened != null) InventoryManager.OnInventoryOpened -= _onInventoryOpened;
            if (_onInventoryClosed != null) InventoryManager.OnInventoryClosed -= _onInventoryClosed;
            if (_menu != null)
            {
                if (_onRollDice != null) _menu.OnRollDiceRequested -= _onRollDice;
                _menu.Hide();
            }
            _onRollDice = null;
            _onInventoryOpened = null;
            _onInventoryClosed = null;
        }

        public override void OnInteract(BoardGameManager context)
        {
            if (GameManager.Instance.CurrentState != GameState.BoardMode) return;
            if (context.movementController == null || context.movementController.IsMoving) return;

            int steps = Random.Range(1, 7);
            Debug.Log($"<color=cyan>🎲 掷骰子: {steps}</color>");
            context.ChangeState(new MovementState(steps));
        }

        private void HandleRollDiceRequested()
        {
        }

        private void HandleInventoryOpened(BoardGameManager context)
        {
            if (_menu != null)
            {
                _menu.Hide();
            }
        }

        private void HandleInventoryClosed(BoardGameManager context)
        {
            if (_menu != null)
            {
                _menu.Show(BuildDefaultMenuData());
            }
        }

        private System.Collections.Generic.List<BoardActionOptionData> BuildDefaultMenuData()
        {
            return new System.Collections.Generic.List<BoardActionOptionData>
            {
                new BoardActionOptionData
                {
                    Id = BoardActionId.RollDice,
                    Name = new LocalizedString { TableReference = "BoardActions", TableEntryReference = "RollDice" }
                },
                new BoardActionOptionData
                {
                    Id = BoardActionId.Item,
                    Name = new LocalizedString { TableReference = "BoardActions", TableEntryReference = "Item" }
                },
                new BoardActionOptionData
                {
                    Id = BoardActionId.Camp,
                    Name = new LocalizedString { TableReference = "BoardActions", TableEntryReference = "Camp" }
                }
            };
        }
    }
}
