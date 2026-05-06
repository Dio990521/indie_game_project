using IndieGame.Core.Utilities;
using IndieGame.Core;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Data
{
    /// <summary>
    /// 扭曲格：保护指定路径不被轻易进入的特殊路口格子。
    ///
    /// 两种触发行为：
    /// 1. 路过时（非终点）：在分叉UI中隐藏被保护的路径，玩家只能从其他出口通过
    /// 2. 停下时（终点）：强制向预配置方向滑行1格，跳过分叉UI
    ///
    /// 配置约束：只应放置于有多个出口的路口节点。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Warp Tile")]
    public class WarpTile : TileBase
    {
        [Header("扭曲格配置")]
        [Tooltip("停下时强制滑行到的相邻格子 nodeID（必须是本路口的直接出口之一）")]
        public int forcedSlideNodeId = -1;

        [Tooltip("被保护的路径入口 nodeID（路过时该出口会从分叉UI中隐藏）")]
        public int protectedNodeId = -1;

        // 路过时也需触发，以便在分叉UI生效前排除被保护路径
        public override bool TriggerOnPass => true;

        public override void OnEnter(GameObject player)
        {
            // 路过和停下都发送路径过滤请求
            if (protectedNodeId >= 0)
                EventBus.Raise(new BoardWarpFilterPathEvent { ProtectedNodeId = protectedNodeId });

            // 强制滑行请求：Controller 会在路过时自动丢弃，仅最终落点时生效
            if (forcedSlideNodeId >= 0)
                EventBus.Raise(new BoardWarpSlideRequestedEvent { ForcedNodeId = forcedSlideNodeId });

            DebugTools.Log($"<color=cyan>[Warp Tile]</color> 玩家 {player.name} 经过扭曲格（保护:{protectedNodeId} / 强制滑行:{forcedSlideNodeId}）");
        }

        // 满足基类抽象约束，实际逻辑已在 OnEnter 中处理
        public override void OnPlayerStop(GameObject player) { }
    }
}
