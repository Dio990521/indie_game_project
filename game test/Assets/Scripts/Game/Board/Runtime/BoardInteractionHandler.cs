using System.Collections;
using IndieGame.Core;
using IndieGame.UI.Confirmation;

namespace IndieGame.Gameplay.Board.Runtime
{
    /// <summary>
    /// 棋盘交互处理器：专门负责处理实体（玩家或NPC）在到达或经过地块时的逻辑。
    /// 这种设计遵循了单一职责原则，让位移控制器（Controller）只关注坐标计算，而由此产生的游戏效果由本类处理。
    /// </summary>
    public class BoardInteractionHandler
    {
        /// <summary>
        /// 协程：处理实体到达地块的逻辑。
        /// 在位移过程中，每进入一个新的地块都会调用此方法。
        /// </summary>
        /// <param name="entity">正在移动的实体（如玩家）</param>
        /// <param name="node">当前到达或经过的地块节点</param>
        /// <param name="isFinalStep">是否是本次移动的终点（即骰子点数扣完的位置）</param>
        /// <param name="triggerNodeEvents">外部开关，控制是否要触发地块逻辑（如NPC移动时可能不触发格子效果）</param>
        public IEnumerator HandleArrival(BoardEntity entity, MapWaypoint node, bool isFinalStep, bool triggerNodeEvents)
        {
            // 1. 基础合法性检查：如果对象已销毁或数据缺失，直接中断
            if (entity == null || node == null) yield break;

            // 如果全局禁用了地块事件，或者该地块没有配置具体的 TileData，则跳过后续逻辑
            if (!triggerNodeEvents || node.tileData == null) yield break;

            // 2. 检测单位遭遇：检查当前格子上是否已经有其他的实体存在
            BoardEntity other = BoardEntityManager.Instance != null
                ? BoardEntityManager.Instance.FindOtherAtNode(node, entity)
                : null;

            if (other != null)
            {
                // 如果遇到其他单位（如路过敌人或路过队友）：
                // 先将实体的移动动画速度设为 $0$，使其看起来像是停下来交谈或战斗
                entity.SetMoveAnimationSpeed(0f);

                // 执行单位遭遇的交互逻辑并等待其完成（如触发对话或进入小游戏）
                yield return HandleEntityEncounter(entity, other, node);

                // 如果还没走到终点，则恢复动画速度继续向下一个格子迈进
                if (!isFinalStep) entity.SetMoveAnimationSpeed(1f);
            }

            // 3. 判断是否需要触发地块效果：
            // 逻辑：如果是步进的最后一跳（终点），或者该地块配置为“路过即触发”（TriggerOnPass）
            bool shouldTrigger = isFinalStep || node.tileData.TriggerOnPass;

            if (shouldTrigger)
            {
                // 在执行具体效果前，暂停移动动画，确保角色在地块中心静止执行动作
                entity.SetMoveAnimationSpeed(0f);

                // 广播“玩家抵达节点”事件，供 UI 或相机系统监听
                EventBus.Raise(new PlayerReachedNodeEvent { Node = node });

                // 调用地块数据定义的进入效果（如：加钱、扣血、触发随机事件）
                node.tileData.OnEnter(entity.gameObject);
            }

            // 4. 处理确认弹窗拦截：
            // 如果地块效果触发了一个确认弹窗（例如：“是否花费 100 金币购买此地？”）
            if (ConfirmationEvent.HasPending)
            {
                // 我们必须阻塞位移协程，直到玩家点击了“确定”或“取消”
                bool responded = false;

                // 定义临时的响应回调
                void OnResponded(ConfirmationRespondedEvent _) => responded = true;

                // 订阅响应事件
                EventBus.Subscribe<ConfirmationRespondedEvent>(OnResponded);

                // 在响应发生前，通过 yield return null 让出执行权，每帧轮询一次
                while (!responded)
                {
                    yield return null;
                }

                // 收到响应后，务必注销订阅，防止内存泄漏和重复触发
                EventBus.Unsubscribe<ConfirmationRespondedEvent>(OnResponded);
            }

            // 5. 恢复移动状态：
            // 如果这只是路径中的一个中间点，且交互已处理完毕，将动画速度设回 $1$，继续前进
            if (!isFinalStep) entity.SetMoveAnimationSpeed(1f);
        }

        /// <summary>
        /// 协程：处理两个实体在同一格相遇的情况。
        /// </summary>
        /// <param name="player">发起移动的主体</param>
        /// <param name="target">被遇到的目标实体</param>
        /// <param name="node">相遇的地块</param>
        private IEnumerator HandleEntityEncounter(BoardEntity player, BoardEntity target, MapWaypoint node)
        {
            bool completed = false;

            // 构建交互事件包
            BoardEntityInteractionEvent evt = new BoardEntityInteractionEvent
            {
                Player = player,
                Target = target,
                Node = node,
                // 当交互逻辑彻底执行完（如战斗结束）时，调用此回调
                OnCompleted = () => completed = true
            };

            // 如果当前没有任何系统（如战斗系统）订阅此交互事件，则直接标记为完成
            if (!EventBus.HasSubscribers<BoardEntityInteractionEvent>())
            {
                completed = true;
            }

            // 发送事件，由外部逻辑（如战斗系统）接管流程
            EventBus.Raise(evt);

            // 持续挂起当前协程，直到外部系统调用了 OnCompleted
            while (!completed)
            {
                yield return null;
            }
        }
    }
}