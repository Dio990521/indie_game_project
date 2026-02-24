using System;
using UnityEngine;
using IndieGame.Gameplay.Inventory;

namespace IndieGame.Gameplay.Shop
{
    /// <summary>
    /// 商店条目静态配置：
    /// 代表“商店里可售卖的一条商品规则”。
    ///
    /// 为什么需要独立条目 ID：
    /// 1) 同一个 ItemSO 可能在不同商店出现不同价格/库存；
    /// 2) 未来同一商店也可能出现“同物品不同套餐”；
    /// 3) 因此运行时与存档都应优先以 ShopEntryID 作为主键，而不是 ItemSO.ID。
    /// </summary>
    [Serializable]
    public class ShopItemEntry
    {
        [Header("Identity")]
        [Tooltip("商店条目唯一 ID（建议稳定字符串）。若留空将回退到 ItemSO.ID。")]
        [SerializeField] private string entryID;

        [Header("Commodity")]
        [Tooltip("售卖的物品配置。")]
        [SerializeField] private ItemSO item;

        [Tooltip("单价（金币）。必须大于 0。")]
        [SerializeField] private int unitPrice = 1;

        [Header("Stock & Limit")]
        [Tooltip("初始库存：-1 表示无限库存；>=0 表示有限库存。")]
        [SerializeField] private int initialStock = -1;

        [Tooltip("每个存档的累计限购：-1 表示无限购买；>=0 表示最多可买 N 个。")]
        [SerializeField] private int purchaseLimitPerSave = -1;

        /// <summary>
        /// 解析后的条目 ID：
        /// - 优先使用 entryID；
        /// - 若 entryID 为空，则回退 ItemSO.ID；
        /// - 两者都为空则返回空字符串（视为无效配置）。
        /// </summary>
        public string EntryID
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(entryID))
                {
                    return entryID.Trim();
                }

                if (item != null && !string.IsNullOrWhiteSpace(item.ID))
                {
                    return item.ID.Trim();
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// 售卖物品。
        /// </summary>
        public ItemSO Item => item;

        /// <summary>
        /// 单价（保证 >=1）。
        /// </summary>
        public int UnitPrice => Mathf.Max(1, unitPrice);

        /// <summary>
        /// 初始库存：
        /// -1 代表无限库存；
        /// >=0 代表有限库存。
        /// </summary>
        public int InitialStock => initialStock < 0 ? -1 : initialStock;

        /// <summary>
        /// 存档内累计限购：
        /// -1 代表无限购买；
        /// >=0 代表最多购买数量。
        /// </summary>
        public int PurchaseLimitPerSave => purchaseLimitPerSave < 0 ? -1 : purchaseLimitPerSave;
    }
}
