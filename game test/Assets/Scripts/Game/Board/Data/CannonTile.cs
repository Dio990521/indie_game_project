using IndieGame.Core;
using IndieGame.Core.Utilities;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Data
{
    /// <summary>
    /// 人体大炮格：玩家落下时被随机弹射到地图上的另一个格子，轨迹为抛物线。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Cannon Tile")]
    public class CannonTile : TileBase
    {
        [Header("弹射配置")]
        [Tooltip("抛物线峰值高度（越大弧度越高）")]
        public float arcHeight = 5f;

        [Tooltip("弹射移动速度（越大越快）")]
        public float launchSpeed = 12f;

        public override void OnPlayerStop(GameObject player)
        {
            EventBus.Raise(new BoardCannonLaunchRequestedEvent
            {
                ArcHeight = arcHeight,
                LaunchSpeed = launchSpeed
            });

            DebugTools.Log($"<color=orange>[Cannon Tile]</color> 玩家 {player.name} 被人体大炮发射！");
        }
    }
}
