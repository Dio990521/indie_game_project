using UnityEngine;
using IndieGame.Core;

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
            if (Context.actionMenu != null)
            {
                Context.actionMenu.SetAllowShow(false);
            }

            if (Context.movementController == null)
            {
                Debug.LogWarning("[MovementState] Missing movementController.");
                Context.ChangeState(new PlayerTurnState(Context));
                return;
            }

            Context.movementController.MoveEnded += HandleMoveEnded;
            Context.movementController.BeginMove(_steps);
        }

        public override void Exit()
        {
            if (Context.movementController != null)
            {
                Context.movementController.MoveEnded -= HandleMoveEnded;
            }
        }

        private void HandleMoveEnded()
        {
            Context.ChangeState(new EventState(Context));
        }
    }
}
