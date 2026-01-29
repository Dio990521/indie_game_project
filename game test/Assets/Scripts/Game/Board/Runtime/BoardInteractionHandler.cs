using System.Collections;
using IndieGame.Core;
using IndieGame.UI.Confirmation;

namespace IndieGame.Gameplay.Board.Runtime
{
    // 负责节点到达/交互逻辑，Controller 只关心位移。
    public class BoardInteractionHandler
    {
        public IEnumerator HandleArrival(BoardEntity entity, MapWaypoint node, bool isFinalStep, bool triggerNodeEvents)
        {
            if (entity == null || node == null) yield break;
            if (!triggerNodeEvents || node.tileData == null) yield break;

            BoardEntity other = BoardEntity.FindOtherAtNode(node, entity);
            if (other != null)
            {
                // 遇到其他单位时先暂停移动并处理交互
                entity.SetMoveAnimationSpeed(0f);
                yield return HandleEntityEncounter(entity, other, node);
                if (!isFinalStep) entity.SetMoveAnimationSpeed(1f);
            }

            bool shouldTrigger = isFinalStep || node.tileData.TriggerOnPass;
            if (shouldTrigger) entity.SetMoveAnimationSpeed(0f);

            if (shouldTrigger)
            {
                // 广播抵达事件并触发格子效果
                EventBus.Raise(new PlayerReachedNodeEvent { Node = node });
                node.tileData.OnEnter(entity.gameObject);
            }

            if (ConfirmationEvent.HasPending)
            {
                // 等待确认弹窗响应，避免移动过程中继续推进
                bool responded = false;
                void OnResponded(ConfirmationRespondedEvent _) => responded = true;
                EventBus.Subscribe<ConfirmationRespondedEvent>(OnResponded);
                while (!responded)
                {
                    yield return null;
                }
                EventBus.Unsubscribe<ConfirmationRespondedEvent>(OnResponded);
            }

            if (!isFinalStep) entity.SetMoveAnimationSpeed(1f);
        }

        private IEnumerator HandleEntityEncounter(BoardEntity player, BoardEntity target, MapWaypoint node)
        {
            bool completed = false;
            BoardEntityInteractionEvent evt = new BoardEntityInteractionEvent
            {
                Player = player,
                Target = target,
                Node = node,
                OnCompleted = () => completed = true
            };

            if (!EventBus.HasSubscribers<BoardEntityInteractionEvent>())
            {
                completed = true;
            }

            EventBus.Raise(evt);
            while (!completed)
            {
                yield return null;
            }
        }
    }
}
