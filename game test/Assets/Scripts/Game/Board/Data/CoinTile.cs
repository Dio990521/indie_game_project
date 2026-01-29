using UnityEngine;

namespace IndieGame.Gameplay.Board.Data
{
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Coin Tile")]
    public class CoinTile : TileBase
    {
        public int coinAmount = 10;

        public override void OnPlayerStop(GameObject player)
        {
            // 这里未来可以调用 player.GetComponent<Wallet>().AddCoin(coinAmount);
            // 目前仅输出调试信息
            Debug.Log($"<color=yellow>[Coin Tile]</color> 恭喜！玩家 {player.name} 获得了 {coinAmount} 金币！");
        }
    }
}
