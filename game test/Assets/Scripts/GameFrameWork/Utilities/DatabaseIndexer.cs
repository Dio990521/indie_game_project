using System;
using System.Collections.Generic;

namespace IndieGame.Core.Utilities
{
    /// <summary>
    /// 数据库索引构建工具：
    /// 把 List&lt;T&gt; 形式的 ScriptableObject 数据库统一构建为 ID -> 对象 的 Dictionary，
    /// 替代各系统重复编写的 RebuildXxxIndex（含空值检查、重复 ID 警告等）。
    ///
    /// 使用方法：
    /// <code>
    /// _shopById = DatabaseIndexer.BuildById(shopDatabase.Shops, s => s.ID, "ShopSystem");
    /// </code>
    /// </summary>
    public static class DatabaseIndexer
    {
        /// <summary>
        /// 用给定 ID 选择器构建 ID -> 对象 的字典。
        /// - 跳过 null 元素
        /// - 跳过空 ID 元素并记录警告
        /// - 跳过重复 ID 元素并记录警告
        /// </summary>
        /// <typeparam name="T">数据元素类型（引用类型）。</typeparam>
        /// <param name="items">数据列表（允许 null，返回空字典）。</param>
        /// <param name="idSelector">从元素提取 ID 的回调。</param>
        /// <param name="contextName">上下文名称（用于日志，如 "ShopSystem"）。</param>
        /// <returns>构建好的字典（StringComparer.Ordinal）。</returns>
        public static Dictionary<string, T> BuildById<T>(
            IReadOnlyList<T> items,
            Func<T, string> idSelector,
            string contextName) where T : class
        {
            Dictionary<string, T> dict = new Dictionary<string, T>(StringComparer.Ordinal);
            if (items == null || idSelector == null) return dict;

            for (int i = 0; i < items.Count; i++)
            {
                T item = items[i];
                if (item == null) continue;

                string id = idSelector(item);
                if (string.IsNullOrWhiteSpace(id))
                {
                    DebugTools.LogWarning($"[{contextName}] Item at index {i} has empty ID, ignored.");
                    continue;
                }

                if (dict.ContainsKey(id))
                {
                    DebugTools.LogWarning($"[{contextName}] Duplicate ID ignored: {id}");
                    continue;
                }

                dict.Add(id, item);
            }
            return dict;
        }
    }
}
