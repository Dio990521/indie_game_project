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

            [Tooltip("每次刷新时该格子的最小生成数量（0 = 不限制）")]
            [Min(0)] public int minCount = 0;

            [Tooltip("每次刷新时该格子的最大生成数量（0 = 不限制）")]
            [Min(0)] public int maxCount = 0;
        }

        // ------------------------------------------------------------------
        // 运行时工具方法
        // ------------------------------------------------------------------

        /// <summary>
        /// 按 minCount/maxCount 约束和权重，为 totalCount 个格子分配 tile 索引。
        /// 返回列表长度等于 totalCount，顺序已随机打乱。
        /// 索引 -1 表示空格（所有 tile 均达到 maxCount 时出现）。
        /// </summary>
        public List<int> AllocateTiles(int totalCount)
        {
            var result = new List<int>(totalCount);
            if (tilePool == null || tilePool.Count == 0) return result;

            var allocated = new int[tilePool.Count];

            // ── 第一阶段：保证每个 tile 达到有效 minCount ──────────────────
            int totalMin = 0;
            for (int i = 0; i < tilePool.Count; i++)
            {
                var e = tilePool[i];
                if (e.tile == null) continue;
                int effectiveMax = e.maxCount > 0 ? e.maxCount : int.MaxValue;
                totalMin += Mathf.Min(e.minCount, effectiveMax);
            }

            // minCount 总和超过节点数时按比例缩减
            float scale = totalMin > totalCount && totalMin > 0
                ? (float)totalCount / totalMin
                : 1f;
            if (scale < 1f)
                Debug.LogWarning(
                    $"[BoardRefreshConfig] minCount 总和（{totalMin}）超过可刷新节点数（{totalCount}），已按比例缩减。");

            for (int i = 0; i < tilePool.Count; i++)
            {
                var e = tilePool[i];
                if (e.tile == null || e.minCount <= 0) continue;
                int effectiveMax = e.maxCount > 0 ? e.maxCount : int.MaxValue;
                int effectiveMin = Mathf.Min(e.minCount, effectiveMax);
                int toAdd = Mathf.Min(
                    Mathf.RoundToInt(effectiveMin * scale),
                    totalCount - result.Count);
                for (int j = 0; j < toAdd; j++)
                    result.Add(i);
                allocated[i] += toAdd;
            }

            // ── 第二阶段：权重随机填充剩余格子（尊重 maxCount）─────────────
            int remaining = totalCount - result.Count;
            for (int k = 0; k < remaining; k++)
            {
                int idx = PickWeightedIndexWithCap(allocated);
                if (idx < 0) break; // 所有 tile 均已达到 maxCount，停止分配
                result.Add(idx);
                allocated[idx]++;
            }

            // 若所有 tile 达到上限仍有剩余，用 -1（空格）补齐
            while (result.Count < totalCount)
                result.Add(-1);

            // ── Fisher-Yates 打乱，保证空间分布随机 ────────────────────────
            for (int i = result.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }

            return result;
        }

        /// <summary>
        /// 根据权重加权随机选取一个未达到 maxCount 的 tile 索引。
        /// 全部超限时返回 -1。
        /// </summary>
        private int PickWeightedIndexWithCap(int[] allocated)
        {
            float total = 0f;
            for (int i = 0; i < tilePool.Count; i++)
            {
                var e = tilePool[i];
                if (e.tile == null) continue;
                if (e.maxCount > 0 && allocated[i] >= e.maxCount) continue;
                total += Mathf.Max(0f, e.weight);
            }
            if (total <= 0f) return -1;

            float roll = UnityEngine.Random.Range(0f, total);
            float cumulative = 0f;
            for (int i = 0; i < tilePool.Count; i++)
            {
                var e = tilePool[i];
                if (e.tile == null) continue;
                if (e.maxCount > 0 && allocated[i] >= e.maxCount) continue;
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
