using UnityEngine;
using IndieGame.Core;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Board.Runtime.States;

namespace IndieGame.Gameplay.Treasure
{
    /// <summary>
    /// 木头人宝具配置：召唤一个临时木头人，在玩家当前格子为中心的指定半径内自由移动。
    /// 操控期间摄像机跟随木头人；按 ESC 摧毁木头人，恢复棋盘游戏规则。
    /// 在 Inspector 中设置 TreasureId = "woodenPuppet"，ActionPointCost = 1。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Treasure/Wooden Puppet Treasure", fileName = "WoodenPuppetTreasureSO")]
    public class WoodenPuppetTreasureSO : TreasureSO
    {
        /// <summary> 创建木头人激活状态（M10：多态分发）。 </summary>
        public override BaseState<BoardGameManager> CreateActivationState()
        {
            return new WoodenPuppetTreasureState(this);
        }

        [Header("木头人移动")]
        [Tooltip("木头人的自由移动速度（世界单位/秒）")]
        public float MoveSpeed = 4f;

        [Tooltip("以玩家当前格子 XZ 位置为圆心的最大可移动半径（世界单位）")]
        public float MaxRadius = 5f;

        [Tooltip("木头人旋转平滑速度（值越大转向越快）")]
        public float RotateSpeed = 15f;

        [Header("外观")]
        [Tooltip("木头人预制体（若为 null，运行时自动创建默认 Cube 用于测试）")]
        public GameObject PuppetPrefab;
    }
}
