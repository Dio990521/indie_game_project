using UnityEngine;
using IndieGame.Core;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Board.Runtime.States;

namespace IndieGame.Gameplay.Treasure
{
    [CreateAssetMenu(menuName = "BoardGame/Treasure/Cloak Treasure", fileName = "CloakTreasureSO")]
    public class CloakTreasureSO : TreasureSO
    {
        // 斗篷宝具无额外专属字段
        // ActionPointCost 继承自基类，在 Inspector 中配置

        /// <summary> 创建斗篷激活状态（M10：多态分发）。 </summary>
        public override BaseState<BoardGameManager> CreateActivationState()
        {
            return new CloakTreasureState(this);
        }
    }
}
