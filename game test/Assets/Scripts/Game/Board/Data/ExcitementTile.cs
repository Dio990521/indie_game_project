using IndieGame.Core.Utilities;
using IndieGame.Core;
using IndieGame.Gameplay.ActionPoint;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Data
{
    /// <summary>
    /// 兴奋格：玩家停下后随机恢复 1~5 点行动点
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Excitement Tile")]
    public class ExcitementTile : TileBase
    {
        public override void OnPlayerStop(GameObject player)
        {
            int amount = Random.Range(1, 6);
            DebugTools.Log($"<color=magenta>[Excitement Tile]</color> 玩家 {player.name} 兴奋了！随机恢复 {amount} 点行动点！");
            ActionPointSystem.Instance.RestoreActionPoints(amount, "ExcitementTile");
        }
    }
}
