using System.Collections;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    public class EnemyTurnState : BoardState
    {
        private Coroutine _routine;

        public override void OnEnter(BoardGameManager context)
        {
            _routine = context.StartCoroutine(RunEnemyTurn(context));
        }

        public override void OnExit(BoardGameManager context)
        {
            if (_routine != null)
            {
                context.StopCoroutine(_routine);
                _routine = null;
            }
        }

        private IEnumerator RunEnemyTurn(BoardGameManager context)
        {
            BoardEntity npc = BoardEntity.FindFirstNpc();
            if (npc == null)
            {
                context.ChangeState(new PlayerTurnState());
                yield break;
            }

            int steps = Random.Range(1, 7);
            Debug.Log($"<color=orange>🤖 NPC 回合掷骰子: {steps}</color>");

            npc.MoveTo(steps);
            yield return new WaitUntil(() => npc == null || !npc.IsMoving);

            context.ChangeState(new PlayerTurnState());
        }
    }
}
