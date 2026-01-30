using IndieGame.Core;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    public class InitState : BoardState
    {
        public override void OnEnter(BoardGameManager context)
        {
            if (context.movementController != null && context.movementController.CurrentNodeId >= 0)
            {
                // 已有有效节点时不强制重置
                return;
            }
            context.ResetToStart();
        }

        public override void OnUpdate(BoardGameManager context)
        {
            // 在第一帧更新时切换状态，此时状态机的 _isTransitioning 已重置为 false
            context.ChangeState(new PlayerTurnState());
        }
    }
}
