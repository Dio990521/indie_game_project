using IndieGame.Core.Utilities;
using IndieGame.Core;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Data
{
    /// <summary>
    /// 方向移动格：玩家停下后，强制从指定节点方向出发移动 steps 格。
    /// 方向由 directionNodeId 决定（配置为当前格邻接节点的 nodeID），不依赖玩家当前朝向。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Directional Move Tile")]
    public class DirectionalMoveTile : TileBase
    {
        [Tooltip("首步强制走向的节点 nodeID（必须是当前格的直接邻接节点）")]
        public int directionNodeId = -1;

        [Tooltip("移动格数（大于0）")]
        [Min(1)]
        public int steps = 3;

        public override void OnPlayerStop(GameObject player)
        {
            DebugTools.Log($"<color=cyan>[Directional Move Tile]</color> 玩家 {player.name} 触发方向格，朝节点 {directionNodeId} 移动 {steps} 格！");
            EventBus.Raise(new BoardDirectionalMoveRequestedEvent
            {
                Steps = steps,
                DirectionNodeId = directionNodeId
            });
        }
    }
}
