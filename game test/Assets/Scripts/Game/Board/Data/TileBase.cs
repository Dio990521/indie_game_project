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

        /// <summary>
        /// 是否在经过该格子时触发（非最终落点）。
        /// </summary>
        public virtual bool TriggerOnPass => false;

        /// <summary>
        /// 当玩家进入该格子时触发（默认映射到 OnPlayerStop）
        /// </summary>
        public virtual void OnEnter(GameObject player)
        {
            OnPlayerStop(player);
        }
    }
}
