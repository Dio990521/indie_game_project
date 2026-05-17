using UnityEngine;
using IndieGame.Core;
using IndieGame.UI.Confirmation;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Board.Runtime.States;

namespace IndieGame.Gameplay.Board.Data
{
    /// <summary>
    /// 城镇地块：玩家路过时弹出确认框，选择是否进入城镇。
    /// - 选"是"：舍弃剩余步数，进入城镇菜单（TownState）。
    /// - 选"否"：继续剩余步数移动，流程不受影响。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Town Tile")]
    public class TownTile : TileBase
    {
        [Header("城镇配置")]
        [Tooltip("城镇名称，显示在确认框中（如迷雾小镇）")]
        public string townName = "城镇";

        /// <summary>
        /// 路过即触发：无论玩家是否在此格停步，都会弹出询问框。
        /// </summary>
        public override bool TriggerOnPass => true;

        /// <summary>
        /// 玩家步数恰好用完并停在此格时触发，统一走 OnEnter 逻辑。
        /// </summary>
        public override void OnPlayerStop(GameObject player)
        {
            OnEnter(player);
        }

        /// <summary>
        /// 核心逻辑：玩家经过或停留时，弹出确认框询问是否进入城镇。
        /// </summary>
        public override void OnEnter(GameObject player)
        {
            string message = $"是否进入{townName}？";

            ConfirmationEvent.Request(new ConfirmationRequest
            {
                Message  = message,
                OnConfirm = EnterTown,
                OnCancel  = null   // 取消时不做任何处理，步数自然继续消耗
            });
        }

        /// <summary>
        /// 玩家确认进入城镇：停止移动并切换到 TownState。
        /// </summary>
        private void EnterTown()
        {
            var board = BoardGameManager.Instance;
            if (board == null) return;

            // 立即停止棋盘移动，舍弃剩余步数
            board.movementController?.StopMoveImmediate();

            // 切换到城镇状态（TownState.OnEnter 负责显示城镇 UI）
            board.ChangeState(new TownState());
        }
    }
}
