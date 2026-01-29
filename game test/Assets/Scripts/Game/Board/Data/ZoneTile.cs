using UnityEngine;
using IndieGame.Core;
using IndieGame.UI.Confirmation;
using IndieGame.Gameplay.Board.Runtime;
using UnityEngine.Localization;
using UnityEngine.Serialization;

namespace IndieGame.Gameplay.Board.Data
{
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Zone Tile")]
    public class ZoneTile : TileBase
    {
        public string TargetSceneName;
        public LocationID TargetLocationId;
        [FormerlySerializedAs("ZoneName")]
        public LocalizedString ZoneName;
        [TextArea]
        [FormerlySerializedAs("Description")]
        public LocalizedString Description;
        [FormerlySerializedAs("EnterPrompt")]
        public LocalizedString EnterPrompt;

        public override bool TriggerOnPass => true;

        public override void OnPlayerStop(GameObject player)
        {
            // Legacy path, route to OnEnter for consistency.
            OnEnter(player);
        }

        public override void OnEnter(GameObject player)
        {
            // 组装进入提示文案（支持本地化）
            string zoneLabel = ZoneName != null ? ZoneName.GetLocalizedString() : "this zone";
            string message = $"Enter {zoneLabel}?";
            if (EnterPrompt != null)
            {
                EnterPrompt.Arguments = new object[] { zoneLabel };
                message = EnterPrompt.GetLocalizedString();
            }
            ConfirmationEvent.Request(new ConfirmationRequest
            {
                Message = message,
                OnConfirm = () =>
                {
                    SceneLoader loader = SceneLoader.Instance;
                    if (loader == null) return;
                    // 确认后跳转到目标探索场景
                    loader.LoadScene(TargetSceneName, TargetLocationId);
                },
                OnCancel = null
            });
        }
    }
}
