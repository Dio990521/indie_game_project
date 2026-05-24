using System;
using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Data
{
    /// <summary>
    /// 大世界格子随机刷新系统的配置文件。
    /// 通过此 SO 控制：是否开启刷新、以及各格子类型的概率分布。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Board Refresh Config", fileName = "BoardRefreshConfig")]
    public class BoardRefreshConfigSO : ScriptableObject
    {
        [Header("主开关")]
        [Tooltip("全局开关。BoardRefreshManager 上也有一个 Inspector 开关，两者都为 true 才会刷新")]
        public bool enableRandomRefresh = true;

        [Header("格子概率分布")]
        [Tooltip("刷新时用来填充可刷新空格的格子类型及其权重。\n可在自定义 Inspector 中可视化调整比例。")]
        public List<TileSpawnEntry> tilePool = new List<TileSpawnEntry>();

        // ------------------------------------------------------------------
        // 数据结构
        // ------------------------------------------------------------------

        [Serializable]
        public class TileSpawnEntry
        {
            [Tooltip("目标格子 ScriptableObject")]
            public TileBase tile;

            [Tooltip("该格子出现的相对权重（值越大出现越频繁）")]
            [Min(0f)] public float weight = 1f;
        }

        // ------------------------------------------------------------------
        // 运行时工具方法
        // ------------------------------------------------------------------

        /// <summary>
        /// 根据 tilePool 加权随机选取一个格子，返回其在池中的索引。
        /// 返回 -1 表示池为空或权重全为 0（保留空格）。
        /// </summary>
        public int PickRandomTileIndex()
        {
            if (tilePool == null || tilePool.Count == 0) return -1;

            float total = 0f;
            foreach (var e in tilePool)
                if (e.tile != null) total += Mathf.Max(0f, e.weight);
            if (total <= 0f) return -1;

            float roll = UnityEngine.Random.Range(0f, total);
            float cumulative = 0f;
            for (int i = 0; i < tilePool.Count; i++)
            {
                var e = tilePool[i];
                if (e.tile == null) continue;
                cumulative += Mathf.Max(0f, e.weight);
                if (roll < cumulative) return i;
            }
            return -1;
        }

        /// <summary>
        /// 根据池索引获取对应的 TileBase，越界或 -1 时返回 null。
        /// </summary>
        public TileBase GetTileByIndex(int index)
        {
            if (index < 0 || index >= tilePool.Count) return null;
            return tilePool[index].tile;
        }
    }
}
