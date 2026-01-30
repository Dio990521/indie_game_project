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
                // 打开回合菜单并注册掷骰子回调
                _menu.Show(BuildDefaultMenuData());
                _onRollDice = () => OnInteract(context);
                _menu.OnRollDiceRequested += _onRollDice;
            }
            // 监听背包开关，以便隐藏/恢复菜单
            _onInventoryOpened = () => HandleInventoryOpened(context);
            _onInventoryClosed = () => HandleInventoryClosed(context);
            InventoryManager.OnInventoryOpened += _onInventoryOpened;
            InventoryManager.OnInventoryClosed += _onInventoryClosed;
        }

        public override void OnExit(BoardGameManager context)
        {
            // 清理 UI 与事件订阅，避免泄漏
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

            // 掷骰子进入移动阶段
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
                // 打开背包时隐藏棋盘菜单
                _menu.Hide();
            }
        }

        private void HandleInventoryClosed(BoardGameManager context)
        {
            if (_menu != null)
            {
                // 关闭背包后恢复棋盘菜单
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
