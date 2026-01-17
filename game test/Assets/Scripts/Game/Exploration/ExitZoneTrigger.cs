using UnityEngine;
using IndieGame.Core;
using IndieGame.UI.Confirmation;

namespace IndieGame.Gameplay.Exploration
{
    public class ExitZoneTrigger : MonoBehaviour
    {
        public string BoardSceneName;
        public string ZoneName = "Board";

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            string message = $"Leave {ZoneName}?";
            ConfirmationEvent.Request(new ConfirmationRequest
            {
                Message = message,
                OnConfirm = () =>
                {
                    var gm = GameManager.Instance;
                    if (gm == null) return;
                    gm.LoadScene(BoardSceneName, GameState.BoardMode);
                },
                OnCancel = null
            });
        }
    }
}
