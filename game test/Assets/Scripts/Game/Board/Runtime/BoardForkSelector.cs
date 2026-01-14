using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core.Input;
using IndieGame.Gameplay.Board.View;

namespace IndieGame.Gameplay.Board.Runtime
{
    public class BoardForkSelector : MonoBehaviour
    {
        [Header("Dependencies")]
        public GameInputReader inputReader;
        public BoardViewHelper viewHelper;

        [Header("Settings")]
        public float inputDelay = 0.2f;

        private bool _interactTriggered = false;

        private void OnEnable()
        {
            if (inputReader != null) inputReader.InteractEvent += OnInteractInput;
        }

        private void OnDisable()
        {
            if (inputReader != null) inputReader.InteractEvent -= OnInteractInput;
        }

        private void OnInteractInput()
        {
            _interactTriggered = true;
        }

        public void ClearSelection()
        {
            _interactTriggered = false;
            if (viewHelper != null) viewHelper.ClearCursors();
        }

        public IEnumerator SelectConnection(MapWaypoint forkNode, System.Action<WaypointConnection> onSelected)
        {
            if (forkNode == null || forkNode.connections == null || forkNode.connections.Count == 0)
            {
                onSelected?.Invoke(null);
                yield break;
            }

            if (inputReader == null || viewHelper == null)
            {
                Debug.LogWarning("[BoardForkSelector] Missing inputReader or viewHelper.");
                onSelected?.Invoke(null);
                yield break;
            }

            List<WaypointConnection> options = forkNode.connections;
            int currentIndex = 0;
            bool selected = false;

            _interactTriggered = false;

            viewHelper.ShowCursors(options, forkNode.transform.position);
            viewHelper.HighlightCursor(currentIndex);

            float nextInputTime = 0f;

            yield return null;

            while (!selected)
            {
                Vector2 moveInput = inputReader.CurrentMoveInput;
                if (Time.time > nextInputTime && Mathf.Abs(moveInput.x) > 0.5f)
                {
                    if (moveInput.x < 0) currentIndex--;
                    else currentIndex++;

                    if (currentIndex < 0) currentIndex = options.Count - 1;
                    if (currentIndex >= options.Count) currentIndex = 0;

                    viewHelper.HighlightCursor(currentIndex);
                    nextInputTime = Time.time + inputDelay;
                }

                if (_interactTriggered)
                {
                    selected = true;
                    _interactTriggered = false;
                }

                yield return null;
            }

            viewHelper.ClearCursors();
            onSelected?.Invoke(options[currentIndex]);
        }
    }
}
