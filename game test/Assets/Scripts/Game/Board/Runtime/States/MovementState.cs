using UnityEngine;
using IndieGame.Core;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    public class MovementState : BoardState
    {
        private readonly int _steps;

        public MovementState(BoardGameManager context, int steps) : base(context)
        {
            _steps = steps;
        }

        public override void Enter()
        {
            if (Context.movementController == null)
            {
                Debug.LogWarning("[MovementState] Missing movementController.");
                Context.ChangeState(new PlayerTurnState(Context));
                return;
            }

            Context.movementController.MoveEnded += HandleMoveEnded;
            Context.movementController.ForkSelectionRequested += HandleForkSelectionRequested;
            Context.movementController.BeginMove(_steps);
        }

        public override void Exit()
        {
            if (Context.movementController != null)
            {
                Context.movementController.MoveEnded -= HandleMoveEnded;
                Context.movementController.ForkSelectionRequested -= HandleForkSelectionRequested;
            }
        }

        private void HandleMoveEnded()
        {
            Context.ChangeState(new EventState(Context));
        }

        private void HandleForkSelectionRequested(MapWaypoint forkNode, System.Action<WaypointConnection> onSelected)
        {
            Context.PushOverlayState(new ForkSelectionState(Context, forkNode, onSelected));
        }
    }
}
