namespace IndieGame.Gameplay.Board.Runtime.States
{
    public class EventState : BoardState
    {
        public EventState(BoardGameManager context) : base(context) { }

        public override void Enter()
        {
            if (!IndieGame.UI.Confirmation.ConfirmationEvent.HasPending)
            {
                Context.ChangeState(new PlayerTurnState(Context));
                return;
            }

            IndieGame.UI.Confirmation.ConfirmationEvent.OnResponded += HandleResponse;
        }

        public override void Exit()
        {
            IndieGame.UI.Confirmation.ConfirmationEvent.OnResponded -= HandleResponse;
        }

        private void HandleResponse(bool confirmed)
        {
            Context.ChangeState(new PlayerTurnState(Context));
        }
    }
}
