using UnityEngine;
using IndieGame.Core;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    public class MovementState : BoardState
    {
        private readonly int _steps;
        private System.Action _onMoveEnded;
        private System.Action<MapWaypoint, System.Action<WaypointConnection>> _onForkSelection;

        public MovementState(int steps)
        {
            _steps = steps;
        }

        public override void OnEnter(BoardGameManager context)
        {
            if (context.movementController == null)
            {
                Debug.LogWarning("[MovementState] Missing movementController.");
                context.ChangeState(new PlayerTurnState());
                return;
            }

            _onMoveEnded = () => context.ChangeState(new EventState());
            _onForkSelection = (node, onSelected) =>
                context.PushOverlayState(new ForkSelectionState(node, onSelected));

            context.movementController.MoveEnded += _onMoveEnded;
            context.movementController.ForkSelectionRequested += _onForkSelection;
            context.movementController.BeginMove(_steps);
        }

        public override void OnExit(BoardGameManager context)
        {
            if (context.movementController != null)
            {
                if (_onMoveEnded != null) context.movementController.MoveEnded -= _onMoveEnded;
                if (_onForkSelection != null) context.movementController.ForkSelectionRequested -= _onForkSelection;
            }
            _onMoveEnded = null;
            _onForkSelection = null;
        }
    }
}
