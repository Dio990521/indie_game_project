using UnityEngine;
using IndieGame.Core;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Debugging
{
    public class StateDebugOverlay : MonoBehaviour
    {
        private GUIStyle _labelStyle;

        private void OnGUI()
        {
            if (!Application.isPlaying) return;
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 50,
                    normal = { textColor = Color.white }
                };
            }

            string globalState = GameManager.Instance != null
                ? GameManager.Instance.CurrentState.ToString()
                : "None";

            string boardState = "None";
            string overlayState = "None";
            if (BoardGameManager.Instance != null)
            {
                boardState = BoardGameManager.Instance.CurrentState != null
                    ? BoardGameManager.Instance.CurrentState.GetType().Name
                    : "None";
                overlayState = BoardGameManager.Instance.OverlayState != null
                    ? BoardGameManager.Instance.OverlayState.GetType().Name
                    : "None";
            }

            string text = $"Global: {globalState}\nBoard: {boardState}\nOverlay: {overlayState}";
            GUI.Label(new Rect(10f, 10f, 400f, 400f), text, _labelStyle);
        }
    }
}
