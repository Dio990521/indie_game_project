using UnityEngine;
using IndieGame.Core;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Board.Runtime.States;

namespace IndieGame.Gameplay.Treasure
{
    /// <summary>
    /// 飞翼宝具配置：跨越地形，移动至前方或后方 1 格目标位置，无视障碍。
    /// 在 Inspector 中设置 TreasureId = "wing"，ActionPointCost = 3。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Treasure/Wing Treasure", fileName = "WingTreasureSO")]
    public class WingTreasureSO : TreasureSO
    {
        [Header("飞翼动画")]
        [Tooltip("抛物线飞跃的峰值高度（世界空间单位），复用大炮格弹射参数")]
        public float ArcHeight = 4f;

        [Tooltip("抛物线飞跃的移动速度")]
        public float LaunchSpeed = 10f;

        /// <summary> 创建飞翼激活状态（M10：多态分发）。 </summary>
        public override BaseState<BoardGameManager> CreateActivationState()
        {
            return new WingTreasureState(this);
        }
    }
}
