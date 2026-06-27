using System;
using UnityEngine;

namespace IndieGame.Gameplay.Inventory
{
    /// <summary>
    /// 背包槽位：
    /// 用于存放具体道具与数量。
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        // 槽位里的道具
        public ItemSO Item;
        // 当前数量
        public int Count;
        // 该槽位的“实例名称”（用于支持制造时的自定义命名）
        // 约定：
        // - null/空字符串 代表“使用 ItemSO 的原始名称”
        // - 非空字符串 代表“该槽位物品实例名”
        public string CustomName;
        // 武器实例专属的强化数据（已应用前缀等）；非武器物品恒为 null
        public WeaponInstanceData WeaponData;

        public InventorySlot(ItemSO item, int count, string customName = null)
        {
            Item = item;
            Count = Mathf.Max(0, count);
            CustomName = NormalizeCustomName(customName);
        }

        /// <summary>
        /// 判断当前槽位是否可与目标物品堆叠：
        /// 条件为“同一个 ItemSO 且同一个自定义名称”；武器（WeaponSO）永远不堆叠——
        /// 每把武器都可能携带独立的强化数据，堆叠会导致强化信息被错误合并/丢失。
        /// </summary>
        public bool CanStackWith(ItemSO item, string customName)
        {
            if (Item != item) return false;
            if (item is WeaponSO) return false;
            return string.Equals(CustomName, NormalizeCustomName(customName), StringComparison.Ordinal);
        }

        /// <summary>
        /// 自定义名称标准化：
        /// 把 null / 空白统一转成空字符串，避免比较时出现重复分支。
        /// </summary>
        private static string NormalizeCustomName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
