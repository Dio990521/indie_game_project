namespace IndieGame.Gameplay.Board.Runtime.States
{
    public class EventState : BoardState
    {
        private System.Action<bool> _onResponded;

        public override void OnEnter(BoardGameManager context)
        {
            if (!IndieGame.UI.Confirmation.ConfirmationEvent.HasPending)
            {
                context.ChangeState(new PlayerTurnState());
                return;
            }

            _onResponded = _ => context.ChangeState(new PlayerTurnState());
            IndieGame.UI.Confirmation.ConfirmationEvent.OnResponded += _onResponded;
        }

        public override void OnExit(BoardGameManager context)
        {
            if (_onResponded != null)
            {
                IndieGame.UI.Confirmation.ConfirmationEvent.OnResponded -= _onResponded;
            }
            _onResponded = null;
        }
    }
}
