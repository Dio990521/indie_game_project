using System.Collections;
using UnityEngine;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    /// <summary>
    /// 分叉路口选择状态：这是一个覆盖型状态（Overlay State）。
    /// 当玩家移动到拥有多个出口的节点时，主位移逻辑会暂停，并推入此状态。
    /// 它的职责是驱动 UI 和输入逻辑，直到玩家选定一条前进路线。
    /// </summary>
    public class ForkSelectionState : BoardState
    {
        // 发生分叉的起始节点
        private readonly MapWaypoint _forkNode;
        // 可供选择的路径连接列表（可选，若为空则默认使用节点的全部连接）
        private readonly System.Collections.Generic.List<WaypointConnection> _options;
        // 选择完成后执行的回调动作（通常是将选中的连接传回移动控制器）
        private readonly System.Action<WaypointConnection> _onSelected;
        // 当前正在运行的选择协程引用，用于生命周期管理
        private Coroutine _routine;

        /// <summary>
        /// 构造函数 A：使用节点的默认所有连接作为选项。
        /// </summary>
        public ForkSelectionState(MapWaypoint forkNode, System.Action<WaypointConnection> onSelected)
        {
            _forkNode = forkNode;
            _onSelected = onSelected;
        }

        /// <summary>
        /// 构造函数 B：允许传入经过筛选或特殊处理的连接列表。
        /// </summary>
        public ForkSelectionState(MapWaypoint forkNode, System.Collections.Generic.List<WaypointConnection> options, System.Action<WaypointConnection> onSelected)
        {
            _forkNode = forkNode;
            _options = options;
            _onSelected = onSelected;
        }

        /// <summary>
        /// 当状态被推入状态机并激活时执行。
        /// </summary>
        /// <param name="context">棋盘游戏管理器上下文</param>
        public override void OnEnter(BoardGameManager context)
        {
            // 1. 依赖性安全检查：如果缺少移动控制器或选择器组件
            if (context.movementController == null || context.movementController.forkSelector == null)
            {
                Debug.LogError("[ForkSelectionState] 找不到所需的 BoardForkSelector 组件。");
                // 直接返回空结果，避免流程卡死
                _onSelected?.Invoke(null);
                // 立即退出当前的覆盖状态
                context.PopOverlayState();
                return;
            }

            // 2. 开启异步选择流程：进入协程，开始处理玩家输入和 UI 表现
            _routine = context.StartCoroutine(SelectRoutine(context));
        }

        /// <summary>
        /// 当状态被弹出或强制切换时执行。
        /// </summary>
        public override void OnExit(BoardGameManager context)
        {
            // 如果协程仍在运行，则强制停止，防止回调在状态退出后被触发
            if (_routine != null)
            {
                context.StopCoroutine(_routine);
                _routine = null;
            }
        }

        /// <summary>
        /// 内部协程：具体的选择等待逻辑。
        /// </summary>
        private IEnumerator SelectRoutine(BoardGameManager context)
        {
            WaypointConnection selected = null;

            // 3. 根据是否存在自定义选项列表，调用选择器的不同重载方法
            if (_options != null)
            {
                // 使用指定的候选列表进行 UI 展示和选择
                yield return context.movementController.forkSelector.SelectConnection(
                    _forkNode,
                    _options,
                    result => selected = result
                );
            }
            else
            {
                // 使用路点节点自带的连接列表进行选择
                yield return context.movementController.forkSelector.SelectConnection(
                    _forkNode,
                    result => selected = result
                );
            }

            // 4. 选择完成：执行外部传入的回调函数，通知移动控制器继续后续位移
            _onSelected?.Invoke(selected);

            // 5. 任务完成：从管理器中弹出本覆盖状态，恢复底层（位移状态）的正常更新
            context.PopOverlayState();
        }
    }
}