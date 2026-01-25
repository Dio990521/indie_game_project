using UnityEngine;
using IndieGame.Core;
using IndieGame.UI.Confirmation;

namespace IndieGame.Gameplay.Exploration
{
    public class ExitZoneTrigger : MonoBehaviour
    {
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
                    // 统一通过 SceneLoader 返回棋盘（自动恢复上次节点）
                    SceneLoader loader = SceneLoader.Instance;
                    if (loader == null) return;
                    loader.ReturnToBoard();
                },
                OnCancel = null
            });
        }
    }
}
