using UnityEngine;

namespace IndieGame.Gameplay.Board.Data
{
    /// <summary>
    /// 所有格子效果的基类
    /// </summary>
    public abstract class TileBase : ScriptableObject
    {
        [Header("Base Settings")]
        public string tileName;
        public Color gizmoColor = Color.white; // 在编辑器里显示的颜色

        /// <summary>
        /// 当玩家停在这个格子上时触发
        /// </summary>
        public abstract void OnPlayerStop(GameObject player);
    }
}