using System;
using System.Collections.Generic;
using IndieGame.Core.Utilities;
using UnityEngine;
using IndieGame.Core;
using IndieGame.UI;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.ActionPoint;
using IndieGame.Gameplay.Treasure;
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

        // 收集本状态期间所有的反订阅闭包，OnExit 时统一执行，避免遗漏单条 Unsubscribe。
        // 该模式与 EventBusMonoBehaviour 的设计一致，只是状态机不能直接继承 MonoBehaviour 基类，故在此本地实现。
        private readonly List<Action> _eventUnsubscribers = new List<Action>();

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
            SubscribeEvent<BoardRollDiceRequestedEvent>(_ => OnInteract(_context));

            // 2. 界面互斥逻辑：监听背包系统的状态切换。
            // 当玩家在回合内打开背包查看道具时，棋盘操作菜单应当暂时隐藏，避免视觉叠层混乱。
            Action onInventoryOpened = () => HandleInventoryOpened(context);
            Action onInventoryClosed = () => HandleInventoryClosed(context);
            InventoryManager.OnInventoryOpened += onInventoryOpened;
            InventoryManager.OnInventoryClosed += onInventoryClosed;
            _eventUnsubscribers.Add(() => InventoryManager.OnInventoryOpened -= onInventoryOpened);
            _eventUnsubscribers.Add(() => InventoryManager.OnInventoryClosed -= onInventoryClosed);

            // 订阅宝具菜单相关事件：
            // - BoardTreasureMenuRequested：操作菜单点击"宝具"时触发，由此处直接调用 TreasureMenuView.Show()
            //   集中在 PlayerTurnState 处理可提供 UIManager 未配置时的兜底回退逻辑
            // - TreasureItemSelected：玩家在宝具菜单中确认选择，切换到对应宝具激活状态
            // - TreasureMenuCancelled：玩家取消宝具菜单，重新显示操作菜单
            SubscribeEvent<BoardTreasureMenuRequestedEvent>(HandleBoardTreasureMenuRequested);
            SubscribeEvent<TreasureItemSelectedEvent>(HandleTreasureSelected);
            SubscribeEvent<TreasureMenuCancelledEvent>(_ => HandleTreasureCancelled());
        }

        /// <summary>
        /// 本地辅助：订阅 EventBus 事件并自动登记反订阅闭包，确保 OnExit 一次性清理。
        /// </summary>
        private void SubscribeEvent<T>(Action<T> handler)
        {
            if (handler == null) return;
            EventBus.Subscribe(handler);
            _eventUnsubscribers.Add(() => EventBus.Unsubscribe(handler));
        }

        /// <summary>
        /// 退出状态时执行：负责清理 UI 状态和所有的事件监听。
        /// 这是一个良好的编程习惯，防止在状态切换后继续响应旧事件。
        /// </summary>
        public override void OnExit(BoardGameManager context)
        {
            // 一次性执行所有反订阅闭包，避免逐个 Unsubscribe 易遗漏
            for (int i = 0; i < _eventUnsubscribers.Count; i++) _eventUnsubscribers[i]();
            _eventUnsubscribers.Clear();

            if (_menu != null)
            {
                // 隐藏菜单
                _menu.Hide();
            }

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
                    DebugTools.Log("<color=orange>[行动点] 行动点不足，无法掷骰子。</color>");
                    return;
                }
            }

            // --- 核心游戏逻辑：掷骰子 ---
            // 随机生成 1 到 4 之间的点数
            int steps = UnityEngine.Random.Range(1, 5);
            DebugTools.Log($"<color=cyan>🎲 掷骰子: {steps}</color>");

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
        /// 响应操作菜单"宝具"点击：直接调用 TreasureMenuView.Show()。
        /// 若 UIManager 未配置宝具菜单预制体或宝具列表为空，则直接回退到操作菜单。
        /// </summary>
        private void HandleBoardTreasureMenuRequested(BoardTreasureMenuRequestedEvent evt)
        {
            var treasureMenu = UIManager.Instance?.TreasureMenuInstance;
            var ownedTreasures = TreasureSystem.Instance?.OwnedTreasures;

            if (treasureMenu == null)
            {
                DebugTools.LogWarning("[PlayerTurnState] TreasureMenuInstance 未配置，回退到操作菜单。");
                _menu?.Show(BuildDefaultMenuData());
                return;
            }

            if (ownedTreasures == null || ownedTreasures.Count == 0)
            {
                DebugTools.Log("[PlayerTurnState] 宝具列表为空，回退到操作菜单。");
                _menu?.Show(BuildDefaultMenuData());
                return;
            }

            treasureMenu.Show(ownedTreasures);
        }

        /// <summary>
        /// 响应宝具选中事件：根据宝具 ID 切换到对应的激活状态。
        /// </summary>
        private void HandleTreasureSelected(TreasureItemSelectedEvent evt)
        {
            // 从 TreasureSystem 按 ID 查找 SO，避免在 BoardGameManager 上维护各宝具引用
            TreasureSO so = null;
            var owned = TreasureSystem.Instance?.OwnedTreasures;
            if (owned != null)
            {
                foreach (var t in owned)
                {
                    if (t.TreasureId == evt.TreasureId) { so = t; break; }
                }
            }

            if (so is WingTreasureSO wingData && _context != null)
            {
                _context.ChangeState(new WingTreasureState(wingData));
            }
            else if (so is WoodenPuppetTreasureSO puppetData && _context != null)
            {
                _context.ChangeState(new WoodenPuppetTreasureState(puppetData));
            }
            else if (so is ImmovableBellTreasureSO bellData && _context != null)
            {
                _context.ChangeState(new ImmovableBellTreasureState(bellData));
            }
            else
            {
                // 未知宝具 ID 或 SO 未注册到 TreasureSystem：回退到操作菜单
                DebugTools.LogWarning($"[PlayerTurnState] 未知宝具 ID \"{evt.TreasureId}\" 或未注册，返回操作菜单。");
                _menu?.Show(BuildDefaultMenuData());
            }
        }

        /// <summary>
        /// 响应宝具菜单取消事件：重新显示操作菜单。
        /// </summary>
        private void HandleTreasureCancelled()
        {
            _menu?.Show(BuildDefaultMenuData());
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
                // 2. 背包选项
                new BoardActionOptionData
                {
                    Id = BoardActionId.Item,
                    Name = new LocalizedString { TableReference = "BoardActions", TableEntryReference = "Bag" }
                },
                // 3. 宝具选项
                new BoardActionOptionData
                {
                    Id = BoardActionId.Treasure,
                    Name = new LocalizedString { TableReference = "BoardActions", TableEntryReference = "Treasure" }
                },
                // 4. 营地/整备选项
                new BoardActionOptionData
                {
                    Id = BoardActionId.Camp,
                    Name = new LocalizedString { TableReference = "BoardActions", TableEntryReference = "Camp" }
                }
            };
        }
    }
}
