using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;

namespace IndieGame.Gameplay.Board.Runtime
{
    public class BoardGameManager : MonoSingleton<BoardGameManager>
    {
        [Header("Dependencies")]
        public BoardMovementController movementController;

        [ContextMenu("Roll Dice")]
        public void RollDice()
        {
             if (GameManager.Instance.CurrentState != GameState.BoardMode) return;
             if (movementController == null || movementController.IsMoving) return;

             int steps = Random.Range(1, 7);
             Debug.Log($"<color=cyan>🎲 掷骰子: {steps}</color>");
             movementController.BeginMove(steps);
        }
        public void ResetToStart()
        {
            if (movementController != null)
            {
                movementController.ResetToStart();
            }
        }
    }
}
