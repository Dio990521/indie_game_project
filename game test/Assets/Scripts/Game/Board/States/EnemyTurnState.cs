using UnityEngine;
using IndieGame.Core;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    public class EnemyTurnState : BoardState
    {
        private BoardGameManager _context;
        private BoardMovementController _controller;
        private BoardEntity _npc;
        private System.Action<BoardMovementEndedEvent> _onMoveEnded;

        public override void OnEnter(BoardGameManager context)
        {
            _context = context;
            _controller = context.movementController;
            _npc = BoardEntityManager.Instance != null
                ? BoardEntityManager.Instance.FindFirstNpc()
                : null;

            if (_npc == null)
            {
                // 没有 NPC 时直接回到玩家回合
                context.ChangeState(new PlayerTurnState());
                return;
            }

            if (_controller == null)
            {
                Debug.LogWarning("[EnemyTurnState] Missing movementController.");
                context.ChangeState(new PlayerTurnState());
                return;
            }

            int steps = 1;
            Debug.Log("<color=orange>🤖 NPC 回合移动: 1</color>");

            _onMoveEnded = OnMoveEnded;
            EventBus.Subscribe(_onMoveEnded);

            _controller.BeginMove(_npc, steps, false);
            if (!_controller.IsMoving)
            {
                CleanupSubscriptions();
                context.ChangeState(new PlayerTurnState());
            }
        }

        public override void OnExit(BoardGameManager context)
        {
            CleanupSubscriptions();
        }

        private void OnMoveEnded(BoardMovementEndedEvent evt)
        {
            if (_context == null) return;
            if (evt.Entity != _npc) return;

            CleanupSubscriptions();
            _context.ChangeState(new PlayerTurnState());
        }

        private void CleanupSubscriptions()
        {
            if (_onMoveEnded != null)
            {
                EventBus.Unsubscribe(_onMoveEnded);
                _onMoveEnded = null;
            }
        }
    }
}
