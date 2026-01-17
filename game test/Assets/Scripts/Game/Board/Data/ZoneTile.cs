using UnityEngine;
using IndieGame.Core;
using IndieGame.UI.Confirmation;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Gameplay.Board.Data
{
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Zone Tile")]
    public class ZoneTile : TileBase
    {
        public string TargetSceneName;
        public string ZoneName;
        [TextArea] public string Description;

        public override bool TriggerOnPass => true;

        public override void OnPlayerStop(GameObject player)
        {
            // Legacy path, route to OnEnter for consistency.
            OnEnter(player);
        }

        public override void OnEnter(GameObject player)
        {
            string label = string.IsNullOrEmpty(ZoneName) ? "this zone?" : ZoneName;
            string message = $"Enter {label}?";
            ConfirmationEvent.Request(new ConfirmationRequest
            {
                Message = message,
                OnConfirm = () =>
                {
                    var gm = GameManager.Instance;
                    if (gm == null) return;

                    var board = BoardGameManager.Instance;
                    if (board != null && board.movementController != null)
                    {
                        gm.LastBoardIndex = board.movementController.CurrentNodeId;
                    }

                    gm.LoadScene(TargetSceneName, GameState.FreeRoam);
                },
                OnCancel = null
            });
        }
    }
}
