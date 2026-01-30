using UnityEngine;
using IndieGame.Core;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    public class MovementState : BoardState
    {
        private readonly int _steps;
        private BoardGameManager _context;
        private BoardMovementController _controller;
        private System.Action<BoardMovementEndedEvent> _onMoveEnded;
        private System.Action<MapWaypoint, System.Collections.Generic.List<WaypointConnection>, System.Action<WaypointConnection>> _onForkRequested;

        public MovementState(int steps)
        {
            _steps = steps;
        }

        public override void OnEnter(BoardGameManager context)
        {
            _context = context;
            if (context.movementController == null)
            {
                Debug.LogWarning("[MovementState] Missing movementController.");
                context.ChangeState(new PlayerTurnState());
                return;
            }

            if (context.movementController.PlayerEntity == null)
            {
                // 兜底补齐玩家实体引用
                context.movementController.ResolveReferences(-1);
            }

            if (context.movementController.PlayerEntity == null)
            {
                Debug.LogWarning("[MovementState] Missing player entity.");
                context.ChangeState(new PlayerTurnState());
                return;
            }

            _controller = context.movementController;

            _onMoveEnded = OnMoveEnded;
            EventBus.Subscribe(_onMoveEnded);

            _onForkRequested = HandleForkSelectionRequested;
            _controller.ForkSelectionRequested += _onForkRequested;

            // 交由控制器统一执行步数与选路逻辑
            _controller.BeginMove(_controller.PlayerEntity, _steps, true);
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
            if (_controller == null || _context == null) return;
            if (evt.Entity != _controller.PlayerEntity) return;

            CleanupSubscriptions();
            _context.ChangeState(new EventState());
        }

        private void HandleForkSelectionRequested(
            MapWaypoint node,
            System.Collections.Generic.List<WaypointConnection> options,
            System.Action<WaypointConnection> onSelected)
        {
            if (_context == null)
            {
                onSelected?.Invoke(null);
                return;
            }

            _context.PushOverlayState(new ForkSelectionState(node, options, result =>
            {
                onSelected?.Invoke(result);
            }));
        }

        private void CleanupSubscriptions()
        {
            if (_onMoveEnded != null)
            {
                EventBus.Unsubscribe(_onMoveEnded);
                _onMoveEnded = null;
            }
            if (_controller != null && _onForkRequested != null)
            {
                _controller.ForkSelectionRequested -= _onForkRequested;
                _onForkRequested = null;
            }
        }
    }
}
