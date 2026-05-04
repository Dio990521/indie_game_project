using UnityEngine;
using IndieGame.Core;
using IndieGame.UI;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.ActionPoint;
using UnityEngine.Localization;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    /// <summary>
    /// 玩家回合状态：棋盘模式下玩家的初始决策阶段。
    /// 负责显示操作菜单（掷骰子、道具、整备等），并等待玩家触发行动。
    /// </summary>
    public class PlayerTurnState : BoardState
    {
        // 缓存棋盘操作菜单的视图组件引用
        private BoardActionMenuView _menu;
        private BoardGameManager _context;

        // 缓存事件委托，确保订阅与取消订阅的是同一个方法引用，防止内存泄漏
        private System.Action<BoardRollDiceRequestedEvent> _onRollDice;
        private System.Action _onInventoryOpened;
        private System.Action _onInventoryClosed;

        /// <summary>
        /// 进入该状态时执行：初始化 UI 菜单并绑定相关输入事件。
        /// </summary>
        public override void OnEnter(BoardGameManager context)
        {
            _context = context;

            // 行动点耗尽检测：若无法再行动，强制进入露营，不显示回合菜单
            if (ActionPointSystem.Instance != null &&
                ActionPointSystem.Instance.CurrentActionPoints <= 0)
            {
                context.ChangeState(new CampingState(context.campingLocationId));
                return;
            }

            // 1. 获取 UI 菜单实例：通过 UIManager 单例访问全局唯一的棋盘菜单
            _menu = UIManager.Instance != null ? UIManager.Instance.BoardActionMenuInstance : null;

            if (_menu != null)
            {
                // 显示菜单并注入初始化的操作项数据（掷骰子、道具等）
                _menu.Show(BuildDefaultMenuData());
            }

            // 绑定掷骰子事件：由 UI 通过 EventBus 广播
            _onRollDice = _ => OnInteract(_context);
            EventBus.Subscribe(_onRollDice);

            // 2. 界面互斥逻辑：监听背包系统的状态切换。
            // 当玩家在回合内打开背包查看道具时，棋盘操作菜单应当暂时隐藏，避免视觉叠层混乱。
            _onInventoryOpened = () => HandleInventoryOpened(context);
            _onInventoryClosed = () => HandleInventoryClosed(context);

            InventoryManager.OnInventoryOpened += _onInventoryOpened;
            InventoryManager.OnInventoryClosed += _onInventoryClosed;
        }

        /// <summary>
        /// 退出状态时执行：负责清理 UI 状态和所有的事件监听。
        /// 这是一个良好的编程习惯，防止在状态切换后继续响应旧事件。
        /// </summary>
        public override void OnExit(BoardGameManager context)
        {
            // 注销背包相关的全局事件
            if (_onInventoryOpened != null) InventoryManager.OnInventoryOpened -= _onInventoryOpened;
            if (_onInventoryClosed != null) InventoryManager.OnInventoryClosed -= _onInventoryClosed;
            if (_onRollDice != null)
            {
                EventBus.Unsubscribe(_onRollDice);
            }

            if (_menu != null)
            {
                // 隐藏菜单
                _menu.Hide();
            }

            // 清空委托引用
            _onRollDice = null;
            _onInventoryOpened = null;
            _onInventoryClosed = null;
            _context = null;
        }

        /// <summary>
        /// 处理交互逻辑：当玩家点击“掷骰子”或触发全局交互键时调用。
        /// </summary>
        public override void OnInteract(BoardGameManager context)
        {
            // 安全检查：必须处于棋盘模式状态
            if (GameManager.Instance.CurrentState != GameState.BoardMode) return;

            // 状态锁检查：如果控制器不存在或当前已经在位移中，则不响应
            if (context.movementController == null || context.movementController.IsMoving) return;

            // 行动点检查：每次掷骰子消耗 1 点行动点
            if (ActionPointSystem.Instance != null)
            {
                if (!ActionPointSystem.Instance.TryConsumeActionPoints(1, "RollDice"))
                {
                    Debug.Log("<color=orange>[行动点] 行动点不足，无法掷骰子。</color>");
                    return;
                }
            }

            // --- 核心游戏逻辑：掷骰子 ---
            // 随机生成 1 到 6 之间的点数
            int steps = Random.Range(1, 7);
            Debug.Log($"<color=cyan>🎲 掷骰子: {steps}</color>");

            // 切换状态机：进入“移动状态”，并将计算出的步数传递过去
            context.ChangeState(new MovementState(steps));
        }

        /// <summary>
        /// 占位方法：预留给后续可能的掷骰子请求处理。
        /// </summary>
        private void HandleRollDiceRequested()
        {
        }

        /// <summary>
        /// 响应背包打开事件：隐藏操作菜单，让出屏幕空间给背包 UI。
        /// </summary>
        private void HandleInventoryOpened(BoardGameManager context)
        {
            if (_menu != null)
            {
                _menu.Hide();
            }
        }

        /// <summary>
        /// 响应背包关闭事件：重新显示操作菜单，恢复玩家的回合决策界面。
        /// </summary>
        private void HandleInventoryClosed(BoardGameManager context)
        {
            if (_menu != null)
            {
                _menu.Show(BuildDefaultMenuData());
            }
        }

        /// <summary>
        /// 构建默认菜单数据：定义玩家回合开始时菜单里有哪些按钮。
        /// 这里使用了本地化字符串 (LocalizedString)，确保 UI 文字支持多语言。
        /// </summary>
        /// <returns>操作选项数据列表</returns>
        private System.Collections.Generic.List<BoardActionOptionData> BuildDefaultMenuData()
        {
            return new System.Collections.Generic.List<BoardActionOptionData>
            {
                // 1. 掷骰子选项
                new BoardActionOptionData
                {
                    Id = BoardActionId.RollDice,
                    Name = new LocalizedString { TableReference = "BoardActions", TableEntryReference = "RollDice" }
                },
                // 2. 道具选项
                new BoardActionOptionData
                {
                    Id = BoardActionId.Item,
                    Name = new LocalizedString { TableReference = "BoardActions", TableEntryReference = "Item" }
                },
                // 3. 营地/整备选项
                new BoardActionOptionData
                {
                    Id = BoardActionId.Camp,
                    Name = new LocalizedString { TableReference = "BoardActions", TableEntryReference = "Camp" }
                }
            };
        }
    }
}
