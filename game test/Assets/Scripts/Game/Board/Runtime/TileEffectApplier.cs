using System.Collections.Generic;
using IndieGame.Core;

namespace IndieGame.Gameplay.Board.Runtime
{
    /// <summary>
    /// 格子效果应用工具（纯函数）：
    /// <para>
    /// 把 BoardMovementController.HandleSegmentCompleted 协程中"消费 _fx 状态 / 修改剩余步数 /
    /// 更新连锁计数"这部分从协程内剥离，集中到本类作为静态方法。这样：
    /// </para>
    /// <para>
    /// 1) HandleSegmentCompleted 协程主体更短，专注于流程编排（终止判断 / 协程串联）；<br/>
    /// 2) 各 Apply 方法都是无 yield 的同步函数，便于单元测试；<br/>
    /// 3) 新增格子效果时，只需在本类追加 ApplyXxx 静态方法，调用方按相同顺序串接即可。
    /// </para>
    /// <para>
    /// 与 BoardMovementController 之间通过 <c>ref</c> 参数共享状态：
    /// - <c>ref TileEffectPendingState fx</c>：消费/修改格子待执行效果；
    /// - <c>ref int stepsRemaining</c>：直接修改 controller 的剩余步数；
    /// - <c>BoardEntity activeEntity</c>：在反向 / 掉头场景下调用其实体方法；
    /// - <c>out bool shouldAllowFirstStepUTurn</c>：把"是否需要触发首步掉头许可"反馈给 controller。
    /// </para>
    /// </summary>
    internal static class TileEffectApplier
    {
        /// <summary>
        /// [扭曲格] 强制滑行效果应用：
        /// - 最终落点时（isFinalStep=true）：进入连锁计数，并确保 stepsRemaining 至少为 1
        ///   （让 AdvanceToNextStep 还能读取一次方向锁）；
        /// - 路过时（isFinalStep=false）：丢弃方向锁。
        /// </summary>
        public static void ApplyForcedNext(ref TileEffectPendingState fx, ref int stepsRemaining, bool isFinalStep)
        {
            if (fx.ForcedNextNodeId < 0) return;

            if (isFinalStep)
            {
                ComboMoveSystem.IncrementCombo(); // 扭曲格触发强制滑行，进入连锁
                if (stepsRemaining <= 0) stepsRemaining = 1;
            }
            else
            {
                fx.ForcedNextNodeId = -1; // 路过：丢弃方向锁（分叉过滤仍有效）
            }
        }

        /// <summary>
        /// [前进/后退格] 消费额外步数效果：
        /// - 正值：追加到 stepsRemaining；
        /// - 负值：重置 stepsRemaining 为反向步数，并通过 ReverseDirection 或
        ///   首步掉头许可（由 shouldAllowFirstStepUTurn 反馈给 controller）实现方向反转。
        /// </summary>
        public static void ApplyExtraSteps(
            ref TileEffectPendingState fx,
            ref int stepsRemaining,
            BoardEntity activeEntity,
            out bool shouldAllowFirstStepUTurn)
        {
            shouldAllowFirstStepUTurn = false;

            if (fx.ExtraSteps == 0) return;

            int extra = fx.ExtraSteps;
            fx.ExtraSteps = 0;
            ComboMoveSystem.IncrementCombo(); // 前进/后退格触发额外位移，进入连锁

            if (extra > 0)
            {
                // 前进：追加步数，自然向前继续
                stepsRemaining += extra;
                return;
            }

            // 后退：重置为反向步数，再通过掉头实现方向反转
            stepsRemaining = -extra;
            if (IsAtDeadEnd(activeEntity))
            {
                // 死胡同节点：正向出口就是来路，需要 controller 设置首步掉头许可
                shouldAllowFirstStepUTurn = true;
            }
            else
            {
                // 普通节点：原地掉头，让 GetValidNextNodes 自然返回反向路径
                activeEntity.ReverseDirection();
            }
        }

        /// <summary>
        /// [方向格] 应用首步强制方向 + 指定步数：
        /// 仅最终落点生效；路过时丢弃。
        /// </summary>
        public static void ApplyDirectionalSteps(ref TileEffectPendingState fx, ref int stepsRemaining, bool isFinalStep)
        {
            if (fx.DirectionalSteps <= 0) return;

            if (isFinalStep)
            {
                ComboMoveSystem.IncrementCombo(); // 方向格触发强制移动，进入连锁
                stepsRemaining = fx.DirectionalSteps;
                fx.ForcedNextNodeId = fx.DirectionalNodeId;
            }
            // 不论是否最终落点，都消耗 DirectionalSteps（路过时即丢弃）
            fx.DirectionalSteps = 0;
            fx.DirectionalNodeId = -1;
        }

        /// <summary>
        /// [不动铃铛] 清空所有位移类效果：
        /// 强制玩家停在骰子点数对应的格子上，忽略大炮、传送、行进、扭曲格等所有位移效果。
        /// </summary>
        public static void ClearAllMovementEffects(ref TileEffectPendingState fx)
        {
            fx.ExtraSteps = 0;
            fx.ForcedNextNodeId = -1;
            fx.DirectionalSteps = 0;
            fx.DirectionalNodeId = -1;
            fx.CannonLaunch = false;
            fx.Teleport = false;
        }

        /// <summary>
        /// 判断实体当前是否停在死胡同（唯一可走方向是原路返回）。
        /// 移出 BoardMovementController 后，BeginMove 与 ReversePlayerDirection 复用此方法。
        /// </summary>
        public static bool IsAtDeadEnd(BoardEntity entity)
        {
            if (entity == null || entity.CurrentNode == null) return false;
            List<MapWaypoint> validNodes = entity.CurrentNode.GetValidNextNodes(entity.LastWaypoint);
            return validNodes.Count == 1 && entity.LastWaypoint != null && validNodes[0] == entity.LastWaypoint;
        }
    }
}
