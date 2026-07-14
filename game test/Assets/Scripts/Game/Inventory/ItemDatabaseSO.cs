using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.Gameplay.Inventory
{
    /// <summary>
    /// 物品总数据库（ScriptableObject）：
    /// 存档系统通过 ItemSO.ID 反查 ItemSO 资源时使用的唯一登记表。
    ///
    /// 使用约定：
    /// 1) 游戏中所有"可进入背包"的 ItemSO（含 WeaponSO / ArmorSO / 图纸等子类）都必须登记到 Items 列表；
    /// 2) 未登记的物品在读档时无法恢复（会打印警告并跳过该槽位）；
    /// 3) ItemSO.ID 必须全局唯一（重复 ID 由 DatabaseIndexer 构建索引时打警告并忽略后者）。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Inventory/Item Database", fileName = "ItemDatabaseSO")]
    public class ItemDatabaseSO : ScriptableObject
    {
        [Tooltip("全部物品资源列表（存档按 ItemSO.ID 反查）")]
        public List<ItemSO> Items = new List<ItemSO>();
    }
}
