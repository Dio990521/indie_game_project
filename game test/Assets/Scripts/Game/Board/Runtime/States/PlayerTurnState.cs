using UnityEngine;
using IndieGame.Core;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    public class PlayerTurnState : BoardState
    {
        public PlayerTurnState(BoardGameManager context) : base(context) { }

        public override void Enter()
        {
            if (Context.actionMenu != null)
            {
                Context.actionMenu.SetAllowShow(true);
            }
        }

        public override void Exit()
        {
            if (Context.actionMenu != null)
            {
                Context.actionMenu.SetAllowShow(false);
            }
        }

        public override void OnInteract()
        {
            if (GameManager.Instance.CurrentState != GameState.BoardMode) return;
            if (Context.movementController == null || Context.movementController.IsMoving) return;

            int steps = Random.Range(1, 7);
            Debug.Log($"<color=cyan>🎲 掷骰子: {steps}</color>");
            Context.ChangeState(new MovementState(Context, steps));
        }
    }
}
