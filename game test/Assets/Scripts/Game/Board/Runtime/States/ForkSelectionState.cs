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

        public ForkSelectionState(MapWaypoint forkNode, System.Action<WaypointConnection> onSelected)
        {
            _forkNode = forkNode;
            _onSelected = onSelected;
        }

        public override void OnEnter(BoardGameManager context)
        {
            if (context.movementController == null || context.movementController.forkSelector == null)
            {
                _onSelected?.Invoke(null);
                context.PopOverlayState();
                return;
            }

            _routine = context.StartCoroutine(SelectRoutine(context));
        }

        public override void OnExit(BoardGameManager context)
        {
            if (_routine != null)
            {
                context.StopCoroutine(_routine);
                _routine = null;
            }
        }

        private IEnumerator SelectRoutine(BoardGameManager context)
        {
            WaypointConnection selected = null;
            yield return context.movementController.forkSelector.SelectConnection(_forkNode, result => selected = result);
            _onSelected?.Invoke(selected);
            context.PopOverlayState();
        }
    }
}
