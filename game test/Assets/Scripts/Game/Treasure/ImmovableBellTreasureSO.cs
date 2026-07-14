using UnityEngine;
using IndieGame.Core;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Board.Runtime.States;

namespace IndieGame.Gameplay.Treasure
{
    /// <summary>
    /// 不动铃铛宝具配置：激活后，本次掷骰的移动将忽略所有位移格效果（大炮、传送、行进、扭曲格强制滑行等），
    /// 强制玩家停在骰子点数对应的格子上。
    /// 在 Inspector 中设置 TreasureId = "immovable_bell"，ActionPointCost = 1。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Treasure/Immovable Bell Treasure", fileName = "ImmovableBellTreasureSO")]
    public class ImmovableBellTreasureSO : TreasureSO
    {
        /// <summary> 创建不动铃铛激活状态（M10：多态分发）。 </summary>
        public override BaseState<BoardGameManager> CreateActivationState()
        {
            return new ImmovableBellTreasureState(this);
        }
    }
}
