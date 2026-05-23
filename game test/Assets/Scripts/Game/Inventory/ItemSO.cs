using IndieGame.Core.Utilities;
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

        // 本地化名称运行时缓存：
        // - 首次访问 GetLocalizedName() 时通过 LocalizedString.GetLocalizedString() 同步获取并缓存；
        // - 后续读取直接返回缓存值，避免在 UI 高频刷新（如背包详情面板）时反复发起本地化查询；
        // - 玩家切换语言后必须调用 ClearLocalizedCache() 让缓存失效，否则会读到旧语言文本。
        // 注：[System.NonSerialized] 保证缓存不被 Unity 写入 .asset 文件，仅在运行时存在。
        [System.NonSerialized] private string _cachedLocalizedName;

        /// <summary>
        /// 获取本地化的物品名称（同步）：
        /// <para>
        /// 与 <c>ItemName.GetLocalizedString()</c> 相比的优势：
        /// 1) 统一的 fallback —— ItemName 缺失时返回 ID，再缺失时返回 "Item"；
        /// 2) 运行时缓存，避免 UI 高频读取的本地化查询开销；
        /// 3) 集中调用点：将来若需要替换为异步加载或异常处理，只改本方法即可。
        /// </para>
        /// <para>调用方如果担心切换语言后缓存失效，可在切换后主动调用 <see cref="ClearLocalizedCache"/>。</para>
        /// </summary>
        public string GetLocalizedName()
        {
            if (!string.IsNullOrEmpty(_cachedLocalizedName))
            {
                return _cachedLocalizedName;
            }

            if (ItemName == null)
            {
                // 兜底：ItemName 未配置时使用 ID；ID 也为空时退化为"Item"，保证调用方拿到非空字符串。
                _cachedLocalizedName = string.IsNullOrEmpty(ID) ? "Item" : ID;
                return _cachedLocalizedName;
            }

            _cachedLocalizedName = ItemName.GetLocalizedString();
            return _cachedLocalizedName;
        }

        /// <summary>
        /// 清除本地化名称缓存。
        /// 调用时机：玩家切换语言后（如订阅 LocalizationSettings.SelectedLocaleChanged 后调用），
        /// 或单元测试中需要强制重新读取本地化文本时。
        /// </summary>
        public void ClearLocalizedCache()
        {
            _cachedLocalizedName = null;
        }

        public virtual void Use()
        {
            DebugTools.Log($"[ItemSO] Use -> ID: {ID}, Name: {GetLocalizedName()}");
        }
    }
}
