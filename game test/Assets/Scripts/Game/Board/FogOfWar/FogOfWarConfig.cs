using UnityEngine;

namespace IndieGame.Gameplay.Board.FogOfWar
{
    /// <summary>
    /// 战争迷雾配置，用 ScriptableObject 管理所有可调参数。
    /// 菜单路径：IndieGame/FogOfWar/Config
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/FogOfWar/Config")]
    public class FogOfWarConfig : ScriptableObject
    {
        [Header("世界边界（回退值，运行时自动从棋盘节点计算，通常无需修改）")]
        [Tooltip("棋盘中心 X/Z，仅在 BoardMapManager 不可用时使用")]
        public Vector2 worldCenter;
        [Tooltip("棋盘 XZ 总跨度（米），仅在 BoardMapManager 不可用时使用")]
        public Vector2 worldSize = new Vector2(200f, 200f);

        [Header("纹理精度")]
        [Tooltip("迷雾纹理分辨率。256 已足够大多数地图；精度需求高时用 512")]
        [Range(64, 1024)]
        public int textureResolution = 256;

        [Header("揭开参数")]
        [Tooltip("以玩家为圆心的揭开半径（世界单位）")]
        public float revealRadius = 20f;
        [Tooltip("玩家位移超过该距离才触发一次揭开，避免每帧重复计算")]
        [Range(0.1f, 5f)]
        public float updateThreshold = 0.5f;

        [Header("视觉")]
        public Color fogColor = new Color(0.05f, 0.05f, 0.05f, 0.95f);
    }
}
