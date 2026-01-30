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
            BoardEntity npc = BoardEntityManager.Instance != null
                ? BoardEntityManager.Instance.FindFirstNpc()
                : null;
            if (npc == null)
            {
                // 没有 NPC 时直接回到玩家回合
                context.ChangeState(new PlayerTurnState());
                yield break;
            }

            int steps = 1;
            Debug.Log("<color=orange>🤖 NPC 回合移动: 1</color>");

            if (context.movementController != null)
            {
                // 使用同一套移动控制器，避免逻辑分叉
                context.movementController.BeginMove(npc, steps, false);
                yield return new WaitUntil(() => context.movementController == null || !context.movementController.IsMoving);
            }
            else
            {
                // 兜底使用实体自身移动
                npc.MoveTo(steps);
                yield return new WaitUntil(() => npc == null || !npc.IsMoving);
            }

            // 敌方回合结束切回玩家回合
            context.ChangeState(new PlayerTurnState());
        }
    }
}
