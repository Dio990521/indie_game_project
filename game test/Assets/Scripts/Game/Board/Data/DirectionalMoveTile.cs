using IndieGame.Core.Utilities;
using IndieGame.Core;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Data
{
    /// <summary>
    /// 方向移动格：玩家停下后，沿当前行进方向随机前进 1~3 格。
    /// DirectionNodeId 传 -1 表示不覆盖行进方向，玩家自然延续当前朝向移动。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Directional Move Tile")]
    public class DirectionalMoveTile : TileBase
    {
        // 随机步数范围（含两端）
        [Min(1)] public int minSteps = 1;
        [Min(1)] public int maxSteps = 3;

        public override void OnPlayerStop(GameObject player)
        {
            int steps = Random.Range(minSteps, maxSteps + 1);
            DebugTools.Log($"<color=cyan>[Directional Move Tile]</color> 玩家 {player.name} 触发方向格，沿当前方向前进 {steps} 格！");
            EventBus.Raise(new BoardDirectionalMoveRequestedEvent
            {
                Steps = steps,
                DirectionNodeId = -1  // -1：不锁定方向，保持玩家当前行进朝向
            });
        }
    }
}
