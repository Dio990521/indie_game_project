using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.Gameplay.Shop
{
    /// <summary>
    /// 商店静态配置（ScriptableObject）：
    /// 代表一个完整商店（例如“城镇武器商店”）。
    ///
    /// 该资源只保存静态数据：
    /// - 商店 ID
    /// - 展示名
    /// - 商品条目列表
    ///
    /// 运行时动态状态（库存剩余、已购数量）由 ShopSystem 维护并参与存档。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Shop/Shop")]
    public class ShopSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("商店唯一 ID（用于运行时索引与存档恢复）。")]
        [SerializeField] private string id;

        [Tooltip("商店显示名（可选，用于 UI 标题扩展）。")]
        [SerializeField] private string displayName = "Unnamed Shop";

        [Header("Entries")]
        [Tooltip("商店商品条目列表。")]
        [SerializeField] private List<ShopItemEntry> entries = new List<ShopItemEntry>();

        /// <summary>
        /// 商店 ID（只读）：
        /// 若为空则返回空字符串，调用方应将其视为无效配置。
        /// </summary>
        public string ID => string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();

        /// <summary>
        /// 商店显示名（只读）。
        /// </summary>
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Unnamed Shop" : displayName.Trim();

        /// <summary>
        /// 商品条目列表（只读引用）。
        /// </summary>
        public IReadOnlyList<ShopItemEntry> Entries => entries;
    }
}
