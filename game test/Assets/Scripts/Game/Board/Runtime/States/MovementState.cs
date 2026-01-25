using System.Collections;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    public class MovementState : BoardState
    {
        private readonly int _steps;
        private Coroutine _routine;

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

            if (context.movementController.PlayerEntity == null)
            {
                context.movementController.ResolveReferences(-1);
            }

            if (context.movementController.PlayerEntity == null)
            {
                Debug.LogWarning("[MovementState] Missing player entity.");
                context.ChangeState(new PlayerTurnState());
                return;
            }

            _routine = context.StartCoroutine(MoveRoutine(context));
        }

        public override void OnExit(BoardGameManager context)
        {
            if (_routine != null)
            {
                context.StopCoroutine(_routine);
                _routine = null;
            }
            if (context.movementController != null)
            {
                context.movementController.EndDirectedMove();
            }
        }

        private IEnumerator MoveRoutine(BoardGameManager context)
        {
            BoardMovementController controller = context.movementController;
            BoardEntity entity = controller.PlayerEntity;

            controller.BeginDirectedMove(entity, true);

            int stepsRemaining = _steps;
            while (stepsRemaining > 0)
            {
                MapWaypoint current = entity.CurrentWaypoint;
                if (current == null) break;

                System.Collections.Generic.List<MapWaypoint> validNodes = current.GetValidNextNodes(entity.LastWaypoint);
                if (validNodes.Count == 0) break;

                WaypointConnection selectedConnection = null;

                if (validNodes.Count == 1)
                {
                    selectedConnection = current.GetConnectionTo(validNodes[0]);
                }
                else
                {
                    System.Collections.Generic.List<WaypointConnection> options = current.GetConnectionsTo(validNodes);
                    bool resolved = false;
                    entity.SetMoveAnimationSpeed(0f);

                    context.PushOverlayState(new ForkSelectionState(current, options, result =>
                    {
                        selectedConnection = result;
                        resolved = true;
                    }));

                    yield return new WaitUntil(() => resolved || context.OverlayState == null || !context.isActiveAndEnabled);
                }

                if (selectedConnection == null) break;

                yield return controller.MoveActiveEntityAlongConnection(selectedConnection, stepsRemaining == 1);
                stepsRemaining--;
            }

            controller.EndDirectedMove();
            context.ChangeState(new EventState());
        }
    }
}
