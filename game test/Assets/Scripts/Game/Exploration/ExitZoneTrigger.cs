using UnityEngine;
using IndieGame.Core;
using IndieGame.UI.Confirmation;
using UnityEngine.Localization;
using UnityEngine.Serialization;

namespace IndieGame.Gameplay.Exploration
{
    public class ExitZoneTrigger : MonoBehaviour
    {
        [FormerlySerializedAs("ZoneName")]
        public LocalizedString ZoneName;
        [FormerlySerializedAs("LeavePrompt")]
        public LocalizedString LeavePrompt;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            string zoneLabel = ZoneName != null ? ZoneName.GetLocalizedString() : "Board";
            string message = $"Leave {zoneLabel}?";
            if (LeavePrompt != null)
            {
                LeavePrompt.Arguments = new object[] { zoneLabel };
                message = LeavePrompt.GetLocalizedString();
            }
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
