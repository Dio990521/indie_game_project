using System;
using System.Collections.Generic;
using IndieGame.Core.Utilities;
using UnityEngine;
using IndieGame.Core;
using IndieGame.UI;
using IndieGame.Gameplay.Board.Data;
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

        // 投骰子后是否在等待镜头拉远完成（避免角色在镜头还没拉远完就开始走）
        private bool _rollPending;
        // 等待期间缓存的骰子步数
        private int _pendingSteps;

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

            // 绑定镜头拉远完成事件：投骰子后先等 ActionMenuCameraController 把镜头拉远完，再真正开始移动
            SubscribeEvent<BoardActionMenuCameraSettledEvent>(_ => HandleCameraSettled());

            // 2. 界面互斥逻辑：监听背包系统的状态切换。
            // 当玩家在回合内打开背包查看道具时，棋盘操作菜单应当暂时隐藏，避免视觉叠层混乱。
            // 旧 InventoryManager 静态委托已统一迁移到 EventBus，订阅/反订阅都走 SubscribeEvent 模式。
            SubscribeEvent<InventoryOpenedEvent>(_ => HandleInventoryOpened(context));
            SubscribeEvent<InventoryClosedEvent>(_ => HandleInventoryClosed(context));

            // 装备界面同理：打开时隐藏操作菜单，关闭后恢复，与背包的互斥逻辑保持对称。
            SubscribeEvent<EquipmentUIOpenedEvent>(_ => HandleEquipmentUIOpened(context));
            SubscribeEvent<EquipmentUIClosedEvent>(_ => HandleEquipmentUIClosed(context));

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

            // 状态锁检查：如果控制器不存在、当前已经在位移中、或已经投过骰子在等镜头拉远，则不响应
            if (context.movementController == null || context.movementController.IsMoving) return;
            if (_rollPending) return;

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

            // 影骰子效果：若已激活，本次点数翻倍（一次性消耗）
            bool shadowDiceConsumed = context.movementController != null
                && context.movementController.ConsumeShadowDice();
            if (shadowDiceConsumed)
                steps *= 2;

            DebugTools.Log($"<color=cyan>[掷骰子] {steps} 步</color>");

            // 不立即切换状态：缓存步数，等 ActionMenuCameraController 广播镜头拉远完成后
            // 由 HandleCameraSettled 再真正进入 MovementState，避免角色在镜头还没拉远完就开始走。
            _pendingSteps = steps;
            _rollPending = true;
        }

        /// <summary>
        /// 镜头拉远完成回调：真正切换到移动状态。
        /// </summary>
        private void HandleCameraSettled()
        {
            if (!_rollPending || _context == null) return;
            _rollPending = false;
            int steps = _pendingSteps;
            _pendingSteps = 0;
            _context.ChangeState(new MovementState(steps));
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
        /// 响应装备界面打开事件：隐藏操作菜单，让出屏幕空间给装备 UI。
        /// </summary>
        private void HandleEquipmentUIOpened(BoardGameManager context)
        {
            if (_menu != null)
            {
                _menu.Hide();
            }
        }

        /// <summary>
        /// 响应装备界面关闭事件：重新显示操作菜单，恢复玩家的回合决策界面。
        /// </summary>
        private void HandleEquipmentUIClosed(BoardGameManager context)
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

            // M10 修复：由宝具 SO 多态创建自己的激活状态，
            // 取代原先按 SO 具体类型逐个 if-else 分发——新增宝具不再需要修改本类。
            BaseState<BoardGameManager> activationState = so != null ? so.CreateActivationState() : null;
            if (activationState != null && _context != null)
            {
                _context.ChangeState(activationState);
            }
            else
            {
                // 未知宝具 ID、SO 未注册、或该宝具未提供激活状态：回退到操作菜单
                DebugTools.LogWarning($"[PlayerTurnState] 宝具 ID \"{evt.TreasureId}\" 未注册或未提供激活状态，返回操作菜单。");
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
        /// 按钮分为左右两侧：左侧【骰子】【宝具】【露营】，右侧【背包】【装备】【地图】，
        /// 各侧内部按列表顺序从上到下排列，与 View 中的圆弧布局及方向键分组一一对应。
        /// 站在城镇格时不显示营地按钮——城镇提供专属服务，无法在此扎营。
        /// </summary>
        private List<BoardActionOptionData> BuildDefaultMenuData()
        {
            var options = new List<BoardActionOptionData>
            {
                new BoardActionOptionData
                {
                    Id   = BoardActionId.RollDice,
                    Name = new LocalizedString { TableReference = "BoardActions", TableEntryReference = "RollDice" },
                    Side = BoardActionSide.Left
                },
                new BoardActionOptionData
                {
                    Id   = BoardActionId.Treasure,
                    Name = new LocalizedString { TableReference = "BoardActions", TableEntryReference = "Treasure" },
                    Side = BoardActionSide.Left
                },
                new BoardActionOptionData
                {
                    Id   = BoardActionId.Item,
                    Name = new LocalizedString { TableReference = "BoardActions", TableEntryReference = "Bag" },
                    Side = BoardActionSide.Right
                },
                new BoardActionOptionData
                {
                    Id   = BoardActionId.Equip,
                    Name = new LocalizedString { TableReference = "BoardActions", TableEntryReference = "Equip" },
                    Side = BoardActionSide.Right
                },
                new BoardActionOptionData
                {
                    Id   = BoardActionId.Map,
                    Name = new LocalizedString { TableReference = "BoardActions", TableEntryReference = "Map" },
                    Side = BoardActionSide.Right
                },
            };

            // 城镇格上不提供营地：玩家应通过城镇旅馆休息，而非露营
            if (!IsStandingOnTownTile())
            {
                options.Add(new BoardActionOptionData
                {
                    Id   = BoardActionId.Camp,
                    Name = new LocalizedString { TableReference = "BoardActions", TableEntryReference = "Camp" },
                    Side = BoardActionSide.Left
                });
            }

            return options;
        }

        /// <summary>
        /// 检测玩家当前所在节点是否为城镇格。
        /// </summary>
        private bool IsStandingOnTownTile()
        {
            if (_context == null) return false;
            var mc = _context.movementController;
            if (mc == null) return false;
            var mapMgr = BoardMapManager.Instance;
            if (mapMgr == null) return false;
            var node = mapMgr.GetNode(mc.CurrentNodeId);
            return node != null && node.tileData is TownTile;
        }
    }
}
