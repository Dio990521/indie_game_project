using System.Collections;
using UnityEngine;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    public class ForkSelectionState : BoardState
    {
        private readonly MapWaypoint _forkNode;
        private readonly System.Action<WaypointConnection> _onSelected;
        private Coroutine _routine;

        public ForkSelectionState(BoardGameManager context, MapWaypoint forkNode, System.Action<WaypointConnection> onSelected) : base(context)
        {
            _forkNode = forkNode;
            _onSelected = onSelected;
        }

        public override void Enter()
        {
            if (Context.movementController == null || Context.movementController.forkSelector == null)
            {
                _onSelected?.Invoke(null);
                Context.PopOverlayState();
                return;
            }

            _routine = Context.StartCoroutine(SelectRoutine());
        }

        public override void Exit()
        {
            if (_routine != null)
            {
                Context.StopCoroutine(_routine);
                _routine = null;
            }
        }

        private IEnumerator SelectRoutine()
        {
            WaypointConnection selected = null;
            yield return Context.movementController.forkSelector.SelectConnection(_forkNode, result => selected = result);
            _onSelected?.Invoke(selected);
            Context.PopOverlayState();
        }
    }
}
