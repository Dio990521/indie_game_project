using UnityEngine;
using IndieGame.Core;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    /// <summary>
    /// NPC/敌人回合状态：处理棋盘上非玩家实体的行动逻辑。
    /// 该状态负责查找 NPC、分配随机（或固定）步数、驱动移动并监听移动结束以交还控制权。
    /// </summary>
    public class EnemyTurnState : BoardState
    {
        // 缓存上下文和控制器引用
        private BoardGameManager _context;
        private BoardMovementController _controller;

        // 当前执行行动的 NPC 实体
        private BoardEntity _npc;

        // 缓存移动结束的监听委托，以便后续精准注销
        private System.Action<BoardMovementEndedEvent> _onMoveEnded;

        /// <summary>
        /// 进入该状态时执行：初始化 NPC 移动流程。
        /// </summary>
        /// <param name="context">棋盘游戏管理器上下文</param>
        public override void OnEnter(BoardGameManager context)
        {
            _context = context;
            _controller = context.movementController;

            // 1. 获取 NPC：从实体管理器中查找第一个 NPC。
            // 如果场景中没有 NPC，则视为该阶段不存在，直接跳过。
            _npc = BoardEntityManager.Instance != null
                ? BoardEntityManager.Instance.FindFirstNpc()
                : null;

            if (_npc == null)
            {
                // [容错] 没有 NPC 时直接跳过敌人回合，回到玩家回合
                context.ChangeState(new PlayerTurnState());
                return;
            }

            // 2. 依赖检查：确保移动控制器可用
            if (_controller == null)
            {
                Debug.LogWarning("[EnemyTurnState] 缺失 movementController，无法驱动 NPC。");
                context.ChangeState(new PlayerTurnState());
                return;
            }

            // 3. 步数分配：目前硬编码为 1 步。未来可在此接入随机数或 AI 逻辑。
            int steps = 1;
            Debug.Log("<color=orange>🤖 NPC 回合移动: 1</color>");

            // 4. 事件订阅：准备监听移动结束事件
            _onMoveEnded = OnMoveEnded;
            EventBus.Subscribe(_onMoveEnded);

            // 5. 驱动移动：
            // 参数说明：目标实体为 _npc，步数为 steps，triggerNodeEvents 为 false。
            // 这里设置为 false 是为了防止 NPC 在路过或停下时触发玩家专属的奖励/惩罚格子效果。
            _controller.BeginMove(_npc, steps, false);

            // 如果 BeginMove 内部由于某种原因（如路径阻塞）未能启动移动，则立即清理并退出
            if (!_controller.IsMoving)
            {
                CleanupSubscriptions();
                context.ChangeState(new PlayerTurnState());
            }
        }

        /// <summary>
        /// 退出该状态时执行：确保清理残留的事件订阅。
        /// </summary>
        public override void OnExit(BoardGameManager context)
        {
            CleanupSubscriptions();
        }

        /// <summary>
        /// 当移动控制器完成位移任务时触发。
        /// </summary>
        /// <param name="evt">移动结束事件数据</param>
        private void OnMoveEnded(BoardMovementEndedEvent evt)
        {
            if (_context == null) return;

            // 重要：只响应属于当前正在行动的 NPC 的结束事件，防止逻辑混淆
            if (evt.Entity != _npc) return;

            // 敌人行动完毕，清理监听并切换回玩家回合
            CleanupSubscriptions();
            _context.ChangeState(new PlayerTurnState());
        }

        /// <summary>
        /// 内部清理方法：注销事件总线的监听，防止内存泄漏或在后续状态中触发错误的逻辑。
        /// </summary>
        private void CleanupSubscriptions()
        {
            if (_onMoveEnded != null)
            {
                EventBus.Unsubscribe(_onMoveEnded);
                _onMoveEnded = null;
            }
        }
    }
}