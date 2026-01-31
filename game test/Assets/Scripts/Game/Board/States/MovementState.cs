using UnityEngine;
using IndieGame.Core;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    /// <summary>
    /// 玩家移动状态：当玩家掷骰子确定步数后进入此状态。
    /// 负责驱动玩家实体在棋盘上行走，并处理移动过程中的逻辑中断（如遇到分叉路口）。
    /// </summary>
    public class MovementState : BoardState
    {
        private readonly int _steps; // 本次移动需要走的总步数
        private BoardGameManager _context;
        private BoardMovementController _controller;

        // 缓存事件委托，确保订阅与取消订阅的是同一个引用，防止内存泄漏
        private System.Action<BoardMovementEndedEvent> _onMoveEnded;
        private System.Action<BoardForkSelectionRequestedEvent> _onForkRequested;

        /// <summary>
        /// 构造函数：初始化时传入步数。
        /// </summary>
        /// <param name="steps">玩家掷骰子得到的点数</param>
        public MovementState(int steps)
        {
            _steps = steps;
        }

        /// <summary>
        /// 进入该状态时执行：准备移动环境、订阅事件并启动位移逻辑。
        /// </summary>
        public override void OnEnter(BoardGameManager context)
        {
            _context = context;

            // 1. 基础依赖检查
            if (context.movementController == null)
            {
                Debug.LogWarning("[MovementState] 缺失 movementController，无法执行移动。");
                context.ChangeState(new PlayerTurnState()); // 容错：跳回回合开始状态
                return;
            }

            // 2. 玩家实体引用检查：如果当前控制器还没有玩家引用，尝试进行一次解析补齐
            if (context.movementController.PlayerEntity == null)
            {
                // 兜底逻辑：重新查找场景中的玩家 Token 并绑定组件
                context.movementController.ResolveReferences(-1);
            }

            // 如果还是找不到玩家实体，说明场景配置有问题
            if (context.movementController.PlayerEntity == null)
            {
                Debug.LogWarning("[MovementState] 场景中缺失玩家实体 (Player Entity)。");
                context.ChangeState(new PlayerTurnState());
                return;
            }

            _controller = context.movementController;

            // 3. 事件订阅：
            // A. 订阅移动结束事件：用于在走完全部步数后切换状态
            _onMoveEnded = OnMoveEnded;
            EventBus.Subscribe(_onMoveEnded);

            // B. 订阅分叉请求事件：这是区分“玩家”与“NPC”逻辑的关键。
            // 只有在玩家回合的 MovementState 中，才会响应此事件并弹出 UI 供人工选择。
            _onForkRequested = HandleForkSelectionRequested;
            EventBus.Subscribe(_onForkRequested);

            // 4. 执行位移：调用控制器驱动玩家实体。
            // 第三个参数 triggerNodeEvents = true，表示玩家移动时会触发格子上的各种效果。
            _controller.BeginMove(_controller.PlayerEntity, _steps, true);

            // 如果控制器未能启动移动（例如步数为 0），则清理并回退
            if (!_controller.IsMoving)
            {
                CleanupSubscriptions();
                context.ChangeState(new PlayerTurnState());
            }
        }

        /// <summary>
        /// 退出状态时执行：清理所有注册的事件。
        /// </summary>
        public override void OnExit(BoardGameManager context)
        {
            CleanupSubscriptions();
        }

        /// <summary>
        /// 处理移动结束：当控制器完成所有步数的行走后，会触发此回调。
        /// </summary>
        private void OnMoveEnded(BoardMovementEndedEvent evt)
        {
            if (_controller == null || _context == null) return;

            // 过滤事件：只响应属于当前玩家实体的结束消息
            if (evt.Entity != _controller.PlayerEntity) return;

            // 移动正常结束，清理订阅并进入结算/事件状态（EventState）
            CleanupSubscriptions();
            _context.ChangeState(new EventState());
        }

        /// <summary>
        /// 处理分叉路口：当控制器发现前方有多个地块可走时，会触发此请求。
        /// </summary>
        private void HandleForkSelectionRequested(BoardForkSelectionRequestedEvent evt)
        {
            if (_context == null)
            {
                evt.OnSelected?.Invoke(null); // 容错回调
                return;
            }

            // [核心逻辑] 弹出覆盖状态（Overlay State）：
            // Pushes 一个 ForkSelectionState，这会暂停当前的 MovementState 更新逻辑，
            // 弹出路径选择 UI，直到玩家点击确认后，通过回调通知控制器选中了哪条路。
            _context.PushOverlayState(new ForkSelectionState(evt.Node, evt.Options, result =>
            {
                // 将玩家在 UI 上选中的结果（WaypointConnection）传回给控制器
                evt.OnSelected?.Invoke(result);
            }));
        }

        /// <summary>
        /// 内部清理：安全地注销所有事件监听。
        /// </summary>
        private void CleanupSubscriptions()
        {
            if (_onMoveEnded != null)
            {
                EventBus.Unsubscribe(_onMoveEnded);
                _onMoveEnded = null;
            }
            if (_onForkRequested != null)
            {
                EventBus.Unsubscribe(_onForkRequested);
                _onForkRequested = null;
            }
        }
    }
}