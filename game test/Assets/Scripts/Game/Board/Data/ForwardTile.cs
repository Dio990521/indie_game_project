using IndieGame.Core.Utilities;
using IndieGame.Core;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Data
{
    /// <summary>
    /// 前进格：玩家停下后强制额外向前移动 steps 格
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Forward Tile")]
    public class ForwardTile : TileBase
    {
        [Tooltip("额外前进格数")]
        public int steps = 3;

        public override void OnPlayerStop(GameObject player)
        {
            DebugTools.Log($"<color=lime>[Forward Tile]</color> 玩家 {player.name} 触发前进格，额外前进 {steps} 格！");
            EventBus.Raise(new BoardExtraMoveRequestedEvent { Steps = steps });
        }
    }
}
