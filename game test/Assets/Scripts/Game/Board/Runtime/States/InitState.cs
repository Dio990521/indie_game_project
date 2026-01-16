using IndieGame.Core;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    public class InitState : BoardState
    {
        public InitState(BoardGameManager context) : base(context) { }

        public override void Enter()
        {
            Context.ResetToStart();
            if (Context.actionMenu != null)
            {
                Context.actionMenu.SetAllowShow(false);
            }
            Context.ChangeState(new PlayerTurnState(Context));
        }
    }
}
