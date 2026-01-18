using IndieGame.Core;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    public class InitState : BoardState
    {
        public InitState(BoardGameManager context) : base(context) { }

        public override void Enter()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.LastBoardIndex >= 0 && Context.movementController != null)
            {
                Context.movementController.SetCurrentNodeById(gm.LastBoardIndex);
            }
            else
            {
                Context.ResetToStart();
            }
            Context.ChangeState(new PlayerTurnState(Context));
        }
    }
}
