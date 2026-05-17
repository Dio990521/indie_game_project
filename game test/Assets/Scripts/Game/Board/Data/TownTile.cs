using UnityEngine;
using IndieGame.Core;
using IndieGame.UI.Confirmation;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Board.Runtime.States;
using IndieGame.Gameplay.Town;

namespace IndieGame.Gameplay.Board.Data
{
    /// <summary>
    /// 城镇地块：玩家路过时弹出确认框，选择是否进入城镇。
    /// - 选"是"：舍弃剩余步数，进入城镇菜单（TownState）。
    /// - 选"否"：继续剩余步数移动，流程不受影响。
    /// 路过或停留时无条件解锁该城镇（用于传送系统）。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Town Tile")]
    public class TownTile : TileBase
    {
        [Header("城镇配置")]
        [Tooltip("城镇名称，显示在确认框中（如迷雾小镇）")]
        public string townName = "城镇";

        [Tooltip("城镇菜单背景图，在 Inspector 中为每个城镇 SO 单独配置")]
        public Sprite townBackground;

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
        /// 核心逻辑：玩家经过或停留时，无条件解锁本城镇，然后弹出确认框询问是否进入。
        /// </summary>
        public override void OnEnter(GameObject player)
        {
            // 路过即解锁，无论玩家是否选择进入菜单
            var boardInst = BoardGameManager.Instance;
            var mc = boardInst != null ? boardInst.movementController : null;
            int nodeId = mc != null ? mc.CurrentNodeId : -1;
            if (nodeId >= 0)
                TownUnlockManager.Instance?.UnlockTown(nodeId);

            string message = $"是否进入{townName}？";

            ConfirmationEvent.Request(new ConfirmationRequest
            {
                Message  = message,
                OnConfirm = EnterTown,
                OnCancel  = null   // 取消时不做任何处理，步数自然继续消耗
            });
        }

        /// <summary>
        /// 玩家确认进入城镇：停止移动并切换到 TownState，传入城镇数据。
        /// </summary>
        private void EnterTown()
        {
            var board = BoardGameManager.Instance;
            if (board == null) return;

            int nodeId = board.movementController?.CurrentNodeId ?? -1;

            // 立即停止棋盘移动，舍弃剩余步数
            board.movementController?.StopMoveImmediate();

            // 切换到城镇状态，传入 nodeId 和 this 以支持背景图和传送功能
            board.ChangeState(new TownState(nodeId, this));
        }
    }
}
