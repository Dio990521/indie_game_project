using IndieGame.Core;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    public class InitState : BoardState
    {
        public override void OnEnter(BoardGameManager context)
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.LastBoardIndex >= 0 && context.movementController != null)
            {
                context.movementController.SetCurrentNodeById(gm.LastBoardIndex);
            }
            else
            {
                context.ResetToStart();
            }
        }

        public override void OnUpdate(BoardGameManager context)
        {
            // 在第一帧更新时切换状态，此时状态机的 _isTransitioning 已重置为 false
            context.ChangeState(new PlayerTurnState());
        }
    }
}