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
            context.ChangeState(new PlayerTurnState());
        }
    }
}
