using IndieGame.Gameplay.Board.FogOfWar;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Data
{
    /// <summary>
    /// 雷达格：玩家到达时以大范围揭开世界迷雾。揭开半径在 Inspector 中配置。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Radar Tile")]
    public class RadarTile : TileBase
    {
        [Header("雷达揭雾")]
        public float revealRadius = 60f; // 揭开半径（世界单位），远大于普通移动揭开范围

        public override void OnPlayerStop(GameObject player)
        {
            if (FogOfWarManager.Instance == null) return;
            FogOfWarManager.Instance.RevealAreaAt(player.transform.position, revealRadius);
        }
    }
}
