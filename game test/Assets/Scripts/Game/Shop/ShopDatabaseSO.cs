using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.Gameplay.Shop
{
    /// <summary>
    /// 商店数据库（ScriptableObject）：
    /// 用于集中维护全量 ShopSO，供 ShopSystem 在启动时转 Dictionary 索引。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Shop/Shop Database")]
    public class ShopDatabaseSO : ScriptableObject
    {
        [Tooltip("商店列表（可在 Inspector 维护）。")]
        [SerializeField] private List<ShopSO> shops = new List<ShopSO>();

        /// <summary>
        /// 全量商店列表（只读引用）。
        /// </summary>
        public IReadOnlyList<ShopSO> Shops => shops;
    }
}
