using UnityEngine;

namespace IndieGame.Gameplay.Board.Data
{
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Normal Tile")]
    public class NormalTile : TileBase
    {
        public override void OnPlayerStop(GameObject player)
        {
            Debug.Log($"<color=white>[Normal Tile]</color> 玩家 {player.name} 停在了一个普通格子上，什么也没发生。");
        }
    }
}