using UnityEngine;
using IndieGame.Core;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Board.Runtime.States;

namespace IndieGame.Gameplay.Treasure
{
    /// <summary>
    /// 影骰子宝具配置：激活后，下一次掷骰子的点数翻倍（例：掷到 3 点实际移动 6 步）。
    /// 效果为一次性，消耗后自动清除。
    /// 在 Inspector 中设置 TreasureId = "shadow_dice"，ActionPointCost = 1。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Treasure/Shadow Dice Treasure", fileName = "ShadowDiceTreasureSO")]
    public class ShadowDiceTreasureSO : TreasureSO
    {
        /// <summary> 创建影骰子激活状态（M10：多态分发）。 </summary>
        public override BaseState<BoardGameManager> CreateActivationState()
        {
            return new ShadowDiceTreasureState(this);
        }
    }
}
