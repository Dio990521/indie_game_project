using IndieGame.Core;
using IndieGame.Core.Utilities;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Data
{
    /// <summary>
    /// 传送格：玩家落下时瞬间传送到指定节点，无动画，并触发目标格子效果。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Teleport Tile")]
    public class TeleportTile : TileBase
    {
        [Header("传送配置")]
        [Tooltip("目标节点 ID（填写 MapWaypoint 的 nodeID）")]
        public int targetNodeId;

        public override void OnPlayerStop(GameObject player)
        {
            EventBus.Raise(new BoardTeleportRequestedEvent { TargetNodeId = targetNodeId });
            DebugTools.Log($"<color=cyan>[Teleport Tile]</color> 玩家 {player.name} 传送至节点 {targetNodeId}");
        }
    }
}
