using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Serialization;

namespace IndieGame.Gameplay.Inventory
{
    /// <summary>
    /// 道具分类：
    /// 用于排序与逻辑区分（消耗品/装备/材料/任务物品）。
    /// </summary>
    public enum ItemCategory
    {
        Consumable,
        Equipment,
        Material,
        Quest
    }

    [CreateAssetMenu(menuName = "IndieGame/Inventory/Item")]
    public class ItemSO : ScriptableObject
    {
        [Header("Base Info")]
        [Tooltip("唯一 ID，用于存档与查找（建议使用稳定字符串）")]
        public string ID;

        [FormerlySerializedAs("ItemName")]
        public LocalizedString ItemName;

        [Tooltip("道具图标")]
        public Sprite Icon;

        [TextArea]
        [FormerlySerializedAs("Description")]
        public string Description;

        [Header("Category & Stack")]
        [Tooltip("道具分类")]
        public ItemCategory Category = ItemCategory.Consumable;

        [Tooltip("是否可堆叠")]
        public bool isStackable = true;

        [Tooltip("最大堆叠数量")]
        public int maxStack = 999;

        // Inspector 配置指南：
        // - 在 ItemSO 资源文件中选择 Category（Consumable/Equipment/Material/Quest）
        // - 勾选 isStackable 并设置 maxStack 来控制堆叠上限

        public virtual void Use()
        {
            string name = ItemName != null ? ItemName.GetLocalizedString() : "Item";
            Debug.Log($"[ItemSO] Use -> ID: {ID}, Name: {name}");
        }
    }
}
