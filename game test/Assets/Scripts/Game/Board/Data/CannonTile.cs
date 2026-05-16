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

        [Header("转体配置")]
        [Tooltip("飞行时Y轴自转速度（度/秒），0=不转体，直接面向目标")]
        public float spinSpeed = 720f;

        [Tooltip("落地减速动画中，在对齐目标朝向之前额外旋转的圈数（越多减速感越强）")]
        public float settleExtraRotations = 2f;

        public override void OnPlayerStop(GameObject player)
        {
            EventBus.Raise(new BoardCannonLaunchRequestedEvent
            {
                ArcHeight             = arcHeight,
                LaunchSpeed           = launchSpeed,
                SpinSpeed             = spinSpeed,
                SettleExtraRotations  = settleExtraRotations
            });

            DebugTools.Log($"<color=orange>[Cannon Tile]</color> 玩家 {player.name} 被人体大炮发射！");
        }
    }
}
