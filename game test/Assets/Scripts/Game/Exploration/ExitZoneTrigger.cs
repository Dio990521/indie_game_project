using UnityEngine;
using IndieGame.Core;
using IndieGame.UI.Confirmation;

namespace IndieGame.Gameplay.Exploration
{
    public class ExitZoneTrigger : MonoBehaviour
    {
        [SerializeField] private int waypointIndex = 0;
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
                    SceneLoader loader = SceneLoader.Instance;
                    if (loader == null) return;
                    loader.ReturnToBoard(waypointIndex);
                },
                OnCancel = null
            });
        }
    }
}
