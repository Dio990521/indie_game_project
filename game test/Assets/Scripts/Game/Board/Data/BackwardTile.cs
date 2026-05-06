using IndieGame.Core.Utilities;
using IndieGame.Core;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Data
{
    /// <summary>
    /// 后退格：玩家停下后强制向后退回 steps 格
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Backward Tile")]
    public class BackwardTile : TileBase
    {
        [Tooltip("额外后退格数")]
        public int steps = 3;

        public override void OnPlayerStop(GameObject player)
        {
            DebugTools.Log($"<color=red>[Backward Tile]</color> 玩家 {player.name} 触发后退格，后退 {steps} 格！");
            // Steps 为负数，Controller 收到后执行后退逻辑
            EventBus.Raise(new BoardExtraMoveRequestedEvent { Steps = -steps });
        }
    }
}
