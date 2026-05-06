using System;
using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.Core.Utilities
{
    /// <summary>
    /// 通用加权随机工具：按各条目的 Weight 比例随机抽取一条。
    /// </summary>
    public static class WeightedRandomUtil
    {
        /// <summary>
        /// 从实现了 IWeighted 的列表中按权重随机抽取一条。
        /// Weight &lt;= 0 的条目会被跳过。列表为空或全部无效时返回 default。
        /// </summary>
        public static T Pick<T>(IList<T> entries) where T : class, IWeighted
        {
            return Pick(entries, e => e.Weight);
        }

        /// <summary>
        /// 从任意列表中按委托返回的权重随机抽取一条。
        /// 适用于无法直接实现 IWeighted 的已有类型。
        /// Weight &lt;= 0 的条目会被跳过。列表为空或全部无效时返回 default。
        /// </summary>
        public static T Pick<T>(IList<T> entries, Func<T, int> getWeight)
        {
            if (entries == null || entries.Count == 0)
                return default;

            // 计算有效总权重
            int totalWeight = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                int w = getWeight(entries[i]);
                if (w > 0) totalWeight += w;
            }

            if (totalWeight <= 0)
                return default;

            int roll = UnityEngine.Random.Range(0, totalWeight);
            int accumulated = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                int w = getWeight(entries[i]);
                if (w <= 0) continue;
                accumulated += w;
                if (roll < accumulated)
                    return entries[i];
            }

            return default;
        }
    }
}
